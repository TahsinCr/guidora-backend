using System.Linq;
using Microsoft.Extensions.Configuration;
using WebAPI.Configuration;

namespace Tests.WebAPI.Configuration;

public sealed class BootstrapAdminSettingsTests
{
    [Fact]
    public void DisabledBootstrapDoesNotRequireCredentials()
    {
        var settings = BootstrapAdminSettings.FromConfiguration(Configuration(
            ("BootstrapAdmin:Enabled", "false")));

        Assert.False(settings.Enabled);
    }

    [Fact]
    public void EnabledBootstrapRequiresWellFormedCredentialsAndLongPassword()
    {
        var configuration = Configuration(
            ("BootstrapAdmin:Enabled", "true"),
            ("BootstrapAdmin:Username", "tahsincr"),
            ("BootstrapAdmin:Email", "tahsincrs@gmail.com"),
            ("BootstrapAdmin:Password", "abcdefghijklmnopqrstuvwxyzABCDEF"));

        var settings = BootstrapAdminSettings.FromConfiguration(configuration);

        Assert.True(settings.Enabled);
        Assert.Equal("tahsincr", settings.Username);
    }

    [Theory]
    [InlineData("not-an-email", "abcdefghijklmnopqrstuvwxyzABCDEF")]
    [InlineData("tahsincrs@gmail.com", "too-short")]
    public void EnabledBootstrapRejectsInvalidEmailOrShortPassword(string email, string password)
    {
        var configuration = Configuration(
            ("BootstrapAdmin:Enabled", "true"),
            ("BootstrapAdmin:Username", "tahsincr"),
            ("BootstrapAdmin:Email", email),
            ("BootstrapAdmin:Password", password));

        Assert.Throws<System.InvalidOperationException>(() =>
            BootstrapAdminSettings.FromConfiguration(configuration));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();
}
