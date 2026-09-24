using System.Net;
using WebAPI.Configuration;

namespace Tests.WebAPI.Configuration;

public sealed class RateLimitPartitionKeyTests
{
    [Fact]
    public void PreservesDistinctIpv6AddressesThatShareTheSameLast32Bits()
    {
        var first = IPAddress.Parse("2001:db8:1::c000:201");
        var second = IPAddress.Parse("2001:db8:2::c000:201");

        Assert.NotEqual(RateLimitPartitionKey.FromAddress(first), RateLimitPartitionKey.FromAddress(second));
    }

    [Fact]
    public void NormalizesIpv4MappedIpv6ToTheEquivalentIpv4Partition()
    {
        var ipv4 = IPAddress.Parse("192.0.2.1");
        var mappedIpv4 = ipv4.MapToIPv6();

        Assert.Equal(RateLimitPartitionKey.FromAddress(ipv4), RateLimitPartitionKey.FromAddress(mappedIpv4));
    }

    [Fact]
    public void UsesStableFallbackWhenRemoteAddressIsMissing()
    {
        Assert.Equal("unknown", RateLimitPartitionKey.FromAddress(null));
    }
}
