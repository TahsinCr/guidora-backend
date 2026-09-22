namespace Core.CrossCuttingConcerns.Caching
{
    public interface ICachePolicyProvider
    {
        int GetDurationMinutes(string profile, int fallbackDurationMinutes);
    }
}
