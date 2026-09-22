using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;

namespace Core.Aspects.Transaction
{
    public class TransactionScopeAspect : MethodInterception
    {
        public override void Intercept(IInvocation invocation)
        {
            var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
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

                transactionScope.Complete();
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
                transactionScope.Complete();
                return result;
            }
            finally
            {
                transactionScope.Dispose();
            }
        }
    }
}
