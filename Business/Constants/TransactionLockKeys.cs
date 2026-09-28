namespace Business.Constants;

/// <summary>
/// Encodes application transaction locks into disjoint PostgreSQL advisory-lock key ranges.
/// </summary>
public static class TransactionLockKeys
{
    private const long CommunityProductNamespace = 0x434F4D4D00000000L;
    private const long SessionFamilyNamespace = 0x5345535300000000L;
    public const long BootstrapAdminProvisioning = 0x4253545200000001L;

    public static long ForCommunityProduct(int productId) => Create(CommunityProductNamespace, productId);

    public static long ForSessionFamily(int userId) => Create(SessionFamilyNamespace, userId);

    private static long Create(long keyNamespace, int id) => keyNamespace | unchecked((uint)id);
}
