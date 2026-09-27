using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserAuthenticationStateDal
        : EfEntityRepositoryBase<UserAuthenticationState, GuidoraDbContext>, IUserAuthenticationStateDal
    {
        private readonly GuidoraDbContext _context;
        public EfUserAuthenticationStateDal(GuidoraDbContext context) : base(context) => _context = context;

        public async Task IncrementAuthorizationVersionsAsync(IReadOnlyCollection<int> userIds,
            DateTime updatedAt, CancellationToken cancellationToken = default)
        {
            foreach (var userId in userIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "UserAuthenticationStates" ("UserId", "AuthorizationVersion", "UpdatedAt")
                    VALUES ({userId}, 2, {updatedAt})
                    ON CONFLICT ("UserId") DO UPDATE SET
                        "AuthorizationVersion" = "UserAuthenticationStates"."AuthorizationVersion" + 1,
                        "UpdatedAt" = EXCLUDED."UpdatedAt"
                    """, cancellationToken);
            }
        }
    }
}
