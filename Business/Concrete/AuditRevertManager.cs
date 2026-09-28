using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.BusinessRules.Catalog;
using Business.Constants;
using Business.Utilities;
using Core.Aspects.Caching;
using Core.Aspects.Transaction;
using Core.CrossCuttingConcerns.Caching;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete;

public sealed class AuditRevertManager : IAuditRevertService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAuditLogDal _auditLogDal;
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IProductCategoryDal _productCategoryDal;
    private readonly IProductTagDal _productTagDal;
    private readonly ICategoryDal _categoryDal;
    private readonly IProductBusinessRules _productRules;
    private readonly ICategoryBusinessRules _categoryRules;
    private readonly ITagDal _tagDal;
    private readonly ITagCategoryDal _tagCategoryDal;
    private readonly ITagBusinessRules _tagRules;
    private readonly IUnitOfWork _unitOfWork;

    public AuditRevertManager(IAuditLogDal auditLogDal, IProductDal productDal,
        IProductStatusDal productStatusDal, IProductCategoryDal productCategoryDal,
        IProductTagDal productTagDal, ICategoryDal categoryDal,
        IProductBusinessRules productRules, ICategoryBusinessRules categoryRules,
        ITagDal tagDal, ITagCategoryDal tagCategoryDal, ITagBusinessRules tagRules,
        IUnitOfWork unitOfWork)
    {
        _auditLogDal = auditLogDal;
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _productCategoryDal = productCategoryDal;
        _productTagDal = productTagDal;
        _categoryDal = categoryDal;
        _productRules = productRules;
        _categoryRules = categoryRules;
        _tagDal = tagDal;
        _tagCategoryDal = tagCategoryDal;
        _tagRules = tagRules;
        _unitOfWork = unitOfWork;
    }

    [SecuredOperationAspect("Audit.Revert")]
    [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail, CacheProfiles.CatalogNavigation)]
    [TransactionScopeAspect]
    public async Task<IResult> RevertAsync(long auditLogId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (auditLogId <= 0 || actorUserId <= 0)
            return new ErrorResult("A valid audit entry and authenticated actor are required.");

        var source = await _auditLogDal.GetByIdAsync(auditLogId, cancellationToken);
        if (source is null) return new NotFoundResult("Audit entry was not found.");
        if (await _auditLogDal.HasRevertAsync(source.Id, cancellationToken))
            return new ConflictResult("This audit entry has already been reverted.");

        try
        {
            return source.EntityType switch
            {
                nameof(Product) => await RevertProductAsync(source, actorUserId, cancellationToken),
                nameof(Category) => await RevertCategoryAsync(source, actorUserId, cancellationToken),
                _ => new ErrorResult("This audit entity type is not reversible in V1.")
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("The entity changed while the revert was being applied. Reload its latest state and try again.");
        }
    }

    private async Task<IResult> RevertProductAsync(AuditLog source, int actorUserId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(source.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var productId))
            return new ErrorResult("The audit entry contains an invalid Product identifier.");

        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new ErrorResult("The audited Product no longer exists.");
        var categories = (await _productCategoryDal.GetAllAsync(item => item.ProductId == productId, cancellationToken))
            .Select(item => item.CategoryId).Order().ToArray();
        var tags = (await _productTagDal.GetAllAsync(item => item.ProductId == productId, cancellationToken))
            .Select(item => item.TagId).Order().ToArray();
        var currentStatus = await _productStatusDal.GetAsync(item => item.Id == product.ProductStatusId, cancellationToken);
        if (currentStatus is null) return new ErrorResult("The current Product status is unavailable.");
        var current = Snapshot(product, currentStatus.Code, categories, tags);
        if (!MatchesSnapshot(source.NewValues, current))
            return new ConflictResult("The Product no longer matches this audit entry, so the revert was rejected.");

        string? nextStatusCode = currentStatus.Code;
        var nextCategories = categories;
        var nextTags = tags;

        if (source.Action == "Created")
        {
            nextStatusCode = "deleted";
            product.UpdatedAt = DateTime.UtcNow;
        }
        else if (source.Action == "Deleted")
        {
            var oldStatus = ReadString(source.OldValues, "Status");
            if (string.IsNullOrWhiteSpace(oldStatus)) return new ErrorResult("The original Product status is missing from the audit entry.");
            nextStatusCode = oldStatus;
            product.UpdatedAt = ReadDateTime(source.OldValues, "UpdatedAt");
        }
        else if (source.Action is "Updated" or "EditProposalAccepted" or "Reverted")
        {
            var identity = Deserialize<ProductSnapshot>(source.OldValues);
            if (identity is null) return new ErrorResult("The original Product snapshot is invalid.");
            var name = identity.Name ?? product.Name;
            var slug = identity.Slug ?? product.Slug;
            var websiteUrl = identity.WebsiteUrl ?? product.WebsiteUrl;
            var identityResult = await _productRules.EnsureIdentityIsAvailableAsync(name, slug, websiteUrl,
                productId, cancellationToken);
            if (!identityResult.Success) return identityResult;

            if (TryReadIntArray(source.OldValues, "CategoryIds", out var oldCategories))
            {
                var categoryResult = await _productRules.EnsureCategoriesAreActiveAsync(oldCategories, cancellationToken);
                if (!categoryResult.Success) return categoryResult;
                nextCategories = oldCategories.Distinct().Order().ToArray();
            }

            if (TryReadIntArray(source.OldValues, "TagIds", out var oldTags))
            {
                var selectedTags = await _tagDal.GetAllAsync(tag => oldTags.Contains(tag.Id), cancellationToken);
                if (selectedTags.Count != oldTags.Distinct().Count())
                    return new ErrorResult("The original Product Tags are no longer available.");
                var tagCategoryIds = selectedTags.Select(tag => tag.TagCategoryId).Distinct().ToArray();
                var tagCategories = await _tagCategoryDal.GetAllAsync(category => tagCategoryIds.Contains(category.Id), cancellationToken);
                var tagResult = _tagRules.EnsureSelectionIsValid(selectedTags, tagCategories);
                if (!tagResult.Success) return tagResult;
                nextTags = oldTags.Distinct().Order().ToArray();
            }

            product.Name = name;
            product.Slug = slug;
            product.ShortDescription = identity.ShortDescription ?? product.ShortDescription;
            product.Description = identity.Description ?? product.Description;
            product.WebsiteUrl = websiteUrl;
            product.LogoUrl = identity.LogoUrl ?? product.LogoUrl;
            product.UpdatedAt = identity.UpdatedAt;

            if (TryReadString(source.OldValues, "Status", out var oldStatusCode))
                nextStatusCode = oldStatusCode;
        }
        else
        {
            return new ErrorResult($"Product audit action '{source.Action}' is not reversible in V1.");
        }

        var targetStatus = await _productStatusDal.GetAsync(item => item.Code == nextStatusCode, cancellationToken);
        if (targetStatus is null) return new ErrorResult("The target Product status is not configured.");
        product.ProductStatusId = targetStatus.Id;
        _productDal.Update(product);

        if (!categories.SequenceEqual(nextCategories))
        {
            _productCategoryDal.PurgeRange(await _productCategoryDal.GetAllAsync(
                item => item.ProductId == productId, cancellationToken));
            _productCategoryDal.AddRange(nextCategories.Select(categoryId =>
                new ProductCategory { ProductId = productId, CategoryId = categoryId }));
        }
        if (!tags.SequenceEqual(nextTags))
        {
            _productTagDal.PurgeRange(await _productTagDal.GetAllAsync(
                item => item.ProductId == productId, cancellationToken));
            _productTagDal.AddRange(nextTags.Select(tagId => new ProductTag { ProductId = productId, TagId = tagId }));
        }

        var restored = Snapshot(product, targetStatus.Code, nextCategories, nextTags);
        await AddRevertAuditAsync(source, actorUserId, current, restored, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CacheInvalidationSuccessResult("Product changes were reverted and recorded in the audit history.",
            new CacheInvalidationTarget(CacheProfiles.CatalogListing),
            new CacheInvalidationTarget(CacheProfiles.ProductDetail, ArgumentIndex: 0, ArgumentValue: productId));
    }

    private async Task<IResult> RevertCategoryAsync(AuditLog source, int actorUserId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(source.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var categoryId))
            return new ErrorResult("The audit entry contains an invalid Category identifier.");

        var category = await _categoryDal.GetAsync(item => item.CategoryId == categoryId, cancellationToken);
        if (category is null) return new ErrorResult("The audited Category no longer exists.");
        var current = Snapshot(category);
        if (!MatchesSnapshot(source.NewValues, current))
            return new ConflictResult("The Category no longer matches this audit entry, so the revert was rejected.");

        if (source.Action == "Created")
        {
            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;
        }
        else if (source.Action is "Updated" or "Deactivated" or "Reverted")
        {
            var oldValues = Deserialize<CategorySnapshot>(source.OldValues);
            if (oldValues is null) return new ErrorResult("The original Category snapshot is invalid.");
            var slug = oldValues.Slug ?? category.Slug;
            var uniquenessResult = await _categoryRules.EnsureSlugIsAvailableAsync(slug, categoryId, cancellationToken);
            if (!uniquenessResult.Success) return uniquenessResult;
            category.CategoryName = oldValues.CategoryName ?? category.CategoryName;
            category.Slug = slug;
            category.Description = oldValues.Description ?? category.Description;
            if (TryReadBoolean(source.OldValues, "IsActive", out var isActive)) category.IsActive = isActive;
            if (TryReadDateTime(source.OldValues, "UpdatedAt", out var updatedAt)) category.UpdatedAt = updatedAt;
        }
        else
        {
            return new ErrorResult($"Category audit action '{source.Action}' is not reversible in V1.");
        }

        _categoryDal.Update(category);
        var restored = Snapshot(category);
        await AddRevertAuditAsync(source, actorUserId, current, restored, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CacheInvalidationSuccessResult("Category changes were reverted and recorded in the audit history.",
            new CacheInvalidationTarget(CacheProfiles.CatalogListing),
            new CacheInvalidationTarget(CacheProfiles.CatalogNavigation));
    }

    private async Task AddRevertAuditAsync<TSnapshot>(AuditLog source, int actorUserId,
        TSnapshot previous, TSnapshot restored, CancellationToken cancellationToken)
    {
        await _auditLogDal.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            EntityType = source.EntityType,
            EntityId = source.EntityId,
            Action = "Reverted",
            OldValues = JsonSerializer.Serialize(previous),
            NewValues = JsonSerializer.Serialize(restored),
            RevertsAuditLogId = source.Id,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    private static ProductSnapshot Snapshot(Product product, string statusCode,
        int[] categoryIds, int[] tagIds) => new()
    {
        Name = product.Name, Slug = product.Slug, ShortDescription = product.ShortDescription,
        Description = product.Description, WebsiteUrl = product.WebsiteUrl, LogoUrl = product.LogoUrl,
        UpdatedAt = product.UpdatedAt, CategoryIds = categoryIds, TagIds = tagIds,
        ProductStatusId = product.ProductStatusId, CreatedByUserId = product.CreatedByUserId,
        CreatedAt = product.CreatedAt, Status = statusCode
    };

    private static CategorySnapshot Snapshot(Category category) => new()
    {
        CategoryName = category.CategoryName, Slug = category.Slug, Description = category.Description,
        IsActive = category.IsActive, CreatedAt = category.CreatedAt, UpdatedAt = category.UpdatedAt
    };

    private static TSnapshot? Deserialize<TSnapshot>(string json) where TSnapshot : class =>
        JsonSerializer.Deserialize<TSnapshot>(json, JsonOptions);

    private static bool MatchesSnapshot(string expectedJson, object currentSnapshot)
    {
        using var expectedDocument = JsonDocument.Parse(expectedJson);
        using var currentDocument = JsonDocument.Parse(JsonSerializer.Serialize(currentSnapshot));
        if (expectedDocument.RootElement.ValueKind != JsonValueKind.Object ||
            currentDocument.RootElement.ValueKind != JsonValueKind.Object) return false;

        foreach (var expectedProperty in expectedDocument.RootElement.EnumerateObject())
        {
            if (expectedProperty.Name is "ProposalId" or "SourceAuditLogId") continue;
            if (!currentDocument.RootElement.TryGetProperty(expectedProperty.Name, out var currentProperty) ||
                !JsonValuesEqual(expectedProperty.Name, expectedProperty.Value, currentProperty)) return false;
        }
        return true;
    }

    private static bool JsonValuesEqual(string propertyName, JsonElement expected, JsonElement current)
    {
        if (propertyName is "CategoryIds" or "TagIds" && expected.ValueKind == JsonValueKind.Array &&
            current.ValueKind == JsonValueKind.Array)
        {
            return expected.EnumerateArray().Select(item => item.GetInt32()).Order()
                .SequenceEqual(current.EnumerateArray().Select(item => item.GetInt32()).Order());
        }

        if (expected.ValueKind != current.ValueKind) return false;
        if (propertyName.EndsWith("At", StringComparison.Ordinal) &&
            expected.ValueKind == JsonValueKind.String && current.ValueKind == JsonValueKind.String &&
            expected.TryGetDateTime(out var expectedTime) && current.TryGetDateTime(out var currentTime))
        {
            return TruncateToPostgreSqlTimestampPrecision(expectedTime) ==
                   TruncateToPostgreSqlTimestampPrecision(currentTime);
        }

        return expected.ValueKind switch
        {
            JsonValueKind.String => expected.GetString() == current.GetString(),
            JsonValueKind.Number => expected.GetDecimal() == current.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => expected.GetBoolean() == current.GetBoolean(),
            JsonValueKind.Null => true,
            _ => expected.GetRawText() == current.GetRawText()
        };
    }

    private static DateTime TruncateToPostgreSqlTimestampPrecision(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, value.Kind);

    private static string? ReadString(string json, string propertyName) =>
        TryReadString(json, propertyName, out var value) ? value : null;

    private static bool TryReadString(string json, string propertyName, out string value)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static DateTime? ReadDateTime(string json, string propertyName) =>
        TryReadDateTime(json, propertyName, out var value) ? value : null;

    private static bool TryReadDateTime(string json, string propertyName, out DateTime? value)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var element))
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                value = null;
                return true;
            }
            if (element.ValueKind == JsonValueKind.String && element.TryGetDateTime(out var dateTime))
            {
                value = dateTime;
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool TryReadBoolean(string json, string propertyName, out bool value)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var element) &&
            element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryReadIntArray(string json, string propertyName, out int[] values)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array)
        {
            values = element.EnumerateArray().Select(item => item.GetInt32()).ToArray();
            return true;
        }
        values = [];
        return false;
    }

    private sealed class ProductSnapshot
    {
        public string? Name { get; init; }
        public string? Slug { get; init; }
        public string? ShortDescription { get; init; }
        public string? Description { get; init; }
        public string? WebsiteUrl { get; init; }
        public string? LogoUrl { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public int[]? CategoryIds { get; init; }
        public int[]? TagIds { get; init; }
        public int? ProductStatusId { get; init; }
        public int? CreatedByUserId { get; init; }
        public DateTime? CreatedAt { get; init; }
        public string? Status { get; init; }
    }

    private sealed class CategorySnapshot
    {
        public string? CategoryName { get; init; }
        public string? Slug { get; init; }
        public string? Description { get; init; }
        public bool? IsActive { get; init; }
        public DateTime? CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
