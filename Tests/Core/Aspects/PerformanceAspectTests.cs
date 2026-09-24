using Castle.DynamicProxy;
using Core.Aspects.Performance;
using Microsoft.Extensions.Logging;

namespace Tests.Core.Aspects;

public sealed class PerformanceAspectTests
{
    [Fact]
    public void LogsSlowSynchronousOperationThroughStructuredLogger()
    {
        var logger = new RecordingLogger();
        var proxy = CreateProxy(new PerformanceAspect(0, logger));

        proxy.SlowOperation();

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(nameof(IPerformanceTarget.SlowOperation), warning.Message);
        Assert.Contains("ms", warning.Message);
    }

    [Fact]
    public async Task LogsSlowAsynchronousOperationAfterItCompletes()
    {
        var logger = new RecordingLogger();
        var proxy = CreateProxy(new PerformanceAspect(0, logger));

        await proxy.SlowOperationAsync();

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(nameof(IPerformanceTarget.SlowOperationAsync), warning.Message);
    }

    [Fact]
    public void MeasuresSynchronousExceptionAndPreservesIt()
    {
        var logger = new RecordingLogger();
        var proxy = CreateProxy(new PerformanceAspect(0, logger));

        Assert.Throws<InvalidOperationException>(proxy.ThrowSynchronously);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    private static IPerformanceTarget CreateProxy(PerformanceAspect aspect) =>
        new ProxyGenerator().CreateInterfaceProxyWithTarget<IPerformanceTarget>(
            new PerformanceTarget(), aspect);

    public interface IPerformanceTarget
    {
        void SlowOperation();
        Task SlowOperationAsync();
        void ThrowSynchronously();
    }

    private sealed class PerformanceTarget : IPerformanceTarget
    {
        public void SlowOperation() => Thread.Sleep(5);

        public async Task SlowOperationAsync() => await Task.Delay(5);

        public void ThrowSynchronously() => throw new InvalidOperationException("expected");
    }

    private sealed class RecordingLogger : ILogger<PerformanceAspect>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
