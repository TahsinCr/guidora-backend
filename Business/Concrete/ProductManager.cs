using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Constants;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Core.Entities;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Business.BusinessRules.Catalog;
using Business.BusinessRules.Moderation;
using Business.Utilities;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class ProductManager : IProductService
    {
        private readonly IProductDal _productDal;
        private readonly IProductCategoryDal _productCategoryDal;
        private readonly IProductStatusDal _productStatusDal;
        private readonly IProductTagDal _productTagDal;
        private readonly ITagDal _tagDal;
        private readonly ITagCategoryDal _tagCategoryDal;
        private readonly IProductVerificationDal _productVerificationDal;
        private readonly IProductEditProposalDal _productEditProposalDal;
        private readonly IProductEditProposalTagDal _proposalTagDal;
        private readonly IProductEditProposalCategoryDal _proposalCategoryDal;
        private readonly IAuditLogDal _auditLogDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductBusinessRules _businessRules;
        private readonly ITagBusinessRules _tagBusinessRules;
        private readonly IContentModerationRule _contentModerationRule;

        public ProductManager(IProductDal productDal, IProductCategoryDal productCategoryDal,
            IProductStatusDal productStatusDal, IProductTagDal productTagDal, ITagDal tagDal,
            ITagCategoryDal tagCategoryDal,
            IProductVerificationDal productVerificationDal, IProductEditProposalDal productEditProposalDal,
            IProductEditProposalTagDal proposalTagDal, IProductEditProposalCategoryDal proposalCategoryDal,
            IAuditLogDal auditLogDal, IUnitOfWork unitOfWork,
            IProductBusinessRules businessRules, ITagBusinessRules tagBusinessRules,
            IContentModerationRule contentModerationRule)
        {
            _productDal = productDal;
            _productCategoryDal = productCategoryDal;
            _productStatusDal = productStatusDal;
            _productTagDal = productTagDal;
            _tagDal = tagDal;
            _tagCategoryDal = tagCategoryDal;
            _productVerificationDal = productVerificationDal;
            _productEditProposalDal = productEditProposalDal;
            _proposalTagDal = proposalTagDal;
            _proposalCategoryDal = proposalCategoryDal;
            _auditLogDal = auditLogDal;
            _unitOfWork = unitOfWork;
            _businessRules = businessRules;
            _tagBusinessRules = tagBusinessRules;
            _contentModerationRule = contentModerationRule;
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAll()
        {
            var publishedStatus = _productStatusDal.Get(status => status.Code == "published");
            return publishedStatus is null
                ? new ErrorDataResult<List<Product>>("Published product status is not configured.")
                : new SuccessDataResult<List<Product>>(
                    _productDal.GetAll(product => product.ProductStatusId == publishedStatus.Id), Messages.ProductsListed);
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAllByCategoryId(int id)
        {
            var publishedStatus = _productStatusDal.Get(status => status.Code == "published");
            if (publishedStatus is null)
            {
                return new ErrorDataResult<List<Product>>("Published product status is not configured.");
            }

            var productIds = _productCategoryDal.GetAll(item => item.CategoryId == id)
                .Select(item => item.ProductId)
                .ToHashSet();
            return new SuccessDataResult<List<Product>>(
                _productDal.GetAll(product => productIds.Contains(product.ProductId) &&
                                               product.ProductStatusId == publishedStatus.Id));
        }

        [ValidationAspect(typeof(PageRequestValidator))]
        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public async Task<IDataResult<PagedResult<Product>>> GetPageAsync(PageRequest request, int? categoryId = null,
            CancellationToken cancellationToken = default)
        {
            if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
                return new ErrorDataResult<PagedResult<Product>>("Invalid product page request.");
            if (categoryId is <= 0)
                return new ErrorDataResult<PagedResult<Product>>("Category ID must be positive.");
            var status = await _productStatusDal.GetAsync(item => item.Code == "published", cancellationToken);
            if (status is null)
                return new ErrorDataResult<PagedResult<Product>>("Published product status is not configured.");
            var offset = ((long)request.Page - 1) * request.PageSize;
            if (offset > int.MaxValue)
                return new ErrorDataResult<PagedResult<Product>>("Requested page is out of range.");

            var items = await _productDal.GetPublishedPageAsync(status.Id, categoryId, (int)offset,
                request.PageSize, cancellationToken);
            var totalCount = await _productDal.CountPublishedAsync(status.Id, categoryId, cancellationToken);
            return new SuccessDataResult<PagedResult<Product>>(
                new PagedResult<Product>(items, request.Page, request.PageSize, totalCount), Messages.ProductsListed);
        }


        [CacheAspect(CacheProfiles.ProductDetail, duration: 15, VaryByArgumentIndexes = new[] { 0 })]
        public IDataResult<Product> GetById(int id)
        {
            var publishedStatus = _productStatusDal.Get(status => status.Code == "published");
            if (publishedStatus is null)
            {
                return new ErrorDataResult<Product>("Published product status is not configured.");
            }

            var product = _productDal.Get(product => product.ProductId == id &&
                                                      product.ProductStatusId == publishedStatus.Id);
            return product == null
                ? new NotFoundDataResult<Product>("Product was not found.")
                : new SuccessDataResult<Product>(product);
        }

        private async Task<IResult> AddNewProductAsync(Product product, CancellationToken cancellationToken)
        {
            product.Name = ProductIdentityNormalizer.NormalizeName(product.Name);
            product.Slug = ProductIdentityNormalizer.NormalizeSlug(product.Slug);
            product.WebsiteUrl = ProductIdentityNormalizer.NormalizeWebsiteUrl(product.WebsiteUrl);

            var moderationResult = _contentModerationRule.EnsureAllowed(product.Name, product.ShortDescription,
                product.Description);
            if (!moderationResult.Success) return moderationResult;

            IResult result = await _businessRules.EnsureIdentityIsAvailableAsync(product.Name, product.Slug,
                product.WebsiteUrl, cancellationToken: cancellationToken);
            if (!result.Success) return result;

            var publishedStatus = await _productStatusDal.GetAsync(status => status.Code == "published", cancellationToken);
            if (publishedStatus == null)
            {
                return new ErrorResult("Published product status is not configured.");
            }

            product.ProductStatusId = publishedStatus.Id;
            product.CreatedAt = DateTime.UtcNow;
            await _productDal.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult(Messages.ProductAdded);
            
        }

        [SecuredOperationAspect("Product.Create")]
        [ValidationAspect(typeof(CreateProductRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing)]
        [TransactionScopeAspect]
        public async Task<IResult> CreateAsync(CreateProductRequest request, int createdByUserId,
            CancellationToken cancellationToken = default)
        {
            var normalizedCategoryIds = request.CategoryIds.Distinct().ToArray();
            var categoryValidation = await _businessRules.EnsureCategoriesAreActiveAsync(normalizedCategoryIds,
                cancellationToken);
            if (!categoryValidation.Success) return categoryValidation;
            var normalizedTagIds = request.TagIds.Distinct().ToArray();
            var tagValidation = await EnsureTagSelectionIsValidAsync(normalizedTagIds, cancellationToken);
            if (!tagValidation.Success) return tagValidation;

            var product = new Product
            {
                Name = request.Name,
                Slug = request.Slug,
                ShortDescription = request.ShortDescription,
                Description = request.Description,
                WebsiteUrl = request.WebsiteUrl,
                LogoUrl = request.LogoUrl,
                CreatedByUserId = createdByUserId
            };

            var result = await AddNewProductAsync(product, cancellationToken);
            if (!result.Success)
            {
                return result;
            }

            _productCategoryDal.AddRange(normalizedCategoryIds.Select(categoryId =>
                new ProductCategory { ProductId = product.ProductId, CategoryId = categoryId }));
            _productTagDal.AddRange(normalizedTagIds.Select(tagId =>
                new ProductTag { ProductId = product.ProductId, TagId = tagId }));

            await _auditLogDal.AddAsync(AuditLogFactory.Create(createdByUserId, nameof(Product), product.ProductId.ToString(),
                "Created", new { }, new
                {
                    product.Name, product.Slug, product.ShortDescription, product.Description,
                    product.WebsiteUrl, product.LogoUrl, product.CreatedByUserId,
                    product.ProductStatusId, product.CreatedAt,
                    CategoryIds = normalizedCategoryIds,
                    TagIds = normalizedTagIds
                }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult(Messages.ProductAdded);
        }

        [SecuredOperationAspect("Product.Update")]
        [ValidationAspect(typeof(UpdateProductRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing)]
        [CacheRemoveAspect(CacheProfiles.ProductDetail, ArgumentIndex = 0)]
        [TransactionScopeAspect]
        public async Task<IResult> UpdateAsync(int productId, UpdateProductRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default)
        {
            var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
            if (product == null)
            {
                return new NotFoundResult("Product was not found.");
            }

            var normalizedRequest = new UpdateProductRequest
            {
                Name = ProductIdentityNormalizer.NormalizeName(request.Name),
                Slug = ProductIdentityNormalizer.NormalizeSlug(request.Slug),
                ShortDescription = request.ShortDescription.Trim(),
                Description = request.Description,
                WebsiteUrl = ProductIdentityNormalizer.NormalizeWebsiteUrl(request.WebsiteUrl),
                LogoUrl = request.LogoUrl,
                CategoryIds = request.CategoryIds,
                TagIds = request.TagIds
            };
            var moderationResult = _contentModerationRule.EnsureAllowed(normalizedRequest.Name,
                normalizedRequest.ShortDescription, normalizedRequest.Description);
            if (!moderationResult.Success) return moderationResult;

            var identityResult = await _businessRules.EnsureIdentityIsAvailableAsync(normalizedRequest.Name,
                normalizedRequest.Slug, normalizedRequest.WebsiteUrl, product.ProductId, cancellationToken);
            if (!identityResult.Success) return identityResult;

            var normalizedCategoryIds = normalizedRequest.CategoryIds.Distinct().ToArray();
            var normalizedTagIds = normalizedRequest.TagIds.Distinct().ToArray();
            var tagRule = await EnsureTagSelectionIsValidAsync(normalizedTagIds, cancellationToken);
            if (!tagRule.Success) return tagRule;

            var hasActiveVerification = await _productVerificationDal.AnyAsync(verification =>
                verification.ProductId == productId && verification.RevokedAt == null, cancellationToken);
            if (hasActiveVerification)
            {
                var categoryRule = await _businessRules.EnsureCategoriesAreActiveAsync(normalizedCategoryIds,
                    cancellationToken);
                if (!categoryRule.Success) return categoryRule;
                return await CreateEditProposalAsync(productId, updatedByUserId, normalizedRequest, normalizedCategoryIds,
                    normalizedTagIds, cancellationToken);
            }

            var oldValues = new
            {
                product.Name, product.Slug, product.ShortDescription, product.Description,
                product.WebsiteUrl, product.LogoUrl, product.UpdatedAt,
                CategoryIds = (await _productCategoryDal.GetAllAsync(item => item.ProductId == product.ProductId,
                    cancellationToken))
                    .Select(item => item.CategoryId).ToArray(),
                TagIds = (await _productTagDal.GetAllAsync(item => item.ProductId == product.ProductId,
                    cancellationToken))
                    .Select(item => item.TagId).ToArray()
            };
            product.Name = normalizedRequest.Name;
            product.Slug = normalizedRequest.Slug;
            product.ShortDescription = normalizedRequest.ShortDescription;
            product.Description = normalizedRequest.Description;
            product.WebsiteUrl = normalizedRequest.WebsiteUrl;
            product.LogoUrl = normalizedRequest.LogoUrl;
            product.UpdatedAt = DateTime.UtcNow;

            _productDal.Update(product);
            var categoryResult = await ReplaceCategoriesAsync(product.ProductId, normalizedCategoryIds, cancellationToken);
            if (!categoryResult.Success)
            {
                return categoryResult;
            }
            await ReplaceTagsAsync(product.ProductId, normalizedTagIds, cancellationToken);

            await _auditLogDal.AddAsync(AuditLogFactory.Create(updatedByUserId, nameof(Product), product.ProductId.ToString(),
                "Updated", oldValues, new
                {
                    product.Name, product.Slug, product.ShortDescription, product.Description,
                    product.WebsiteUrl, product.LogoUrl, product.UpdatedAt,
                    CategoryIds = normalizedCategoryIds,
                    TagIds = normalizedTagIds
                }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return categoryResult;
        }

        private async Task<IResult> CreateEditProposalAsync(int productId, int proposedByUserId,
            UpdateProductRequest request, IReadOnlyCollection<int> categoryIds, IReadOnlyCollection<int> tagIds,
            CancellationToken cancellationToken)
        {
            var proposal = new ProductEditProposal
            {
                ProductId = productId,
                ProposedByUserId = proposedByUserId,
                Status = ProductEditProposalStatus.Pending,
                Name = request.Name,
                Slug = request.Slug,
                ShortDescription = request.ShortDescription,
                Description = request.Description,
                WebsiteUrl = request.WebsiteUrl,
                LogoUrl = request.LogoUrl,
                CreatedAt = DateTime.UtcNow
            };
            await _productEditProposalDal.AddAsync(proposal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var categoryId in categoryIds)
            {
                _proposalCategoryDal.Add(new ProductEditProposalCategory
                {
                    ProductEditProposalId = proposal.Id,
                    CategoryId = categoryId
                });
            }

            foreach (var tagId in tagIds)
            {
                _proposalTagDal.Add(new ProductEditProposalTag
                {
                    ProductEditProposalId = proposal.Id,
                    TagId = tagId
                });
            }

            await _auditLogDal.AddAsync(AuditLogFactory.Create(proposedByUserId, nameof(ProductEditProposal),
                proposal.Id.ToString(), "Submitted", new { }, new
                {
                    proposal.ProductId, proposal.Name, proposal.Slug, proposal.ShortDescription,
                    proposal.Description, proposal.WebsiteUrl, proposal.LogoUrl, proposal.ProposedByUserId,
                    Status = proposal.Status.ToString(), proposal.CreatedAt,
                    CategoryIds = categoryIds,
                    TagIds = tagIds
                }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult("Edit proposal submitted for review.");
        }

        [SecuredOperationAspect("Product.Delete")]
        [CacheRemoveAspect(CacheProfiles.CatalogListing)]
        [CacheRemoveAspect(CacheProfiles.ProductDetail, ArgumentIndex = 0)]
        [TransactionScopeAspect]
        public async Task<IResult> DeleteAsync(int productId, int deletedByUserId,
            CancellationToken cancellationToken = default)
        {
            var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
            if (product == null)
            {
                return new NotFoundResult("Product was not found.");
            }

            var deletedStatus = await _productStatusDal.GetAsync(status => status.Code == "deleted", cancellationToken);
            if (deletedStatus == null)
            {
                return new ErrorResult("Deleted product status is not configured.");
            }

            var oldStatus = await _productStatusDal.GetAsync(status => status.Id == product.ProductStatusId,
                cancellationToken);
            var oldUpdatedAt = product.UpdatedAt;
            product.ProductStatusId = deletedStatus.Id;
            product.UpdatedAt = DateTime.UtcNow;
            _productDal.Update(product);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(deletedByUserId, nameof(Product), product.ProductId.ToString(),
                "Deleted", new { Status = oldStatus?.Code, UpdatedAt = oldUpdatedAt },
                new { Status = deletedStatus.Code, UpdatedAt = product.UpdatedAt }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }

        private async Task<IResult> ReplaceCategoriesAsync(int productId, IEnumerable<int> categoryIds,
            CancellationToken cancellationToken)
        {
            var normalizedCategoryIds = categoryIds.Distinct().ToList();
            var categoryRule = await _businessRules.EnsureCategoriesAreActiveAsync(normalizedCategoryIds,
                cancellationToken);
            if (!categoryRule.Success)
            {
                return categoryRule;
            }

            var existing = await _productCategoryDal.GetAllAsync(item => item.ProductId == productId,
                cancellationToken);
            var requestedSet = normalizedCategoryIds.ToHashSet();
            var existingSet = existing.Select(item => item.CategoryId).ToHashSet();
            _productCategoryDal.PurgeRange(existing.Where(item => !requestedSet.Contains(item.CategoryId)));
            _productCategoryDal.AddRange(requestedSet.Except(existingSet).Select(categoryId =>
                new ProductCategory { ProductId = productId, CategoryId = categoryId }));

            return new SuccessResult();
        }

        private async Task<IResult> EnsureTagSelectionIsValidAsync(IEnumerable<int> tagIds,
            CancellationToken cancellationToken)
        {
            var normalizedIds = tagIds.Distinct().ToArray();
            var tags = await _tagDal.GetAllAsync(tag => normalizedIds.Contains(tag.Id), cancellationToken);
            if (tags.Count != normalizedIds.Length)
                return new ErrorResult("One or more selected tags are missing or inactive.");
            var categoryIds = tags.Select(tag => tag.TagCategoryId).Distinct().ToArray();
            var categories = await _tagCategoryDal.GetAllAsync(category => categoryIds.Contains(category.Id), cancellationToken);
            return _tagBusinessRules.EnsureSelectionIsValid(tags, categories);
        }

        private async Task ReplaceTagsAsync(int productId, int[] tagIds, CancellationToken cancellationToken)
        {
            var existing = await _productTagDal.GetAllAsync(item => item.ProductId == productId, cancellationToken);
            var requestedSet = tagIds.ToHashSet();
            var existingSet = existing.Select(item => item.TagId).ToHashSet();
            _productTagDal.PurgeRange(existing.Where(item => !requestedSet.Contains(item.TagId)));
            _productTagDal.AddRange(requestedSet.Except(existingSet)
                .Select(tagId => new ProductTag { ProductId = productId, TagId = tagId }));
        }
    }
}
