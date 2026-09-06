using System.Net;
using OnIT.Smtp.Core.Configuration;
using Xunit;

namespace OnIT.Smtp.Core.Tests;

public class IpAllowRuleTests
{
    [Fact]
    public void Single_MatchesExactAddressOnly()
    {
        var rule = IpAllowRule.ForSingle("192.168.1.10");

        Assert.True(rule.Matches(IPAddress.Parse("192.168.1.10")));
        Assert.False(rule.Matches(IPAddress.Parse("192.168.1.11")));
    }

    [Theory]
    [InlineData("192.168.1.10", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("192.168.1.15", true)]
    [InlineData("192.168.1.9", false)]
    [InlineData("192.168.1.21", false)]
    public void Range_MatchesInclusiveBounds(string address, bool expected)
    {
        var rule = IpAllowRule.ForRange("192.168.1.10", "192.168.1.20");
        Assert.Equal(expected, rule.Matches(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("192.168.1.254", true)]
    [InlineData("192.168.2.1", false)]
    public void Cidr_MatchesSubnet(string address, bool expected)
    {
        var rule = IpAllowRule.ForCidr("192.168.1.0", 24);
        Assert.Equal(expected, rule.Matches(IPAddress.Parse(address)));
    }

    [Fact]
    public void Disabled_RuleNeverMatches()
    {
        var rule = IpAllowRule.ForSingle("192.168.1.10");
        rule.Enabled = false;

        Assert.False(rule.Matches(IPAddress.Parse("192.168.1.10")));
    }

    [Fact]
    public void Validate_RejectsBadCidrPrefix()
    {
        var rule = IpAllowRule.ForCidr("192.168.1.0", 33);
        Assert.Throws<FormatException>(() => rule.Validate());
    }

    [Fact]
    public void Validate_RejectsRangeWhereStartIsAfterEnd()
    {
        var rule = IpAllowRule.ForRange("192.168.1.20", "192.168.1.10");
        Assert.Throws<FormatException>(() => rule.Validate());
    }
}
