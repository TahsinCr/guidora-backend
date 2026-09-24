using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface ITagService
{
    Task<IDataResult<List<Tag>>> GetActiveAsync(int? tagCategoryId = null,
        CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(TagRequest request, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int id, TagRequest request, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> DeactivateAsync(int id, int actorUserId, CancellationToken cancellationToken = default);
}
