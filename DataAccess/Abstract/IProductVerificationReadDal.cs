using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract;

public interface IProductVerificationReadDal
{
    Task<ProductVerificationStateSnapshot?> GetPublishedStateAsync(int productId, int? currentUserId,
        CancellationToken cancellationToken = default);
}
