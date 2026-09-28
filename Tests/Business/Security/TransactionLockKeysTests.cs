using Business.Constants;

namespace Tests.Business.Security;

public sealed class TransactionLockKeysTests
{
    [Fact]
    public void CommunityAndSessionLocksUseDisjointNamespacedKeysAcrossIntIdRange()
    {
        foreach (var id in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            Assert.NotEqual(TransactionLockKeys.ForCommunityProduct(id),
                TransactionLockKeys.ForSessionFamily(id));
        }

        Assert.NotEqual(TransactionLockKeys.ForCommunityProduct(int.MaxValue),
            TransactionLockKeys.ForSessionFamily(int.MinValue));
        Assert.NotEqual(TransactionLockKeys.ForSessionFamily(int.MaxValue),
            TransactionLockKeys.ForCommunityProduct(int.MinValue));
    }
}
