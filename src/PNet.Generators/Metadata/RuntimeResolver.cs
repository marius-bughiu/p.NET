using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNet.Generators.Metadata;

/// <summary>
/// Finds the implementation DLLs of the running .NET runtime so we can read their full
/// metadata (including private members) instead of the compile-time ref-pack, which strips them.
/// </summary>
internal static class RuntimeResolver
{
    /// <summary>
    /// Returns the set of runtime impl DLLs (e.g. System.Private.CoreLib.dll) from the .NET
    /// runtime that's currently executing. Paths to implementation assemblies only — never
    /// reference assemblies.
    /// </summary>
    public static IReadOnlyList<string> GetRuntimeImplAssemblies()
    {
        var coreLibPath = typeof(object).Assembly.Location;
        if (string.IsNullOrEmpty(coreLibPath) || !File.Exists(coreLibPath))
            return Array.Empty<string>();

        var runtimeDir = Path.GetDirectoryName(coreLibPath)!;
        // Grab every DLL next to CoreLib. This is the shared framework folder, e.g.
        // C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.5\
        return Directory.GetFiles(runtimeDir, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(p => !IsLikelyNativeHost(p))
            .ToList();
    }

    private static bool IsLikelyNativeHost(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Equals("hostfxr", StringComparison.OrdinalIgnoreCase)
            || name.Equals("hostpolicy", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ucrtbase", StringComparison.OrdinalIgnoreCase);
    }
}
