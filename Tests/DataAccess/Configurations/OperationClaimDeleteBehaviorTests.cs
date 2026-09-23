using Core.Entities.Concrete;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
}
