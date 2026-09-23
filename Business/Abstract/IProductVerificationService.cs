using Core.Utilities.Results;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductVerificationService
{
    Task<IResult> VerifyAsAdminAsync(int productId, int verifierUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokeAdminVerificationAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
}
