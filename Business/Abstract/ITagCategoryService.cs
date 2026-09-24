using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface ITagCategoryService
{
    Task<IDataResult<List<TagCategory>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(TagCategoryRequest request, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int id, TagCategoryRequest request, int actorUserId,
        CancellationToken cancellationToken = default);
    Task<IResult> DeactivateAsync(int id, int actorUserId, CancellationToken cancellationToken = default);
}
