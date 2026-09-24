using Core.Entities.Concrete;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tests.DataAccess.Configurations;

public sealed class OperationClaimDeleteBehaviorTests
{
    [Fact]
    public void DeletingOperationClaimCascadesOnlyAssignmentRows()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        var userAssignmentForeignKey = context.Model.FindEntityType(typeof(UserOperationClaim))!
            .GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(OperationClaim));
        var roleAssignmentForeignKey = context.Model.FindEntityType(typeof(RoleOperationClaim))!
            .GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(OperationClaim));
        var userForeignKey = context.Model.FindEntityType(typeof(UserOperationClaim))!
            .GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(User));

        Assert.Equal(DeleteBehavior.Cascade, userAssignmentForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, roleAssignmentForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);
    }

    [Fact]
    public void ProductReportReviewPermissionIsSeededForModeratorAndAdminRoles()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        var model = context.GetService<IDesignTimeModel>().Model;
        var claims = model.FindEntityType(typeof(OperationClaim))!.GetSeedData();
        var reviewClaim = Assert.Single(claims, seed => seed["Name"] as string == "ProductReport.Review");
        Assert.Equal(7, Convert.ToInt32(reviewClaim["Id"]));

        var roleAssignments = model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData()
            .Where(seed => Convert.ToInt32(seed["OperationClaimId"]) == 7)
            .Select(seed => Convert.ToInt32(seed["RoleId"]))
            .Order()
            .ToArray();
        Assert.Equal([2, 3], roleAssignments);
    }

    [Fact]
    public void CommunityAndContentPermissionsAreSeededForExpectedRoles()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var claims = model.FindEntityType(typeof(OperationClaim))!.GetSeedData();
        var roleAssignments = model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData();
        var expected = new Dictionary<string, int>
        {
            ["Favorite.Manage"] = 8,
            ["Comment.Create"] = 9,
            ["Comment.Moderate"] = 10,
            ["Product.Content.Manage"] = 11,
            ["Product.Contributor.Manage"] = 12
        };

        foreach (var (name, id) in expected)
        {
            var seed = Assert.Single(claims, item => item["Name"] as string == name);
            Assert.Equal(id, Convert.ToInt32(seed["Id"]));
        }

        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 1 && Convert.ToInt32(item["OperationClaimId"]) == 8);
        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 1 && Convert.ToInt32(item["OperationClaimId"]) == 9);
        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 1 && Convert.ToInt32(item["OperationClaimId"]) == 11);
        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 1 && Convert.ToInt32(item["OperationClaimId"]) == 12);
        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 2 && Convert.ToInt32(item["OperationClaimId"]) == 10);
        Assert.Contains(roleAssignments, item => Convert.ToInt32(item["RoleId"]) == 3 && Convert.ToInt32(item["OperationClaimId"]) == 10);
    }

    [Fact]
    public void ProductIdentityHasCaseInsensitiveNameAndUniqueSlugWithoutUniqueWebsiteConstraint()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        var product = context.Model.FindEntityType(typeof(Product))!;
        Assert.Equal("citext", product.FindProperty(nameof(Product.Name))!.GetColumnType());
        Assert.True(product.GetIndexes().Single(index => index.Properties.Single().Name == nameof(Product.Name)).IsUnique);
        Assert.True(product.GetIndexes().Single(index => index.Properties.Single().Name == nameof(Product.Slug)).IsUnique);
        Assert.DoesNotContain(product.GetIndexes(), index => index.IsUnique && index.Properties.Any(property =>
            property.Name == nameof(Product.WebsiteUrl)));
    }
}
