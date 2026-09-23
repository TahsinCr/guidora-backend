using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Core.Utilities.Interceptors.Autofac
{
    public abstract class MethodInterception : MethodInterceptionBaseAttribute
    {
        protected virtual void OnBefore(IInvocation invocation) { }
        protected virtual void OnAfter(IInvocation invocation) { }
        protected virtual void OnException(IInvocation invocation, Exception e) { }
        protected virtual void OnSuccess(IInvocation invocation) { }
        protected virtual void OnSuccess(IInvocation invocation, object? result) => OnSuccess(invocation);
        public override void Intercept(IInvocation invocation)
        {
            OnBefore(invocation);
            try
            {
                invocation.Proceed();
                if (invocation.ReturnValue is Task task &&
                    invocation.Method.ReturnType.IsGenericType &&
                    invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    invocation.ReturnValue = WrapGenericTask(task, invocation);
                    return;
                }

                if (invocation.ReturnValue is Task taskWithoutResult)
                {
                    invocation.ReturnValue = WrapTask(taskWithoutResult, invocation);
                    return;
                }

                OnSuccess(invocation, invocation.ReturnValue);
            }
            catch (Exception e)
            {
                OnException(invocation, e);
                throw;
            }
            finally
            {
                if (invocation.ReturnValue is not Task)
                {
                    OnAfter(invocation);
                }
            }
        }

        private async Task WrapTask(Task task, IInvocation invocation)
        {
            try
            {
                await task.ConfigureAwait(false);
                OnSuccess(invocation, null);
            }
            catch (Exception e)
            {
                OnException(invocation, e);
                throw;
            }
            finally
            {
                OnAfter(invocation);
            }
        }

        private object WrapGenericTask(Task task, IInvocation invocation)
        {
            var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
            var method = typeof(MethodInterception).GetMethod(nameof(WrapGenericTaskCore),
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return method.MakeGenericMethod(resultType).Invoke(this, [task, invocation])!;
        }

        private async Task<T> WrapGenericTaskCore<T>(Task task, IInvocation invocation)
        {
            try
            {
                var result = await ((Task<T>)task).ConfigureAwait(false);
                OnSuccess(invocation, result);
                return result;
            }
            catch (Exception e)
            {
                OnException(invocation, e);
                throw;
            }
            finally
            {
                OnAfter(invocation);
            }
        }
    }
}
