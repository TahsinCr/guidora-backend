using Microsoft.AspNetCore.Mvc;
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
}
