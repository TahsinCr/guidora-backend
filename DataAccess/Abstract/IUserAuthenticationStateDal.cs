using Core.DataAccess;
using Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IUserAuthenticationStateDal : IEntityRepository<UserAuthenticationState>
    {
        Task IncrementAuthorizationVersionsAsync(IReadOnlyCollection<int> userIds, DateTime updatedAt,
            CancellationToken cancellationToken = default);
    }
}
