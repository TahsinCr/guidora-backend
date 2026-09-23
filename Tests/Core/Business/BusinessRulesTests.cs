using Core.Utilities.Business;
using Core.Utilities.Results;

namespace Tests.Core.Business;

public sealed class BusinessRulesTests
{
    [Fact]
    public void Run_ReturnsFirstFailureAndDoesNotRunLaterRules()
    {
        var visited = new List<int>();
        var failure = new ErrorResult("blocked");

        var result = BusinessRules.Run(
            () => { visited.Add(1); return new SuccessResult(); },
            () => { visited.Add(2); return failure; },
            () => { visited.Add(3); return new SuccessResult(); });

        Assert.Same(failure, result);
        Assert.Equal([1, 2], visited);
    }

    [Fact]
    public void Run_ReturnsNonNullSuccessWhenAllRulesPass()
    {
        var result = BusinessRules.Run(() => new SuccessResult(), () => new SuccessResult("ignored"));

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public async Task RunAsync_ExecutesSequentiallyAndStopsOnFirstFailure()
    {
        var visited = new List<int>();
        var failure = new ErrorResult("blocked");

        var result = await BusinessRules.RunAsync(
            async () => { visited.Add(1); await Task.Yield(); return new SuccessResult(); },
            async () => { visited.Add(2); await Task.Yield(); return failure; },
            () => { visited.Add(3); return Task.FromResult<IResult>(new SuccessResult()); });

        Assert.Same(failure, result);
        Assert.Equal([1, 2], visited);
    }

    [Fact]
    public async Task RunConcurrentAsync_RunsAllRulesAndReturnsFirstFailureInInputOrder()
    {
        var ran = 0;
        var firstFailure = new ErrorResult("first");
        var secondFailure = new ErrorResult("second");

        var result = await BusinessRules.RunConcurrentAsync(
            async () => { Interlocked.Increment(ref ran); await Task.Delay(20); return firstFailure; },
            async () => { Interlocked.Increment(ref ran); await Task.Yield(); return secondFailure; },
            async () => { Interlocked.Increment(ref ran); await Task.Yield(); return new SuccessResult(); });

        Assert.Equal(3, ran);
        Assert.Same(firstFailure, result);
    }
}
