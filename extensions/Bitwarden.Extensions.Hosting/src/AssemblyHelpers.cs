﻿using System.Reflection;

namespace Bitwarden.Extensions.Hosting;

public static class AssemblyHelpers
{
    private const string _gitHashAssemblyKey = "GitHash";

    private static readonly IEnumerable<AssemblyMetadataAttribute> _assemblyMetadataAttributes;
    private static readonly AssemblyInformationalVersionAttribute? _assemblyInformationalVersionAttributes;
    private static string? _version;
    private static string? _gitHash;

    static AssemblyHelpers()
    {
        _assemblyMetadataAttributes = Assembly.GetEntryAssembly()!
            .GetCustomAttributes<AssemblyMetadataAttribute>();
        _assemblyInformationalVersionAttributes = Assembly.GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    }

    public static string? GetVersion()
    {
        if (string.IsNullOrWhiteSpace(_version))
        {
            _version = _assemblyInformationalVersionAttributes?.InformationalVersion;
        }

        return _version;
    }

    public static string? GetGitHash()
    {
        if (string.IsNullOrWhiteSpace(_gitHash))
        {
            try
            {
                _gitHash = _assemblyMetadataAttributes.First(i =>
                    i.Key == _gitHashAssemblyKey).Value;
            }
            catch (Exception)
            {
                // suppress
            }
        }

        return _gitHash;
    }
}
