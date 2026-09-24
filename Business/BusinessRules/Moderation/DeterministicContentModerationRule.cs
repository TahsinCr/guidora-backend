using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System;
using System.Reflection;
using System.Text;
using Core.Utilities.Results;

namespace Business.BusinessRules.Moderation;

public sealed class DeterministicContentModerationRule : IContentModerationRule
{
    private const string EnglishResource = "Guidora.Moderation.en.txt";
    private const string TurkishResource = "Guidora.Moderation.tr.txt";
    private readonly MultiPatternMatcher _matcher;

    public DeterministicContentModerationRule()
    {
        var assembly = typeof(DeterministicContentModerationRule).Assembly;
        var patterns = LoadPatterns(assembly, EnglishResource)
            .Concat(LoadPatterns(assembly, TurkishResource));
        _matcher = new MultiPatternMatcher(patterns);
    }

    public IResult EnsureAllowed(params string?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.Any(value => value is not null && _matcher.ContainsMatch(value))
            ? new ErrorResult("Content violates the community content policy.")
            : new SuccessResult();
    }

    private static IEnumerable<string> LoadPatterns(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Required moderation resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
        {
            var pattern = line.Trim();
            if (pattern.Length > 0 && !pattern.StartsWith('#'))
            {
                yield return pattern;
            }
        }
    }

    private sealed class MultiPatternMatcher
    {
        private readonly Node _root = new();

        public MultiPatternMatcher(IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                var normalized = Normalize(pattern, sourceText: null);
                if (normalized.Values.Count == 0)
                {
                    continue;
                }

                var node = _root;
                foreach (var value in normalized.Values)
                {
                    if (!node.Children.TryGetValue(value, out var child))
                    {
                        child = new Node();
                        node.Children.Add(value, child);
                    }

                    node = child;
                }

                node.MatchLengths.Add(normalized.Values.Count);
            }

            BuildFailureLinks();
        }

        public bool ContainsMatch(string source)
        {
            var text = Normalize(source, source);
            var node = _root;

            for (var index = 0; index < text.Values.Count; index++)
            {
                var value = text.Values[index];
                while (!ReferenceEquals(node, _root) && !node.Children.ContainsKey(value))
                {
                    node = node.Failure;
                }

                if (node.Children.TryGetValue(value, out var child))
                {
                    node = child;
                }

                foreach (var matchLength in node.MatchLengths)
                {
                    var start = index - matchLength + 1;
                    if (start >= 0 && HasTokenBoundaries(source, text.SourceStarts[start], text.SourceEnds[index]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void BuildFailureLinks()
        {
            _root.Failure = _root;
            var queue = new Queue<Node>();
            foreach (var child in _root.Children.Values)
            {
                child.Failure = _root;
                queue.Enqueue(child);
            }

            while (queue.TryDequeue(out var node))
            {
                foreach (var (value, child) in node.Children)
                {
                    var fallback = node.Failure;
                    while (!ReferenceEquals(fallback, _root) && !fallback.Children.ContainsKey(value))
                    {
                        fallback = fallback.Failure;
                    }

                    child.Failure = fallback.Children.TryGetValue(value, out var target) &&
                                     !ReferenceEquals(target, child)
                        ? target
                        : _root;
                    child.MatchLengths.AddRange(child.Failure.MatchLengths);
                    queue.Enqueue(child);
                }
            }
        }

        private static bool HasTokenBoundaries(string source, int sourceStart, int sourceEnd)
        {
            if (TryGetPreviousRune(source, sourceStart, out var previous) && Rune.IsLetterOrDigit(previous))
            {
                return false;
            }

            return !TryGetNextRune(source, sourceEnd + 1, out var next) || !Rune.IsLetterOrDigit(next);
        }

        private static bool TryGetPreviousRune(string source, int position, out Rune rune)
        {
            if (position <= 0)
            {
                rune = default;
                return false;
            }

            return Rune.DecodeLastFromUtf16(source.AsSpan(0, position), out rune, out _) == OperationStatus.Done;
        }

        private static bool TryGetNextRune(string source, int position, out Rune rune)
        {
            if (position >= source.Length)
            {
                rune = default;
                return false;
            }

            return Rune.DecodeFromUtf16(source.AsSpan(position), out rune, out _) == OperationStatus.Done;
        }

        private static NormalizedText Normalize(string value, string? sourceText)
        {
            var normalizedRunes = new List<Rune>(value.Length);
            var sourceStarts = new List<int>(value.Length);
            var sourceEnds = new List<int>(value.Length);
            var sourcePosition = 0;

            foreach (var rune in value.EnumerateRunes())
            {
                var start = sourceText is null ? -1 : sourcePosition;
                sourcePosition += rune.Utf16SequenceLength;
                var end = sourceText is null ? -1 : sourcePosition - 1;
                var compatibilityForm = rune.ToString().Normalize(NormalizationForm.FormKD);

                foreach (var normalizedRune in compatibilityForm.EnumerateRunes())
                {
                    if (Rune.GetUnicodeCategory(normalizedRune) is UnicodeCategory.NonSpacingMark or
                        UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                    {
                        continue;
                    }

                    var canonical = Canonicalize(Rune.ToLowerInvariant(normalizedRune));
                    if (!Rune.IsLetterOrDigit(canonical))
                    {
                        continue;
                    }

                    if (normalizedRunes.Count > 0 && normalizedRunes[^1] == canonical)
                    {
                        if (sourceText is not null)
                        {
                            sourceEnds[^1] = end;
                        }

                        continue;
                    }

                    normalizedRunes.Add(canonical);
                    if (sourceText is not null)
                    {
                        sourceStarts.Add(start);
                        sourceEnds.Add(end);
                    }
                }
            }

            return new NormalizedText(normalizedRunes, sourceStarts, sourceEnds);
        }

        private static Rune Canonicalize(Rune value) => value.Value switch
        {
            0x0131 => new Rune('i'), // Turkish dotless i
            '@' or '4' => new Rune('a'),
            '8' => new Rune('b'),
            '3' => new Rune('e'),
            '6' or '9' => new Rune('g'),
            '1' or '!' or '|' => new Rune('i'),
            '0' => new Rune('o'),
            '$' or '5' => new Rune('s'),
            '7' or '+' => new Rune('t'),
            _ => value
        };

        private sealed class Node
        {
            public Dictionary<Rune, Node> Children { get; } = [];
            public List<int> MatchLengths { get; } = [];
            public Node Failure { get; set; } = null!;
        }

        private sealed record NormalizedText(List<Rune> Values, List<int> SourceStarts, List<int> SourceEnds);
    }
}
