using System;

namespace Core.CrossCuttingConcerns.Logging
{
    public class LogParameter
    {
        public string Name { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
