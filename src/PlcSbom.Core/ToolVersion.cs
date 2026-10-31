using System.Reflection;

namespace PlcSbom.Core;

/// <summary>Version of plc-sbom from Directory.Build.props (CLAUDE.md section 11).</summary>
public static class ToolVersion
{
    /// <summary>Informational version, may carry the source commit after '+' (e.g. 0.1.0+a1b2c3d…).</summary>
    public static string Full { get; } =
        typeof(ToolVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ToolVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>SemVer without build metadata (e.g. 0.1.0).</summary>
    public static string Short { get; } = Full.Split('+')[0];
}
