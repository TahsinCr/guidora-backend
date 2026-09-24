using Core.Utilities.Results;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Utilities.Business;

public static class BusinessRules
{
    public static IResult Run(params Func<IResult>[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            var result = rule() ?? throw new InvalidOperationException("A business rule returned null.");
            if (!result.Success)
            {
                return result;
            }
        }

        return new SuccessResult();
    }

    public static IResult Run(string successMessage, params Func<IResult>[] rules)
    {
        ArgumentNullException.ThrowIfNull(successMessage);
        var result = Run(rules);
        return result.Success ? new SuccessResult(successMessage) : result;
    }

    public static async Task<IResult> RunAsync(params Func<Task<IResult>>[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            var result = await rule().ConfigureAwait(false)
                         ?? throw new InvalidOperationException("A business rule returned null.");
            if (!result.Success)
            {
                return result;
            }
        }

        return new SuccessResult();
    }

    public static async Task<IResult> RunAsync(string successMessage, params Func<Task<IResult>>[] rules)
    {
        ArgumentNullException.ThrowIfNull(successMessage);
        var result = await RunAsync(rules).ConfigureAwait(false);
        return result.Success ? new SuccessResult(successMessage) : result;
    }

    public static async Task<IResult> RunConcurrentAsync(params Func<Task<IResult>>[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var results = await Task.WhenAll(rules.Select(rule =>
        {
            ArgumentNullException.ThrowIfNull(rule);
            return rule();
        })).ConfigureAwait(false);

        foreach (var result in results)
        {
            if (result is null)
            {
                throw new InvalidOperationException("A business rule returned null.");
            }

            if (!result.Success)
            {
                return result;
            }
        }

        return new SuccessResult();
    }

    public static async Task<IResult> RunConcurrentAsync(string successMessage, params Func<Task<IResult>>[] rules)
    {
        ArgumentNullException.ThrowIfNull(successMessage);
        var result = await RunConcurrentAsync(rules).ConfigureAwait(false);
        return result.Success ? new SuccessResult(successMessage) : result;
    }
}
