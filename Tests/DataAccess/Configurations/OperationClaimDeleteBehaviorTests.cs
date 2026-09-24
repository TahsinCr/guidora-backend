using Core.Entities.Concrete;
using Core.DataAccess;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tests.DataAccess.Configurations;

public sealed class OperationClaimDeleteBehaviorTests
{
    [Fact]
    public void PhysicalPurgeIsOptInRatherThanPartOfEveryRepositoryContract()
    {
        Assert.DoesNotContain(typeof(IEntityRepository<Product>).GetMethods(), method =>
            method.Name is nameof(IEntityPurgeRepository<Product>.Purge) or nameof(IEntityPurgeRepository<Product>.PurgeRange));
        Assert.Contains(typeof(IEntityPurgeRepository<ProductCategory>), typeof(IProductCategoryDal).GetInterfaces());
        Assert.DoesNotContain(typeof(IEntityPurgeRepository<Product>), typeof(IProductDal).GetInterfaces());
    }

    [Fact]
    public void CatalogEntitiesUsePostgreSqlXminOptimisticConcurrencyTokens()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        foreach (var entityType in new[] { typeof(Product), typeof(Category) })
        {
            var token = context.Model.FindEntityType(entityType)!.FindProperty("xmin");
            Assert.NotNull(token);
            Assert.True(token!.IsConcurrencyToken);
            Assert.True(token.ValueGenerated.HasFlag(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate));
        }
    }

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
    public void CategoryManagementPermissionIsSeededOnlyForAdminByDefault()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var categoryManageClaim = Assert.Single(model.FindEntityType(typeof(OperationClaim))!.GetSeedData(),
            seed => seed["Name"] as string == "Category.Manage");
        var claimId = Convert.ToInt32(categoryManageClaim["Id"]);
        var roleIds = model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData()
            .Where(seed => Convert.ToInt32(seed["OperationClaimId"]) == claimId)
            .Select(seed => Convert.ToInt32(seed["RoleId"]))
            .ToArray();

        Assert.Equal([3], roleIds);
    }

    [Fact]
    public void ProductUpdatePermissionIsSeededForTheCommunityUserRole()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var updateClaim = Assert.Single(model.FindEntityType(typeof(OperationClaim))!.GetSeedData(),
            seed => seed["Name"] as string == "Product.Update");

        var assignment = Assert.Single(model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData(),
            seed => Convert.ToInt32(seed["OperationClaimId"]) == Convert.ToInt32(updateClaim["Id"])
                    && Convert.ToInt32(seed["RoleId"]) == 1);
        Assert.Equal(25, Convert.ToInt32(assignment["Id"]));
    }

    [Fact]
    public void V1VerificationCatalogContainsOnlyAdminAndCommunityTypes()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        var types = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(VerificationType))!.GetSeedData()
            .Select(seed => (string)seed["Code"]!)
            .Order()
            .ToArray();

        Assert.Equal(["admin", "community"], types);
    }

    [Fact]
    public void UserSessionsUsePostgreSqlXminForRefreshRotationConcurrency()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var token = context.Model.FindEntityType(typeof(UserSession))!.FindProperty("xmin");

        Assert.NotNull(token);
        Assert.True(token!.IsConcurrencyToken);
        Assert.True(token.ValueGenerated.HasFlag(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate));
    }

    [Fact]
    public void GovernanceReviewRecordsUseXminAndAuditRevertsAreUnique()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        foreach (var entityType in new[] { typeof(ProductReport), typeof(ProductEditProposal), typeof(ProductVerification) })
        {
            var token = context.Model.FindEntityType(entityType)!.FindProperty("xmin");
            Assert.NotNull(token);
            Assert.True(token!.IsConcurrencyToken);
        }

        var revertIndex = context.Model.FindEntityType(typeof(AuditLog))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_AuditLogs_RevertsAuditLogId");
        Assert.True(revertIndex.IsUnique);
        Assert.Equal("\"RevertsAuditLogId\" IS NOT NULL", revertIndex.GetFilter());
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
    public void UserManagementPermissionIsReservedForTheAdminRole()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var claims = model.FindEntityType(typeof(OperationClaim))!.GetSeedData();
        var userManagementClaim = Assert.Single(claims, seed => seed["Name"] as string == "User.Manage");
        Assert.Equal(13, Convert.ToInt32(userManagementClaim["Id"]));

        var assignments = model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData()
            .Where(seed => Convert.ToInt32(seed["OperationClaimId"]) == 13)
            .Select(seed => Convert.ToInt32(seed["RoleId"]))
            .ToArray();
        Assert.Equal([3], assignments);
    }

    [Fact]
    public void AuditRevertPermissionIsReservedForTheAdminRole()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var claims = model.FindEntityType(typeof(OperationClaim))!.GetSeedData();
        var auditClaim = Assert.Single(claims, seed => seed["Name"] as string == "Audit.Revert");
        Assert.Equal(14, Convert.ToInt32(auditClaim["Id"]));
        var assignments = model.FindEntityType(typeof(RoleOperationClaim))!.GetSeedData()
            .Where(seed => Convert.ToInt32(seed["OperationClaimId"]) == 14)
            .Select(seed => Convert.ToInt32(seed["RoleId"]))
            .ToArray();
        Assert.Equal([3], assignments);
    }

    [Fact]
    public void ProductIdentityHasDatabaseUniquenessForNameSlugAndExactNormalizedWebsiteUrl()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata-only;Username=unused;Password=unused")
            .Options;
        using var context = new GuidoraDbContext(options);

        var product = context.Model.FindEntityType(typeof(Product))!;
        Assert.Equal("citext", product.FindProperty(nameof(Product.Name))!.GetColumnType());
        Assert.True(product.GetIndexes()
            .Single(index => index.Properties.Single().Name == nameof(Product.Name)).IsUnique);
        Assert.True(product.GetIndexes()
            .Single(index => index.Properties.Single().Name == nameof(Product.Slug)).IsUnique);
        Assert.True(product.GetIndexes()
            .Single(index => index.Properties.Single().Name == nameof(Product.WebsiteUrl)).IsUnique);
    }
}
