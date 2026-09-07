namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Pure logic for deciding which "expires in N days" notification thresholds should fire,
/// given the secret's expiry date and which thresholds have already been sent. Kept
/// side-effect free and separate from SecretExpiryWorker (which owns the schedule, the
/// actual email send, and persisting the result) so the decision itself is easy to test.
/// </summary>
public static class SecretExpiryPolicy
{
    /// <summary>Days-until-expiry thresholds that trigger a notification, largest first. 0 means "already expired".</summary>
    public static readonly IReadOnlyList<int> ThresholdsDaysDescending = new[] { 30, 15, 7, 3, 0 };

    /// <summary>
    /// Returns thresholds that are newly crossed (days remaining is at or below the threshold)
    /// and not already present in <paramref name="alreadyNotified"/>. Can return more than one
    /// at a time if checks were missed for a while (e.g. the service was stopped).
    /// </summary>
    public static IReadOnlyList<int> GetNewlyCrossedThresholds(
        DateTimeOffset? expiresOn, DateTimeOffset now, IReadOnlyCollection<int> alreadyNotified)
    {
        if (expiresOn is null) return Array.Empty<int>();

        var daysRemaining = (expiresOn.Value - now).TotalDays;
        var crossed = new List<int>();

        foreach (var threshold in ThresholdsDaysDescending)
        {
            if (daysRemaining <= threshold && !alreadyNotified.Contains(threshold))
            {
                crossed.Add(threshold);
            }
        }

        return crossed;
    }
}
