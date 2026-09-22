using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Core.Aspects.Transaction
{
    public class TransactionScopeAspect : MethodInterception
    {
        public override void Intercept(IInvocation invocation)
        {
            using (TransactionScope transactionScope = new TransactionScope(
                       TransactionScopeOption.Required,
                       TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    invocation.Proceed();
                    transactionScope.Complete();
                }
                catch
                {
                    transactionScope.Dispose();
                    throw;
                }
            }
        }
    }
}
