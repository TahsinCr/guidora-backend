using System.Threading;
using System.Threading.Tasks;
using Core.DataAccess;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public sealed class GuidoraUnitOfWork : IUnitOfWork
    {
        private readonly GuidoraDbContext _context;

        public GuidoraUnitOfWork(GuidoraDbContext context)
        {
            _context = context;
        }

        public int SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
