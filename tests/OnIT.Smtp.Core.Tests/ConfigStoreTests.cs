using OnIT.Smtp.Core.Configuration;
using Xunit;

namespace OnIT.Smtp.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"onit-smtp-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void RoundTrip_PreservesAllSections()
    {
        var store = new ConfigStore(_path);
        var config = new AppConfiguration();
        config.EntraApp.TenantId = "contoso.onmicrosoft.com";
        config.EntraApp.ApplicationId = "11111111-1111-1111-1111-111111111111";
        config.IpAllowRules.Add(IpAllowRule.ForCidr("192.168.1.0", 24, "office LAN"));
        config.AllowedSenders.Add(new AllowedSender { UserPrincipalName = "svc@contoso.com", DisplayName = "Service Account" });
        config.SmtpListener.Port = 2525;
        config.Logging.Verbosity = LogVerbosity.Detailed;

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(config.EntraApp.TenantId, loaded.EntraApp.TenantId);
        Assert.Single(loaded.IpAllowRules);
        Assert.Equal(IpAllowRuleKind.Cidr, loaded.IpAllowRules[0].Kind);
        Assert.Single(loaded.AllowedSenders);
        Assert.Equal(2525, loaded.SmtpListener.Port);
        Assert.Equal(LogVerbosity.Detailed, loaded.Logging.Verbosity);
    }

    [Fact]
    public void Load_ReturnsDefaultWhenFileMissing()
    {
        var store = new ConfigStore(_path);
        var config = store.Load();

        Assert.NotNull(config);
        Assert.False(config.EntraApp.IsConfigured);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
