using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Reflection;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class CommunityControllerContractTests
{
    [Theory]
    [InlineData(typeof(FavoritesController), "api/favorites")]
    [InlineData(typeof(ProductCommentsController), "api")]
    [InlineData(typeof(ProductDocumentsController), "api/products/{productId:int}/documents")]
    [InlineData(typeof(ProductMediaController), "api/products/{productId:int}/media")]
    [InlineData(typeof(ProductChangelogsController), "api/products/{productId:int}/changelogs")]
    [InlineData(typeof(ProductContributorsController), "api/products/{productId:int}/contributors")]
    public void CommunityControllerHasExpectedResourceRoute(Type controllerType, string expectedRoute)
    {
        Assert.Equal(expectedRoute, controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>().Single().Template);
    }

    [Fact]
    public void ProductCommentsDoNotExposeReplyPersistence()
    {
        Assert.Null(typeof(global::Entities.Comment).GetProperty("ParentCommentId"));
    }

    [Theory]
    [InlineData(typeof(ProductsController), "Add", "product-create")]
    [InlineData(typeof(ProductsController), "Update", "product-update")]
    [InlineData(typeof(ProductCommentsController), "Create", "comment-create")]
    [InlineData(typeof(ProductReportsController), "Submit", "product-report-submit")]
    [InlineData(typeof(FavoritesController), "Add", "favorite-mutation")]
    [InlineData(typeof(FavoritesController), "Remove", "favorite-mutation")]
    [InlineData(typeof(ProductVerificationsController), "VoteCommunity", "community-vote")]
    [InlineData(typeof(ProductDocumentsController), "Create", "product-content-mutation")]
    [InlineData(typeof(ProductDocumentsController), "Update", "product-content-mutation")]
    [InlineData(typeof(ProductDocumentsController), "Delete", "product-content-mutation")]
    [InlineData(typeof(ProductChangelogsController), "Create", "product-content-mutation")]
    [InlineData(typeof(ProductMediaController), "Create", "product-metadata-mutation")]
    [InlineData(typeof(ProductMediaController), "Update", "product-metadata-mutation")]
    [InlineData(typeof(ProductMediaController), "Delete", "product-metadata-mutation")]
    [InlineData(typeof(ProductContributorsController), "Add", "product-metadata-mutation")]
    [InlineData(typeof(ProductContributorsController), "Remove", "product-metadata-mutation")]
    public void UserGeneratedWriteEndpointsHaveRateLimits(Type controller, string methodName, string policy)
    {
        var method = controller.GetMethod(methodName);
        Assert.NotNull(method);
        var attribute = Assert.Single(method!.GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Equal(policy, attribute.PolicyName);
    }
}
