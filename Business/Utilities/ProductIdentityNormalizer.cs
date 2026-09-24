using System;
using System.Linq;
using System.Text;

namespace Business.Utilities;

public static class ProductIdentityNormalizer
{
    private static readonly string[] TrackingQueryPrefixes = ["utm_"];
    private static readonly string[] TrackingQueryKeys = ["fbclid", "gclid", "dclid", "msclkid"];

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().Normalize(NormalizationForm.FormKC);
    }

    public static string NormalizeSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return slug.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
    }

    public static string NormalizeWebsiteUrl(string websiteUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(websiteUrl);
        if (!Uri.TryCreate(websiteUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Website URL must be an absolute HTTP or HTTPS URL.", nameof(websiteUrl));
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Fragment = string.Empty
        };
        builder.Path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');

        var query = NormalizeQuery(uri.Query);
        builder.Query = query;
        var normalized = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return query.Length == 0 ? normalized : $"{normalized}?{query}";
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var separator = part.IndexOf('=');
                var key = Uri.UnescapeDataString(separator < 0 ? part : part[..separator]);
                var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(part[(separator + 1)..]);
                return (Key: key, Value: value);
            })
            .Where(pair => !TrackingQueryKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) &&
                           !TrackingQueryPrefixes.Any(prefix => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")
            .ToArray() is { Length: > 0 } parts
            ? string.Join('&', parts)
            : string.Empty;
    }
}
