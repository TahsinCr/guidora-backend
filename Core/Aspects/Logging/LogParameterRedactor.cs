using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.Aspects.Logging
{
    internal static class LogParameterRedactor
    {
        private const string RedactedValue = "[REDACTED]";

        public static object? Redact(string parameterName, object? value)
        {
            if (IsSensitiveName(parameterName))
            {
                return RedactedValue;
            }

            if (value is null or string || value.GetType().IsPrimitive || value.GetType().IsEnum ||
                value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid)
            {
                return value;
            }

            try
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    MaxDepth = 32,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                var sanitized = value is JToken token
                    ? token.DeepClone()
                    : JToken.FromObject(value, serializer);
                RedactToken(sanitized);
                return sanitized;
            }
            catch (Exception)
            {
                // Logging must fail closed; never fall back to the original object.
                return $"<{value.GetType().Name} omitted from logs>";
            }
        }

        private static void RedactToken(JToken token)
        {
            if (token is JObject objectToken)
            {
                foreach (var property in objectToken.Properties().ToArray())
                {
                    if (IsSensitiveName(property.Name))
                    {
                        property.Value = RedactedValue;
                    }
                    else
                    {
                        RedactToken(property.Value);
                    }
                }

                return;
            }

            if (token is JArray arrayToken)
            {
                foreach (var child in arrayToken)
                {
                    RedactToken(child);
                }
            }
        }

        private static bool IsSensitiveName(string name)
        {
            var normalized = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return normalized.Contains("password", StringComparison.Ordinal) ||
                   normalized.Contains("token", StringComparison.Ordinal) ||
                   normalized.Contains("secret", StringComparison.Ordinal) ||
                   normalized.Contains("authorization", StringComparison.Ordinal) ||
                   normalized.Contains("credential", StringComparison.Ordinal) ||
                   normalized.Contains("cookie", StringComparison.Ordinal) ||
                   normalized is "key" or "apikey" or "accesskey" or "privatekey" or "securitykey" or "signingkey";
        }
    }
}
