using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
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

public sealed class FavoriteManager : IFavoriteService
{
    private readonly IFavoriteDal _favoriteDal;
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IUserDal _userDal;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public FavoriteManager(IFavoriteDal favoriteDal, IProductDal productDal, IProductStatusDal productStatusDal,
        IUserDal userDal, IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _favoriteDal = favoriteDal;
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _userDal = userDal;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    [SecuredOperationAspect("Favorite.Manage")]
    public async Task<IDataResult<PagedResult<Favorite>>> GetForUserAsync(int userId, PageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
            return new ErrorDataResult<PagedResult<Favorite>>("Invalid favorites page request.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue) return new ErrorDataResult<PagedResult<Favorite>>("Requested page is out of range.");
        var favorites = await _favoriteDal.GetByUserPageAsync(userId, (int)offset, request.PageSize, cancellationToken);
        var totalCount = await _favoriteDal.CountByUserAsync(userId, cancellationToken);
        return new SuccessDataResult<PagedResult<Favorite>>(
            new PagedResult<Favorite>(favorites, request.Page, request.PageSize, totalCount));
    }

    [TransactionScopeAspect]
    [SecuredOperationAspect("Favorite.Manage")]
    public async Task<IResult> AddAsync(int productId, int userId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveUserAsync(userId, cancellationToken)) return new ErrorResult("User is unavailable.");
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new ErrorResult("Product was not found.");
        var status = await _productStatusDal.GetAsync(item => item.Id == product.ProductStatusId, cancellationToken);
        if (status?.Code != "published") return new ErrorResult("Only published Products can be favorited.");

        if (await _favoriteDal.AnyAsync(item => item.UserId == userId && item.ProductId == productId, cancellationToken))
            return new SuccessResult("Product is already in favorites.");

        var favorite = new Favorite { UserId = userId, ProductId = productId, CreatedAt = System.DateTime.UtcNow };
        await _favoriteDal.AddAsync(favorite, cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(userId, nameof(Favorite), $"{userId}:{productId}",
            "Added", new { }, new { favorite.UserId, favorite.ProductId, favorite.CreatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product added to favorites.");
    }

    [TransactionScopeAspect]
    [SecuredOperationAspect("Favorite.Manage")]
    public async Task<IResult> RemoveAsync(int productId, int userId, CancellationToken cancellationToken = default)
    {
        var favorite = await _favoriteDal.GetAsync(item => item.UserId == userId && item.ProductId == productId,
            cancellationToken);
        if (favorite is null) return new SuccessResult("Product was not in favorites.");

        _favoriteDal.Delete(favorite);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(userId, nameof(Favorite), $"{userId}:{productId}",
            "Removed", new { favorite.UserId, favorite.ProductId, favorite.CreatedAt }, new { }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product removed from favorites.");
    }

    private async Task<bool> IsActiveUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _userDal.GetAsync(item => item.Id == userId, cancellationToken);
        return user is { Status: true };
    }
}
