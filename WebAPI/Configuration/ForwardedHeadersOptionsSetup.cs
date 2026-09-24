using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace WebAPI.Configuration;

public sealed class ForwardedHeadersOptionsSetup(IConfiguration configuration)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var proxyAddress in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(proxyAddress))
            {
                continue;
            }

            if (!IPAddress.TryParse(proxyAddress, out var address))
            {
                throw new InvalidOperationException(
                    $"Forwarded proxy address '{proxyAddress}' is not a valid IP address.");
            }

            options.KnownProxies.Add(address);
        }
    }
}
