﻿using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Bitwarden.Extensions.Hosting;

/// <summary>
/// Extensions for <see cref="IHostBuilder"/>.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Gets a logger that is suitable for use during the bootstrapping (startup) process.
    /// </summary>
    /// <returns></returns>
    public static ILogger GetBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Configures the host to use Bitwarden defaults.
    /// </summary>
    public static IHostBuilder UseBitwardenDefaults(this IHostBuilder hostBuilder, Action<BitwardenHostOptions>? configure = null)
    {
        // We could default to not including logging in development environments like we currently do.
        var bitwardenHostOptions = new BitwardenHostOptions();
        configure?.Invoke(bitwardenHostOptions);
        return hostBuilder.UseBitwardenDefaults(bitwardenHostOptions);
    }

    /// <summary>
    /// Configures the host to use Bitwarden defaults.
    /// </summary>
    /// <param name="hostBuilder">Host builder.</param>
    /// <param name="bitwardenHostOptions">Host options.</param>
    /// <returns></returns>
    public static IHostBuilder UseBitwardenDefaults(this IHostBuilder hostBuilder, BitwardenHostOptions bitwardenHostOptions)
    {
        hostBuilder.ConfigureServices((_, services) =>
        {
            services.AddOptions<GlobalSettingsBase>()
                .Configure<IConfiguration>((options, config) =>
                {
                    options.IsSelfHosted = config.GetValue("globalSettings:selfHosted", false);
                });
        });

        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            if (context.Configuration.GetValue("globalSettings:selfHosted", false))
            {
                // Current ordering of Configuration:
                // 1. Chained (from Host config)
                //      1. Memory
                //      2. Memory
                //      3. Environment (DOTNET_)
                //      4. Chained
                //          1. Memory
                //          2. Environment (ASPNETCORE_)
                // 2. Json (appsettings.json)
                // 3. Json (appsettings.Environment.json)
                // 4. Secrets
                // 5. Environment (*)
                // 6. Command line args, if present
                // vv If selfhosted vv
                // 7. Json (appsettings.json) again
                // 8. Json (appsettings.Environment.json)
                // 9. Secrets (if development)
                // 10. Environment (*)
                // 11. Command line args, if present

                // As you can see there was a lot of doubling up,
                // I would rather insert the self hosted config, when necessary into
                // the index.

                // These would fail if two main things happen, the default host setup from .NET changes
                // and a new source is added before the appsettings ones.
                // or someone change the order or adding this helper, both things I believe would be quickly
                // discovered during development.

                // I expect the 3rd source to be the main appsettings.json file
                Debug.Assert(builder.Sources[2] is FileConfigurationSource mainJsonSource
                    && mainJsonSource.Path == "appsettings.json");
                // I expect the 4th source to be the environment specific json file
                Debug.Assert(builder.Sources[3] is FileConfigurationSource environmentJsonSource
                    && environmentJsonSource.Path == $"appsettings.{context.HostingEnvironment.EnvironmentName}.json");

                // If both of those are true, I feel good about inserting our own self hosted config after
                builder.Sources.Insert(4, new JsonConfigurationSource
                {
                    Path = "appsettings.SelfHosted.json",
                    Optional = true,
                    ReloadOnChange = true
                });

                if (context.HostingEnvironment.IsDevelopment())
                {
                    var appAssembly = Assembly.Load(new AssemblyName(context.HostingEnvironment.ApplicationName));
                    builder.AddUserSecrets(appAssembly, optional: true);
                }

                builder.AddEnvironmentVariables();
            }
        });

        if (bitwardenHostOptions.IncludeLogging)
        {
            hostBuilder.UseSerilog((context, services, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.WithProperty("Project", context.HostingEnvironment.ApplicationName)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new RenderedCompactJsonFormatter());
            });
        }

        if (bitwardenHostOptions.IncludeMetrics)
        {
            hostBuilder.ConfigureServices((_, services) =>
            {
                services.AddOpenTelemetry()
                    .WithMetrics(options =>
                        options.AddOtlpExporter())
                    .WithTracing(options =>
                        options.AddOtlpExporter());
            });
        }

        return hostBuilder;
    }
}
