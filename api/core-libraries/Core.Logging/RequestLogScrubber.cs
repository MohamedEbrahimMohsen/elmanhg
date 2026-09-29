using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;

namespace Core.Logging;

public static class RequestLogScrubber
{
    public const string RedactedValue = "[redacted]";

    private const int Ipv4PrefixBits = 24;
    private const int Ipv6PrefixBits = 48;

    public static string Query(HttpRequest? request)
        => request is null || !request.QueryString.HasValue
            ? string.Empty
            : "?" + string.Join("&", request.Query.Keys.Select(key => $"{key}={RedactedValue}"));

    public static string? IpList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : string.Join(", ", value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Ip));

    public static string Ip(string? value)
        => IPAddress.TryParse(value, out var address) ? Ip(address) : string.Empty;

    public static string Ip(IPAddress? address)
    {
        if (address is null)
            return string.Empty;

        var normalised = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        var prefixBits = normalised.AddressFamily == AddressFamily.InterNetwork ? Ipv4PrefixBits : Ipv6PrefixBits;
        var bytes = normalised.GetAddressBytes();
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)(bytes[index] & Mask(prefixBits - (index * 8)));

        return $"{new IPAddress(bytes)}/{prefixBits}";
    }

    private static int Mask(int remainingBits) => remainingBits switch
    {
        >= 8 => 0xFF,
        <= 0 => 0x00,
        _ => 0xFF << (8 - remainingBits),
    };
}
