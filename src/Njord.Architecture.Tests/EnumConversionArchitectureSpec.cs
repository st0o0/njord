using System.Text.RegularExpressions;

namespace Njord.Architecture.Tests;

public sealed partial class EnumConversionArchitectureSpec
{
    // User-written configuration text (parameter group names in appsettings), not a wire conversion.
    private static readonly string[] AllowList =
    [
        "Njord.Weather/ParameterRegistry.cs",
    ];

    [GeneratedRegex(@"\bEnum\s*\.\s*(Try)?Parse\b|\.ToString\(\s*\)\s*\.\s*To(Lower|Upper)")]
    private static partial Regex NameBasedConversion();

    [Fact]
    public void Production_code_converts_enums_with_explicit_switch_expressions_only()
    {
        var srcRoot = FindSrcRoot();

        var offenders = ProductionFiles(srcRoot)
            .Select(file => (File: Path.GetRelativePath(srcRoot, file).Replace(Path.DirectorySeparatorChar, '/'), Text: System.IO.File.ReadAllText(file)))
            .Where(f => !AllowList.Contains(f.File))
            .Where(f => NameBasedConversion().IsMatch(f.Text))
            .Select(f => f.File)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Allow_list_entries_still_exist_and_still_need_the_exemption()
    {
        var srcRoot = FindSrcRoot();

        var stale = AllowList
            .Where(entry => !System.IO.File.Exists(Path.Combine(srcRoot, entry))
                            || !NameBasedConversion().IsMatch(System.IO.File.ReadAllText(Path.Combine(srcRoot, entry))))
            .ToList();

        Assert.Empty(stale);
    }

    private static IEnumerable<string> ProductionFiles(string srcRoot)
        => Directory.GetDirectories(srcRoot, "Njord*")
            .Where(dir => !Path.GetFileName(dir).EndsWith("Tests", StringComparison.Ordinal)
                          && !Path.GetFileName(dir).EndsWith("Tests.Shared", StringComparison.Ordinal))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file) && !IsGenerated(file));

    private static bool IsBuildOutput(string file)
        => file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static bool IsGenerated(string file)
        => file.EndsWith(".g.cs", StringComparison.Ordinal) || file.EndsWith(".Designer.cs", StringComparison.Ordinal);

    private static string FindSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(Path.Combine(dir.FullName, "Njord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Njord.slnx not found above the test output directory.");
    }
}
