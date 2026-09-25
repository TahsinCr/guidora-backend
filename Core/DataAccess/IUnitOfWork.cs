using System.Threading;
using System.Threading.Tasks;

namespace Core.DataAccess
{
    public interface IUnitOfWork
    {
        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task AcquireTransactionLockAsync(long key, CancellationToken cancellationToken = default);
    }
}
