using System;
using System.Threading;
using System.Threading.Tasks;
using Core.DataAccess;
using Core.Exceptions;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataAccess.Concrete.Entityframework
{
    public sealed class GuidoraUnitOfWork : IUnitOfWork
    {
        private readonly GuidoraDbContext _context;

        public GuidoraUnitOfWork(GuidoraDbContext context)
        {
            _context = context;
        }

        public int SaveChanges()
        {
            try { return _context.SaveChanges(); }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                throw new DataConflictException("A record with the same unique values already exists.", exception);
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try { return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                throw new DataConflictException("A record with the same unique values already exists.", exception);
            }
        }

        private static bool IsUniqueViolation(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) return true;
            }
            return false;
        }
    }
}
