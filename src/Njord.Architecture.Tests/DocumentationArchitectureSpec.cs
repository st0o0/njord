using System.Reflection;
using System.Text.RegularExpressions;
using Xunit.Sdk;
using Njord.Tests.Shared;
using Xunit.v3;

namespace Njord.Architecture.Tests;

// Keeps AGENTS.md and the project skills honest: every file, spec and method they name exists, and the documented
// test counts equal what the test assemblies discover (one test per [Fact], one per data row of a [Theory]).
public sealed partial class DocumentationArchitectureSpec
{
    private static readonly string[] PathRoots = ["src/", ".claude/", "e2e/", "openspec/", "scripts/", "docs/", "protos/", "Njord"];

    private static readonly string[] FileExtensions = [".cs", ".md", ".json", ".yml", ".yaml", ".proto", ".csproj", ".props", ".slnx"];

    // Names of methods that live in a framework or are placeholders in the documents.
    private static readonly string[] ExternalMethods =
    [
        "AddXxx", "CreateTestProbe", "ToString", "ToTimestamp",
    ];

    // Paths that exist at runtime (container volume, operator machine), not in the repository.
    private static readonly string[] RuntimePaths = ["data/appsettings.Override.json"];

    [GeneratedRegex("`([^`\\n]+)`")]
    private static partial Regex Backticked();

    [GeneratedRegex(@"Current total:\s*(?<total>\d+)\s+tests\s*\((?<list>[^)]*)\)")]
    private static partial Regex DocumentedTotal();

    [GeneratedRegex(@"(?<name>[A-Za-z]+)\s+(?<count>\d+)")]
    private static partial Regex ProjectCount();

    private static string RepoRoot => Path.GetFullPath(Path.Combine(TestSourceScanner.FindSrcRoot(), ".."));

    private static IEnumerable<(string Document, string Token)> Tokens()
    {
        var documents = new List<string> { Path.Combine(RepoRoot, "AGENTS.md") };
        var skills = Path.Combine(RepoRoot, ".claude", "skills");
        documents.AddRange(Directory.GetDirectories(skills)
            .Where(d => Path.GetFileName(d) is "e2e-test" || Path.GetFileName(d).StartsWith("njord-", StringComparison.Ordinal))
            .Select(d => Path.Combine(d, "SKILL.md")));

        foreach (var document in documents)
        {
            var name = Path.GetRelativePath(RepoRoot, document).Replace('\\', '/');
            foreach (Match match in Backticked().Matches(File.ReadAllText(document)))
            {
                yield return (name, match.Groups[1].Value.Trim());
            }
        }
    }

    private static bool IsConcretePath(string token)
        => token.IndexOfAny(['<', '>', '*', '{', '}', ' ', ':', '|', '$', '(', ')']) < 0
           && !token.StartsWith('/') && !token.StartsWith("..", StringComparison.Ordinal) && !token.Contains("...", StringComparison.Ordinal)
           && (PathRoots.Any(r => token.StartsWith(r, StringComparison.Ordinal))
               || FileExtensions.Any(e => token.EndsWith(e, StringComparison.Ordinal)))
           && (token.Contains('/') || FileExtensions.Any(e => token.EndsWith(e, StringComparison.Ordinal)));

    private static bool PathExists(string token)
    {
        var relative = token.TrimEnd('/');
        var roots = new[] { RepoRoot, TestSourceScanner.FindSrcRoot() };
        if (roots.Any(root => PathOrSource(Path.Combine(root, relative))))
        {
            return true;
        }

        // A bare file name is found anywhere in the repository; a path under src/ may omit the extension of a class.
        return !relative.Contains('/') && FindByName(relative);
    }

    private static bool PathOrSource(string full)
        => File.Exists(full) || Directory.Exists(full) || File.Exists(full + ".cs");

    private static bool FindByName(string fileName)
        => Directory.EnumerateFiles(RepoRoot, fileName, SearchOption.AllDirectories)
            .Any(f => !f.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj" or "node_modules" or ".git"));

    [Fact]
    public void Files_and_directories_named_in_the_documents_exist()
    {
        var missing = Tokens()
            .Where(t => IsConcretePath(t.Token) && !RuntimePaths.Contains(t.Token) && !PathExists(t.Token))
            .Select(t => $"{t.Document}: {t.Token}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, ["", .. missing]));
    }

    [Fact]
    public void Specs_named_in_the_documents_exist_as_test_types()
    {
        var specTypes = NjordArchitecture.TestAssemblies
            .SelectMany(a => a.GetTypes())
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Tokens()
            .Where(t => SpecName().IsMatch(t.Token))
            .Where(t => !specTypes.Contains(t.Token.Replace(".cs", string.Empty, StringComparison.Ordinal)))
            .Select(t => $"{t.Document}: {t.Token}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, ["", .. missing]));
    }

    [GeneratedRegex(@"^[A-Z]\w*Spec(\.cs)?$")]
    private static partial Regex SpecName();

    [GeneratedRegex(@"^[A-Z]\w*\(\)$")]
    private static partial Regex MethodReference();

    [Fact]
    public void Methods_named_in_the_documents_exist()
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var methods = NjordArchitecture.ProductionAssemblies.Concat(NjordArchitecture.TestAssemblies)
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(All))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = MissingMethods(Tokens(), methods);

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, ["", .. missing]));
    }

    private static List<string> MissingMethods(IEnumerable<(string Document, string Token)> tokens, ISet<string> methods)
        => [.. tokens
            .Where(t => MethodReference().IsMatch(t.Token))
            .Select(t => (t.Document, Name: t.Token[..^2]))
            .Where(t => !ExternalMethods.Contains(t.Name) && !methods.Contains(t.Name))
            .Select(t => $"{t.Document}: {t.Name}()")
            .Distinct()
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void A_reference_to_a_missing_helper_is_reported()
    {
        var missing = MissingMethods([("AGENTS.md", "AddFastRetryBackoff()"), ("AGENTS.md", "ToProto()")], new HashSet<string> { "ToProto" });

        Assert.Equal(["AGENTS.md: AddFastRetryBackoff()"], missing);
    }

    [Fact]
    public void A_stale_documented_count_is_reported_with_the_discovered_value()
    {
        var discovered = new SortedDictionary<string, int> { ["Njord.Core.Tests"] = 10, ["Njord.Tests"] = 5 };
        var documented = new SortedDictionary<string, int> { ["Njord.Core.Tests"] = 9, ["Njord.Tests"] = 5 };

        var differences = Differences(ExpectedLine(discovered), ExpectedLine(documented, 14));

        Assert.Equal(["discovered total 15", "discovered Core 10", "documented total 14", "documented Core 9"], differences);
    }

    [Theory]
    [InlineData("src/Njord.Grpc/EnrichmentProtoMapper.cs", true)]
    [InlineData("src/Njord.Core/Actors/ActorKeys.cs", true)]
    [InlineData("src/Njord.<Feature>/Configuration/", false)]
    [InlineData("/healthz", false)]
    [InlineData("curl http://localhost:8080/alive", false)]
    [InlineData("Njord.Tests.Shared", false)]
    public void Only_concrete_repository_paths_are_checked(string token, bool concrete)
    {
        Assert.Equal(concrete, IsConcretePath(token));
    }

    [Fact]
    public void A_path_that_does_not_exist_is_not_found()
    {
        Assert.Multiple(
            () => Assert.False(PathExists("src/Njord.Grpc/EnrichmentProtoMapper.cs")),
            () => Assert.True(PathExists("src/Njord.Core/Actors/ActorKeys.cs")),
            () => Assert.True(PathExists("AGENTS.md")));
    }

    [Fact]
    public void The_documented_external_methods_are_still_referenced()
    {
        var referenced = Tokens()
            .Where(t => MethodReference().IsMatch(t.Token))
            .Select(t => t.Token[..^2])
            .ToHashSet(StringComparer.Ordinal);

        var unused = ExternalMethods.Where(name => !referenced.Contains(name)).ToList();

        Assert.Empty(unused);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_documented_test_counts_equal_the_discovered_tests()
    {
        var ct = TestContext.Current.CancellationToken;
        var discovered = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var assembly in NjordArchitecture.TestAssemblies.Where(a => a.GetName().Name != "Njord.Tests.Shared"))
        {
            discovered[assembly.GetName().Name!] = await CountTests(assembly, ct);
        }

        var expected = ExpectedLine(discovered);
        var text = File.ReadAllText(Path.Combine(RepoRoot, "AGENTS.md"));
        var match = DocumentedTotal().Match(text);

        Assert.True(match.Success, "AGENTS.md has no 'Current total: N tests (...)' line. Expected: " + string.Join(", ", expected));
        var documented = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (Match pair in ProjectCount().Matches(match.Groups["list"].Value.Replace("host `Njord.Tests`", "Host", StringComparison.Ordinal)))
        {
            var name = pair.Groups["name"].Value;
            documented[name switch
            {
                "Host" => "Njord.Tests",
                "IntegrationTests" => "Njord.IntegrationTests",
                _ => $"Njord.{name}.Tests",
            }] = int.Parse(pair.Groups["count"].Value);
        }

        var differences = Differences(expected, ExpectedLine(documented, int.Parse(match.Groups["total"].Value)));
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, ["", .. differences]));
    }

    private static List<string> Differences(List<string> discovered, List<string> documented)
        => [.. discovered.Except(documented).Select(e => $"discovered {e}"), .. documented.Except(discovered).Select(s => $"documented {s}")];

    // The total followed by one entry per project, sorted by assembly name.
    private static List<string> ExpectedLine(IReadOnlyDictionary<string, int> counts, int? total = null)
        => [$"total {total ?? counts.Values.Sum()}", .. counts.Select(kv => $"{Label(kv.Key)} {kv.Value}")];

    private static string Label(string assembly)
        => assembly switch
        {
            "Njord.Tests" => "host `Njord.Tests`",
            "Njord.IntegrationTests" => "IntegrationTests",
            _ => assembly["Njord.".Length..^".Tests".Length],
        };

    private static async Task<int> CountTests(Assembly assembly, CancellationToken ct)
    {
        // Inherited test methods count once per concrete class, as the runner does.
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var count = 0;
        foreach (var method in assembly.GetTypes().Where(t => !t.IsAbstract).SelectMany(t => t.GetMethods(All)))
        {
            ct.ThrowIfCancellationRequested();
            if (method.GetCustomAttribute<FactAttribute>() is not { } fact)
            {
                continue;
            }

            if (fact is not TheoryAttribute)
            {
                count++;
                continue;
            }

            var rows = 0;
            foreach (var data in method.GetCustomAttributes().OfType<IDataAttribute>())
            {
                var found = (await data.GetData(method, new DisposalTracker())).Count;
                rows += found > 0 || data is not MemberDataAttributeBase member ? found : MemberRows(method, member);
            }

            count += Math.Max(rows, 1);
        }

        return count;
    }

    // The runner enumerates member data at execution time when it cannot be serialized for discovery.
    private static int MemberRows(MethodInfo method, MemberDataAttributeBase member)
    {
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var owner = member.MemberType ?? method.DeclaringType!;
        object? value = null;
        if (owner.GetProperty(member.MemberName, Static) is { } property)
        {
            value = property.GetValue(null);
        }
        else if (owner.GetField(member.MemberName, Static) is { } field)
        {
            value = field.GetValue(null);
        }
        else if (owner.GetMethod(member.MemberName, Static) is { } source)
        {
            value = source.Invoke(null, member.Arguments);
        }

        return value is System.Collections.IEnumerable rows ? rows.Cast<object?>().Count() : 0;
    }
}
