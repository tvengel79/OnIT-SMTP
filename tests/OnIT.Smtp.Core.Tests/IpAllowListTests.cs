using System.Net;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Networking;
using Xunit;

namespace OnIT.Smtp.Core.Tests;

public class IpAllowListTests
{
    [Fact]
    public void EmptyList_DeniesEverything()
    {
        var list = new IpAllowList(Array.Empty<IpAllowRule>());
        Assert.False(list.IsAllowed(IPAddress.Parse("10.0.0.1")));
    }

    [Fact]
    public void FirstMatchingRuleWins()
    {
        var rules = new[]
        {
            IpAllowRule.ForCidr("10.0.0.0", 8, "corp net"),
            IpAllowRule.ForSingle("10.0.0.5", "specific host")
        };

        var list = new IpAllowList(rules);
        Assert.True(list.IsAllowed(IPAddress.Parse("10.0.0.5"), out var matched));
        Assert.Equal("corp net", matched!.Description);
    }
}
