using Microsoft.Extensions.Configuration;

namespace Core.CrossCuttingConcerns.Caching
{
    public class ConfigurationCachePolicyProvider : ICachePolicyProvider
    {
        private readonly IConfiguration _configuration;

        public ConfigurationCachePolicyProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public int GetDurationMinutes(string profile, int fallbackDurationMinutes)
        {
            var configuredDuration = _configuration.GetValue<int?>(
                $"Caching:Profiles:{profile}:DurationMinutes");
            return configuredDuration is > 0 ? configuredDuration.Value : fallbackDurationMinutes;
        }
    }
}
