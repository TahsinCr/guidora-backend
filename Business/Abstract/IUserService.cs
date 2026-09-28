using Core.Entities.Concrete;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using System.Threading;

namespace Business.Abstract
{
    public interface IUserService
    {
        Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User user, CancellationToken cancellationToken = default);
        Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default);
        Task<IResult> RegisterUserAsync(User user, CancellationToken cancellationToken = default);
        Task<IResult> UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    }
}
