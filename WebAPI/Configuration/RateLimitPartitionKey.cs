using System.Net;

namespace WebAPI.Configuration;

internal static class RateLimitPartitionKey
{
    public static string FromAddress(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
