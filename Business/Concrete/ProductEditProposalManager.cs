using System;
using System.Linq;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.BusinessRules.Catalog;
using Business.Constants;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Caching;
using Core.Aspects.Transaction;
using Core.CrossCuttingConcerns.Caching;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete;

public sealed class ProductEditProposalManager : IProductEditProposalService
{
    private readonly IProductEditProposalDal _proposalDal;
    private readonly IProductDal _productDal;
    private readonly IProductCategoryDal _productCategoryDal;
    private readonly IProductTagDal _productTagDal;
    private readonly IProductEditProposalCategoryDal _proposalCategoryDal;
    private readonly IProductEditProposalTagDal _proposalTagDal;
    private readonly ITagDal _tagDal;
    private readonly ITagCategoryDal _tagCategoryDal;
    private readonly IProductBusinessRules _productRules;
    private readonly ITagBusinessRules _tagRules;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductEditProposalManager(IProductEditProposalDal proposalDal, IProductDal productDal,
        IProductCategoryDal productCategoryDal, IProductTagDal productTagDal,
        IProductEditProposalCategoryDal proposalCategoryDal, IProductEditProposalTagDal proposalTagDal,
        ITagDal tagDal, ITagCategoryDal tagCategoryDal,
        IProductBusinessRules productRules, ITagBusinessRules tagRules,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _proposalDal = proposalDal;
        _productDal = productDal;
        _productCategoryDal = productCategoryDal;
        _productTagDal = productTagDal;
        _proposalCategoryDal = proposalCategoryDal;
        _proposalTagDal = proposalTagDal;
        _tagDal = tagDal;
        _tagCategoryDal = tagCategoryDal;
        _productRules = productRules;
        _tagRules = tagRules;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    [TransactionScopeAspect]
    [SecuredOperationAspect("Product.Proposal.Review")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
    public async Task<IResult> AcceptAsync(int proposalId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _proposalDal.GetAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal is null) return new ErrorResult("Product edit proposal was not found.");
        if (proposal.Status != ProductEditProposalStatus.Pending)
            return new ErrorResult("Only pending proposals can be reviewed.");

        var product = await _productDal.GetAsync(item => item.ProductId == proposal.ProductId, cancellationToken);
        if (product is null) return new ErrorResult("The canonical product was not found.");

        var categoryIds = (await _proposalCategoryDal.GetAllAsync(
                item => item.ProductEditProposalId == proposalId, cancellationToken))
            .Select(item => item.CategoryId).Distinct().ToArray();
        var tagIds = (await _proposalTagDal.GetAllAsync(
                item => item.ProductEditProposalId == proposalId, cancellationToken))
            .Select(item => item.TagId).Distinct().ToArray();

        var nameResult = await _productRules.EnsureNameIsAvailableAsync(proposal.Name, product.ProductId, cancellationToken);
        if (!nameResult.Success) return nameResult;
        var categoryResult = await _productRules.EnsureCategoriesAreActiveAsync(categoryIds, cancellationToken);
        if (!categoryResult.Success) return categoryResult;

        var tags = await _tagDal.GetAllAsync(tag => tagIds.Contains(tag.Id), cancellationToken);
        if (tags.Count != tagIds.Length)
            return new ErrorResult("One or more proposed tags are no longer available.");
        var tagCategoryIds = tags.Select(tag => tag.TagCategoryId).Distinct().ToArray();
        var tagResult = _tagRules.EnsureSelectionIsValid(tags,
            await _tagCategoryDal.GetAllAsync(category => tagCategoryIds.Contains(category.Id), cancellationToken));
        if (!tagResult.Success) return tagResult;

        var oldValues = new
        {
            product.Name,
            product.Slug,
            product.ShortDescription,
            product.Description,
            product.WebsiteUrl,
            product.LogoUrl,
            product.UpdatedAt,
            CategoryIds = (await _productCategoryDal.GetAllAsync(item => item.ProductId == product.ProductId, cancellationToken))
                .Select(item => item.CategoryId).ToArray(),
            TagIds = (await _productTagDal.GetAllAsync(item => item.ProductId == product.ProductId, cancellationToken))
                .Select(item => item.TagId).ToArray()
        };

        product.Name = proposal.Name;
        product.Slug = proposal.Slug;
        product.ShortDescription = proposal.ShortDescription;
        product.Description = proposal.Description;
        product.WebsiteUrl = proposal.WebsiteUrl;
        product.LogoUrl = proposal.LogoUrl;
        product.UpdatedAt = DateTime.UtcNow;
        _productDal.Update(product);
        await ReplaceLinksAsync(product.ProductId, categoryIds, tagIds, cancellationToken);

        proposal.Status = ProductEditProposalStatus.Accepted;
        var oldProposalReview = new
        {
            Status = ProductEditProposalStatus.Pending.ToString(),
            ReviewedAt = proposal.ReviewedAt,
            ReviewedByUserId = proposal.ReviewedByUserId,
            proposal.ReviewNote
        };
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.ReviewedByUserId = reviewerUserId;
        proposal.ReviewNote = request.Note;
        _proposalDal.Update(proposal);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(reviewerUserId, nameof(Product), product.ProductId.ToString(),
            "EditProposalAccepted", oldValues, new
            {
                product.Name,
                product.Slug,
                product.ShortDescription,
                product.Description,
                product.WebsiteUrl,
                product.LogoUrl,
                product.UpdatedAt,
                CategoryIds = categoryIds,
                TagIds = tagIds,
                ProposalId = proposalId
            }), cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(reviewerUserId, nameof(ProductEditProposal),
            proposalId.ToString(), "Accepted", oldProposalReview, new
            {
                Status = proposal.Status.ToString(),
                proposal.ReviewedAt,
                proposal.ReviewedByUserId,
                proposal.ReviewNote
            }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product edit proposal accepted.");
    }

    [TransactionScopeAspect]
    [SecuredOperationAspect("Product.Proposal.Review")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    public async Task<IResult> RejectAsync(int proposalId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _proposalDal.GetAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal is null) return new ErrorResult("Product edit proposal was not found.");
        if (proposal.Status != ProductEditProposalStatus.Pending)
            return new ErrorResult("Only pending proposals can be reviewed.");

        var oldReview = new
        {
            Status = ProductEditProposalStatus.Pending.ToString(),
            ReviewedAt = proposal.ReviewedAt,
            ReviewedByUserId = proposal.ReviewedByUserId,
            proposal.ReviewNote
        };
        proposal.Status = ProductEditProposalStatus.Rejected;
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.ReviewedByUserId = reviewerUserId;
        proposal.ReviewNote = request.Note;
        _proposalDal.Update(proposal);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(reviewerUserId, nameof(ProductEditProposal), proposalId.ToString(),
            "Rejected", oldReview, new
            {
                Status = proposal.Status.ToString(),
                proposal.ReviewNote,
                proposal.ProductId
            }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product edit proposal rejected.");
    }

    [SecuredOperationAspect("Product.Proposal.Review")]
    [ValidationAspect(typeof(PageRequestValidator))]
    public async Task<IDataResult<PagedResult<ProductEditProposal>>> GetPendingAsync(PageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
            return new ErrorDataResult<PagedResult<ProductEditProposal>>("Invalid moderation page request.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            return new ErrorDataResult<PagedResult<ProductEditProposal>>("Requested page is out of range.");
        var items = await _proposalDal.GetPendingPageAsync((int)offset, request.PageSize, cancellationToken);
        var totalCount = await _proposalDal.CountPendingAsync(cancellationToken);
        return new SuccessDataResult<PagedResult<ProductEditProposal>>(
            new PagedResult<ProductEditProposal>(items, request.Page, request.PageSize, totalCount));
    }

    private async Task ReplaceLinksAsync(int productId, int[] categoryIds, int[] tagIds,
        CancellationToken cancellationToken)
    {
        _productCategoryDal.DeleteRange(await _productCategoryDal.GetAllAsync(
            item => item.ProductId == productId, cancellationToken));
        _productTagDal.DeleteRange(await _productTagDal.GetAllAsync(item => item.ProductId == productId, cancellationToken));
        _productCategoryDal.AddRange(categoryIds.Select(categoryId =>
            new ProductCategory { ProductId = productId, CategoryId = categoryId }));
        _productTagDal.AddRange(tagIds.Select(tagId => new ProductTag { ProductId = productId, TagId = tagId }));
    }
}
