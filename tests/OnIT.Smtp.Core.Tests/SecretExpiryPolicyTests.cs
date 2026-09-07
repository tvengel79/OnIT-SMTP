using OnIT.Smtp.Core.Entra;
using Xunit;

namespace OnIT.Smtp.Core.Tests;

public class SecretExpiryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoExpiryDate_ReturnsNoThresholds()
    {
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(null, Now, Array.Empty<int>());
        Assert.Empty(result);
    }

    [Fact]
    public void FarFromExpiry_ReturnsNoThresholds()
    {
        var expiresOn = Now.AddDays(90);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, Array.Empty<int>());
        Assert.Empty(result);
    }

    [Fact]
    public void WithinThirtyDays_ReturnsThirtyDayThresholdOnly()
    {
        var expiresOn = Now.AddDays(25);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, Array.Empty<int>());
        Assert.Equal(new[] { 30 }, result);
    }

    [Fact]
    public void AlreadyNotifiedThreshold_IsNotReturnedAgain()
    {
        var expiresOn = Now.AddDays(25);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, new[] { 30 });
        Assert.Empty(result);
    }

    [Fact]
    public void MissedChecks_ReturnsAllNewlyCrossedThresholdsAtOnce()
    {
        // Service was off for a while; expiry is now only 5 days out and nothing was ever notified.
        var expiresOn = Now.AddDays(5);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, Array.Empty<int>());
        Assert.Equal(new[] { 30, 15, 7 }, result);
    }

    [Fact]
    public void Expired_ReturnsZeroThreshold()
    {
        var expiresOn = Now.AddDays(-2);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, new[] { 30, 15, 7, 3 });
        Assert.Equal(new[] { 0 }, result);
    }

    [Fact]
    public void AllThresholdsAlreadyNotified_ReturnsNothingEvenWhenExpired()
    {
        var expiresOn = Now.AddDays(-10);
        var result = SecretExpiryPolicy.GetNewlyCrossedThresholds(expiresOn, Now, new[] { 30, 15, 7, 3, 0 });
        Assert.Empty(result);
    }
}
