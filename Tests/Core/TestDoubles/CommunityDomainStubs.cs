using Core.Entities.Concrete;
using Core.Pagination;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Core.TestDoubles;

public sealed class CommunityAuditDalStub : IAuditLogDal
{
    private readonly List<AuditLog> _entries = [];
    public IReadOnlyList<AuditLog> Entries => _entries;
    public void Add(AuditLog auditLog) => _entries.Add(auditLog);
    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        _entries.Add(auditLog);
        return Task.CompletedTask;
    }
    public IReadOnlyList<AuditLog> GetByEntity(string entityType, string entityId) =>
        _entries.Where(item => item.EntityType == entityType && item.EntityId == entityId).ToArray();
    public Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, string entityId,
        CancellationToken cancellationToken = default) => Task.FromResult(GetByEntity(entityType, entityId));
    public Task<AuditLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.SingleOrDefault(entry => entry.Id == id));
    public Task<bool> HasRevertAsync(long auditLogId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.Any(entry => entry.RevertsAuditLogId == auditLogId));
}

public sealed class CommunityFavoriteDalStub(IEnumerable<Favorite>? items = null)
    : EntityRepositoryStub<Favorite>(items), IFavoriteDal
{
    public IReadOnlyList<Favorite> Records => GetAll();
    public Task<List<Favorite>> GetByUserPageAsync(int userId, int offset, int pageSize,
        CancellationToken cancellationToken = default) => Task.FromResult(GetAll(item => item.UserId == userId)
        .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Skip(offset).Take(pageSize).ToList());
    public Task<int> CountByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetAll(item => item.UserId == userId).Count);
    public Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default)
    {
        if (GetAll(item => item.UserId == favorite.UserId && item.ProductId == favorite.ProductId).Count > 0)
            return Task.FromResult(false);
        Add(favorite);
        return Task.FromResult(true);
    }
}

public sealed class CommunityCommentDalStub(IEnumerable<Comment>? items = null)
    : EntityRepositoryStub<Comment>(items), ICommentDal
{
    public List<Comment> VisibleRecords { get; set; } = [];
    public override void Add(Comment entity)
    {
        entity.Id = Entities.Count == 0 ? 1 : Entities.Max(item => item.Id) + 1;
        base.Add(entity);
    }
    public Task<List<Comment>> GetVisibleByProductPageAsync(int productId, int offset, int pageSize,
        CancellationToken cancellationToken = default) => Task.FromResult(VisibleRecords.Where(item => item.ProductId == productId)
        .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Skip(offset).Take(pageSize).ToList());
    public Task<int> CountVisibleByProductAsync(int productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(VisibleRecords.Count(item => item.ProductId == productId));
}

public sealed class CommunityCommentStatusDalStub(IEnumerable<CommentStatus>? items = null)
    : EntityRepositoryStub<CommentStatus>(items), ICommentStatusDal;
public sealed class CommunityProductDalStub(IEnumerable<Product>? items = null)
    : EntityRepositoryStub<Product>(items), IProductDal
{
    public Task<List<ProductListItemDto>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
        CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? [] :
        GetAll(item => item.ProductStatusId == statusId).OrderBy(item => item.Name).Skip(skip).Take(take)
            .Select(ProductListItemDto.FromProduct).ToList());
    public Task<int> CountPublishedAsync(int statusId, int? categoryId,
        CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? 0 :
        GetAll(item => item.ProductStatusId == statusId).Count);
}
public sealed class CommunityProductStatusDalStub(IEnumerable<ProductStatus>? items = null)
    : EntityRepositoryStub<ProductStatus>(items), IProductStatusDal;
public sealed class CommunityUserDalStub(IEnumerable<User>? items = null)
    : EntityRepositoryStub<User>(items), IUserDal
{
    public Task<List<global::Core.Entities.Concrete.OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<global::Core.Entities.Concrete.OperationClaim>());
}
public sealed class CommunityVerificationDalStub(IEnumerable<ProductVerification>? items = null)
    : EntityRepositoryStub<ProductVerification>(items), IProductVerificationDal;
public sealed class CommunityDocumentDalStub(IEnumerable<ProductDocument>? items = null)
    : EntityRepositoryStub<ProductDocument>(items), IProductDocumentDal
{
    public Task<PagedResult<ProductDocumentListItemDto>> GetPageAsync(int productId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = GetAll(item => item.ProductId == productId).OrderBy(item => item.DocumentType)
            .ThenBy(item => item.Title).ThenBy(item => item.Id).ToList();
        var pageItems = query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new ProductDocumentListItemDto(item.Id, item.ProductId, item.DocumentType,
                item.Title, item.CreatedAt, item.UpdatedAt)).ToList();
        return Task.FromResult(new PagedResult<ProductDocumentListItemDto>(pageItems, page, pageSize, query.Count));
    }
}
public sealed class CommunityMediaDalStub(IEnumerable<ProductMedia>? items = null)
    : EntityRepositoryStub<ProductMedia>(items), IProductMediaDal;
public sealed class CommunityChangelogDalStub(IEnumerable<ProductChangelog>? items = null)
    : EntityRepositoryStub<ProductChangelog>(items), IProductChangelogDal
{
    public Task<PagedResult<ProductChangelogListItemDto>> GetPageAsync(int productId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = GetAll(item => item.ProductId == productId).OrderByDescending(item => item.ReleaseDate)
            .ThenByDescending(item => item.Id).ToList();
        var pageItems = query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new ProductChangelogListItemDto(item.Id, item.ProductId, item.Version,
                item.Title, item.ReleaseDate, item.CreatedAt)).ToList();
        return Task.FromResult(new PagedResult<ProductChangelogListItemDto>(pageItems, page, pageSize, query.Count));
    }
}
public sealed class CommunityContributorDalStub(IEnumerable<ProductContributor>? items = null)
    : EntityRepositoryStub<ProductContributor>(items), IProductContributorDal;
