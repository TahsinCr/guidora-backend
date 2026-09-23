using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Castle.DynamicProxy;
using Core.Utilities.Results;
using Core.Utilities.Interceptors.Autofac;

namespace Core.Aspects.Transaction
{
    public class TransactionScopeAspect : MethodInterception
    {
        public const int InterceptorPriority = 400;

        public TransactionScopeAspect() => Priority = InterceptorPriority;

        public override void Intercept(IInvocation invocation)
        {
            var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TransactionManager.DefaultTimeout
                },
                TransactionScopeAsyncFlowOption.Enabled);
            try
            {
                invocation.Proceed();
                if (invocation.ReturnValue is Task task && invocation.Method.ReturnType.IsGenericType &&
                    invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    invocation.ReturnValue = WrapGenericTask(task, invocation, transactionScope);
                    return;
                }

                if (invocation.ReturnValue is Task taskWithoutResult)
                {
                    invocation.ReturnValue = WrapTask(taskWithoutResult, transactionScope);
                    return;
                }

                CompleteIfSuccessful(invocation.ReturnValue, transactionScope);
                transactionScope.Dispose();
            }
            catch
            {
                transactionScope.Dispose();
                throw;
            }
        }

        private static async Task WrapTask(Task task, TransactionScope transactionScope)
        {
            try
            {
                await task.ConfigureAwait(false);
                transactionScope.Complete();
            }
            finally
            {
                transactionScope.Dispose();
            }
        }

        private static object WrapGenericTask(Task task, IInvocation invocation,
            TransactionScope transactionScope)
        {
            var method = typeof(TransactionScopeAspect).GetMethod(nameof(WrapGenericTaskCore),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            return method.MakeGenericMethod(invocation.Method.ReturnType.GetGenericArguments()[0])
                .Invoke(null, [task, transactionScope])!;
        }

        private static async Task<T> WrapGenericTaskCore<T>(Task task, TransactionScope transactionScope)
        {
            try
            {
                var result = await ((Task<T>)task).ConfigureAwait(false);
                CompleteIfSuccessful(result, transactionScope);
                return result;
            }
            finally
            {
                transactionScope.Dispose();
            }
        }

        private static void CompleteIfSuccessful(object? result, TransactionScope transactionScope)
        {
            if (result is not IResult operationResult || operationResult.Success)
            {
                transactionScope.Complete();
            }
        }
    }
}
