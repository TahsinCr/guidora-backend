using System;

namespace Core.Exceptions;

/// <summary>Signals a persistence conflict caused by a database uniqueness constraint.</summary>
public sealed class DataConflictException : Exception
{
    public DataConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
