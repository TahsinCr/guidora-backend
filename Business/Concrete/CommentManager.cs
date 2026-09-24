using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.BusinessRules.Moderation;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete;

public sealed class CommentManager : ICommentService
{
    private readonly ICommentDal _commentDal;
    private readonly ICommentStatusDal _commentStatusDal;
    private readonly IUserDal _userDal;
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IContentModerationRule _contentModerationRule;

    public CommentManager(ICommentDal commentDal, ICommentStatusDal commentStatusDal, IUserDal userDal,
        IProductDal productDal, IProductStatusDal productStatusDal, IAuditLogDal auditLogDal, IUnitOfWork unitOfWork,
        IContentModerationRule contentModerationRule)
    {
        _commentDal = commentDal;
        _commentStatusDal = commentStatusDal;
        _userDal = userDal;
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
        _contentModerationRule = contentModerationRule;
    }

    public async Task<IDataResult<PagedResult<Comment>>> GetVisibleByProductAsync(int productId,
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
            return new ErrorDataResult<PagedResult<Comment>>("Invalid comment page request.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue) return new ErrorDataResult<PagedResult<Comment>>("Requested page is out of range.");
        var comments = await _commentDal.GetVisibleByProductPageAsync(productId, (int)offset, request.PageSize,
            cancellationToken);
        var totalCount = await _commentDal.CountVisibleByProductAsync(productId, cancellationToken);
        return new SuccessDataResult<PagedResult<Comment>>(
            new PagedResult<Comment>(comments, request.Page, request.PageSize, totalCount));
    }

    [SecuredOperationAspect("Comment.Create")]
    [ValidationAspect(typeof(CommentRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(int productId, int userId, CommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _contentModerationRule.EnsureAllowed(request.Content);
        if (!moderationResult.Success) return moderationResult;

        var user = await _userDal.GetAsync(item => item.Id == userId, cancellationToken);
        if (user is not { Status: true }) return new ErrorResult("User is unavailable.");
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new ErrorResult("Product was not found.");
        var productStatus = await _productStatusDal.GetAsync(item => item.Id == product.ProductStatusId, cancellationToken);
        if (productStatus?.Code != "published") return new ErrorResult("Comments are available only for published Products.");
        var activeStatus = await _commentStatusDal.GetAsync(item => item.Code == "active", cancellationToken);
        if (activeStatus is null) return new ErrorResult("Active comment status is not configured.");

        var comment = new Comment
        {
            ProductId = productId,
            UserId = userId,
            CommentStatusId = activeStatus.Id,
            Content = request.Content.Trim(),
            CreatedAt = System.DateTime.UtcNow
        };
        await _commentDal.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(userId, nameof(Comment), comment.Id.ToString(), "Created",
            new { }, new { comment.ProductId, comment.UserId, comment.CommentStatusId, comment.Content, comment.CreatedAt }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Comment created.");
    }

    [SecuredOperationAspect("Comment.Create")]
    [TransactionScopeAspect]
    public async Task<IResult> DeleteAsync(int commentId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentDal.GetAsync(item => item.Id == commentId, cancellationToken);
        if (comment is null) return new ErrorResult("Comment was not found.");
        if (comment.UserId != actorUserId) return new ErrorResult("Only the author can delete this comment.");
        var currentStatus = await _commentStatusDal.GetAsync(item => item.Id == comment.CommentStatusId, cancellationToken);
        if (currentStatus?.Code == "deleted") return new SuccessResult("Comment is already deleted.");
        var deletedStatus = await _commentStatusDal.GetAsync(item => item.Code == "deleted", cancellationToken);
        if (deletedStatus is null) return new ErrorResult("Deleted comment status is not configured.");

        var oldValues = new { Status = currentStatus?.Code, comment.Content, comment.UpdatedAt };
        comment.CommentStatusId = deletedStatus.Id;
        comment.UpdatedAt = System.DateTime.UtcNow;
        _commentDal.Update(comment);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Comment), commentId.ToString(),
            "Deleted", oldValues, new { Status = deletedStatus.Code, comment.Content, comment.UpdatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Comment deleted.");
    }

    [SecuredOperationAspect("Comment.Moderate")]
    [TransactionScopeAspect]
    public async Task<IResult> SetVisibilityAsync(int commentId, int actorUserId, bool isVisible,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentDal.GetAsync(item => item.Id == commentId, cancellationToken);
        if (comment is null) return new ErrorResult("Comment was not found.");
        var currentStatus = await _commentStatusDal.GetAsync(item => item.Id == comment.CommentStatusId, cancellationToken);
        if (currentStatus is null || currentStatus.Code is "deleted" or "blocked")
            return new ErrorResult("This comment status cannot be changed by this operation.");
        var targetCode = isVisible ? "active" : "hidden";
        if (currentStatus.Code == targetCode) return new SuccessResult("Comment already has the requested visibility.");
        var targetStatus = await _commentStatusDal.GetAsync(item => item.Code == targetCode, cancellationToken);
        if (targetStatus is null) return new ErrorResult("Target comment status is not configured.");

        comment.CommentStatusId = targetStatus.Id;
        comment.UpdatedAt = System.DateTime.UtcNow;
        _commentDal.Update(comment);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Comment), commentId.ToString(),
            isVisible ? "Shown" : "Hidden", new { Status = currentStatus.Code },
            new { Status = targetStatus.Code, comment.UpdatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult(isVisible ? "Comment is visible." : "Comment is hidden.");
    }
}
