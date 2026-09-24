using Core.Utilities.Results;
using Business.BusinessRules.Moderation;
using DataAccess.Abstract;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Content;

public sealed class ProductResourceRules : IProductResourceRules
{
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _statusDal;
    private readonly IProductVerificationDal _verificationDal;
    private readonly IContentModerationRule _contentModerationRule;

    public ProductResourceRules(IProductDal productDal, IProductStatusDal statusDal,
        IProductVerificationDal verificationDal, IContentModerationRule contentModerationRule)
    {
        _productDal = productDal;
        _statusDal = statusDal;
        _verificationDal = verificationDal;
        _contentModerationRule = contentModerationRule;
    }

    public IResult EnsureContentAllowed(params string?[] values) => _contentModerationRule.EnsureAllowed(values);

    public async Task<IResult> EnsurePublishedAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        var status = await _statusDal.GetAsync(item => item.Id == product.ProductStatusId, cancellationToken);
        return status?.Code == "published"
            ? new SuccessResult()
            : new ConflictResult("Product is not published.");
    }

    public async Task<IResult> EnsureDirectContentChangeAllowedAsync(int productId,
        CancellationToken cancellationToken = default)
    {
        var publishedResult = await EnsurePublishedAsync(productId, cancellationToken);
        if (!publishedResult.Success) return publishedResult;
        if (await _verificationDal.AnyAsync(item => item.ProductId == productId && item.RevokedAt == null,
                cancellationToken))
            return new ConflictResult("Verified Product content changes require a review proposal; this resource is not proposal-enabled in V1.");
        return new SuccessResult();
    }
}
