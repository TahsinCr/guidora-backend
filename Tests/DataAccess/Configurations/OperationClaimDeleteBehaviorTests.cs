using Core.Entities.Concrete;
using DataAccess.Concrete.Entityframework.Contexts;
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
}
