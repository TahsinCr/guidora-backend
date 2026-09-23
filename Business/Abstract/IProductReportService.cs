using Entities.DTOs;
using Core.Utilities.Results;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductReportService
{
    Task<IResult> SubmitAsync(int productId, int reporterUserId, ProductReportRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> ResolveAsync(int reportId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> DismissAsync(int reportId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IDataResult<PagedResult<ProductReport>>> GetOpenAsync(PageRequest request,
        CancellationToken cancellationToken = default);
}
