using System.Net;
using System.Net.Sockets;

namespace OnIT.Smtp.Core.Configuration;

public enum IpAllowRuleKind
{
    Single,
    Range,
    Cidr
}

/// <summary>
/// A single entry in the SMTP client IP allow list. Supports a single address,
/// an inclusive start-end range, or a CIDR subnet. Only clients matching at
/// least one enabled rule may relay mail.
/// </summary>
public sealed class IpAllowRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Description { get; set; } = string.Empty;
    public IpAllowRuleKind Kind { get; set; } = IpAllowRuleKind.Single;
    public bool Enabled { get; set; } = true;

    /// <summary>Used by Single and Cidr (network address) and Range (start).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>CIDR prefix length, e.g. 24. Only used when Kind == Cidr.</summary>
    public int PrefixLength { get; set; }

    /// <summary>Range end address (inclusive). Only used when Kind == Range.</summary>
    public string? RangeEnd { get; set; }

    public static IpAllowRule ForSingle(string address, string? description = null) => new()
    {
        Kind = IpAllowRuleKind.Single,
        Address = address,
        Description = description ?? string.Empty
    };

    public static IpAllowRule ForRange(string start, string end, string? description = null) => new()
    {
        Kind = IpAllowRuleKind.Range,
        Address = start,
        RangeEnd = end,
        Description = description ?? string.Empty
    };

    public static IpAllowRule ForCidr(string network, int prefixLength, string? description = null) => new()
    {
        Kind = IpAllowRuleKind.Cidr,
        Address = network,
        PrefixLength = prefixLength,
        Description = description ?? string.Empty
    };

    /// <summary>
    /// Validates the rule's textual fields without needing a live IPAddress to test.
    /// Throws <see cref="FormatException"/> with a human-readable message on failure.
    /// </summary>
    public void Validate()
    {
        switch (Kind)
        {
            case IpAllowRuleKind.Single:
                if (!IPAddress.TryParse(Address, out _))
                    throw new FormatException($"'{Address}' is not a valid IP address.");
                break;

            case IpAllowRuleKind.Range:
                if (!IPAddress.TryParse(Address, out var start))
                    throw new FormatException($"'{Address}' is not a valid start IP address.");
                if (RangeEnd is null || !IPAddress.TryParse(RangeEnd, out var end))
                    throw new FormatException($"'{RangeEnd}' is not a valid end IP address.");
                if (start.AddressFamily != end.AddressFamily)
                    throw new FormatException("Range start and end must be the same IP family (both IPv4 or both IPv6).");
                if (CompareAddresses(start, end) > 0)
                    throw new FormatException("Range start must be less than or equal to range end.");
                break;

            case IpAllowRuleKind.Cidr:
                if (!IPAddress.TryParse(Address, out var network))
                    throw new FormatException($"'{Address}' is not a valid network address.");
                var maxPrefix = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                if (PrefixLength < 0 || PrefixLength > maxPrefix)
                    throw new FormatException($"Prefix length must be between 0 and {maxPrefix} for {network.AddressFamily}.");
                break;

            default:
                throw new FormatException($"Unknown rule kind '{Kind}'.");
        }
    }

    /// <summary>Returns true if the given client address is covered by this rule.</summary>
    public bool Matches(IPAddress client)
    {
        if (!Enabled) return false;

        try
        {
            switch (Kind)
            {
                case IpAllowRuleKind.Single:
                    return IPAddress.TryParse(Address, out var single) && single.Equals(Normalize(client));

                case IpAllowRuleKind.Range:
                    if (!IPAddress.TryParse(Address, out var start) || !IPAddress.TryParse(RangeEnd, out var end))
                        return false;
                    var normalizedClient = Normalize(client);
                    if (normalizedClient.AddressFamily != start.AddressFamily) return false;
                    return CompareAddresses(normalizedClient, start) >= 0 && CompareAddresses(normalizedClient, end) <= 0;

                case IpAllowRuleKind.Cidr:
                    if (!IPAddress.TryParse(Address, out var network)) return false;
                    return IsInSubnet(Normalize(client), network, PrefixLength);

                default:
                    return false;
            }
        }
        catch
        {
            // A malformed rule should never crash the listener; treat as non-matching.
            return false;
        }
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static int CompareAddresses(IPAddress a, IPAddress b)
    {
        var aBytes = a.GetAddressBytes();
        var bBytes = b.GetAddressBytes();
        if (aBytes.Length != bBytes.Length)
            throw new FormatException("Cannot compare IPv4 and IPv6 addresses.");

        for (var i = 0; i < aBytes.Length; i++)
        {
            var cmp = aBytes[i].CompareTo(bBytes[i]);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    private static bool IsInSubnet(IPAddress client, IPAddress network, int prefixLength)
    {
        if (client.AddressFamily != network.AddressFamily) return false;

        var clientBytes = client.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainderBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (clientBytes[i] != networkBytes[i]) return false;
        }

        if (remainderBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainderBits));
        return (clientBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    public override string ToString() => Kind switch
    {
        IpAllowRuleKind.Single => Address,
        IpAllowRuleKind.Range => $"{Address} - {RangeEnd}",
        IpAllowRuleKind.Cidr => $"{Address}/{PrefixLength}",
        _ => "?"
    };
}
