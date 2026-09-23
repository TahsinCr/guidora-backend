using System.Reflection;
using Business.BusinessAspect.Autofac;
using Business.Security;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class SecurityOperationToolTests
{
    [Fact]
    public void ResolvesConventionClaimAsKebabCaseClassAndMethod()
    {
        var method = typeof(ProductEditProposalManager).GetMethod(nameof(ProductEditProposalManager.AcceptProposal))!;

        var claim = SecuredOperationAspect.ResolveOperationClaim(typeof(ProductEditProposalManager), method);

        Assert.Equal("product-edit-proposal-manager.accept-proposal", claim);
    }

    [Fact]
    public void DiscoversExplicitAndConventionClaimsAndMethodOverridesClassClaim()
    {
        var claims = SecurityOperationTool.DiscoverOperationClaims(Assembly.GetExecutingAssembly());

        Assert.Contains("catalog.product.publish", claims);
        Assert.Contains("operation-claim-discovery-fixture.get-by-slug", claims);
        Assert.Contains("class.permission", claims);
        Assert.DoesNotContain("ignored.class.permission", claims);
    }

    [Fact]
    public void SyncAddsMissingClaimsOnceAndNeverPrunesExistingCatalogEntries()
    {
        var legacyClaim = new OperationClaim { Id = 1, Name = "legacy.permission" };
        var dal = new OperationClaimDalStub([legacyClaim]);
        var unitOfWork = new UnitOfWorkStub();

        var firstSyncCount = SecurityOperationTool.SyncOperationClaims(dal, unitOfWork, Assembly.GetExecutingAssembly());
        var secondSyncCount = SecurityOperationTool.SyncOperationClaims(dal, unitOfWork, Assembly.GetExecutingAssembly());

        Assert.True(firstSyncCount > 0);
        Assert.Equal(0, secondSyncCount);
        Assert.Contains(dal.GetAll(), claim => claim.Name == "legacy.permission");
        Assert.Equal(firstSyncCount + 1, dal.GetAll().Count);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class OperationClaimDalStub(IEnumerable<OperationClaim>? claims = null)
        : EntityRepositoryStub<OperationClaim>(claims), IOperationClaimDal;
}

[SecuredOperationAspect("ignored.class.permission")]
public sealed class OperationClaimDiscoveryFixture
{
    [SecuredOperationAspect("catalog.product.publish")]
    public void Publish() { }

    [SecuredOperationAspect]
    public void GetBySlug() { }

    [SecuredOperationAspect("class.permission")]
    public void LegacyExplicit() { }
}

public sealed class ProductEditProposalManager
{
    public void AcceptProposal() { }
}
