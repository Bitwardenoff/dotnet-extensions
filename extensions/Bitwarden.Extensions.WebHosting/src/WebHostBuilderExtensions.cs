﻿using Bitwarden.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Bitwarden.Extensions.WebHosting;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseBitwardenWebDefaults(this IWebHostBuilder webHostBuilder, Action<BitwardenWebHostOptions>? configure = null)
    {
        var bitwardenWebHostOptions = new BitwardenWebHostOptions();
        configure?.Invoke(bitwardenWebHostOptions);
        return webHostBuilder.UseBitwardenWebDefaults(bitwardenWebHostOptions);
    }

    public static IWebHostBuilder UseBitwardenWebDefaults(this IWebHostBuilder webHostBuilder, BitwardenWebHostOptions bitwardenWebHostOptions)
    {
        // TODO: Add services and default starting middleware
        webHostBuilder.Configure((context, builder) =>
        {
            if (bitwardenWebHostOptions.IncludeRequestLogging)
            {
                builder.UseSerilogRequestLogging();
            }

            // Exception handling middleware?
        });

        webHostBuilder.ConfigureServices(static (context, services) =>
        {
            // Default services that are web specific?
        });

        return webHostBuilder;
    }

    public static IHostBuilder UseBitwardenWebDefaults<TStartup>(this IHostBuilder hostBuilder, Action<BitwardenWebHostOptions>? configure = null)
        where TStartup : class
    {
        var bitwardenWebHostOptions = new BitwardenWebHostOptions();
        configure?.Invoke(bitwardenWebHostOptions);

        hostBuilder.UseBitwardenDefaults(bitwardenWebHostOptions);
        return hostBuilder.ConfigureWebHostDefaults(webHost =>
        {
            // Make sure to call our thing first, so that if we add middleware it is first
            webHost.UseBitwardenWebDefaults(bitwardenWebHostOptions);
            webHost.UseStartup<TStartup>();
        });
    }
}
