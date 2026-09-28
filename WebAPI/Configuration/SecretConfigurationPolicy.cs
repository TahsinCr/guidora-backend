using System;
using System.Text;

namespace WebAPI.Configuration;

public static class SecretConfigurationPolicy
{
    private static readonly string[] TemplatePrefixes =
        ["replace-with-", "change-me", "example-", "placeholder-"];

    public static bool IsConfigured(string? value, int minimumUtf8Bytes = 32)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < minimumUtf8Bytes)
            return false;

        var normalized = value.Trim();
        foreach (var prefix in TemplatePrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
