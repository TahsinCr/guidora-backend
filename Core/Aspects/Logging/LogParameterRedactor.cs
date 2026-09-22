using System;

namespace Core.Aspects.Logging
{
    internal static class LogParameterRedactor
    {
        public static object? Redact(string parameterName, object? value)
        {
            if (parameterName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Contains("authorization", StringComparison.OrdinalIgnoreCase))
            {
                return "[REDACTED]";
            }

            return value;
        }
    }
}
