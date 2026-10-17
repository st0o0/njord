using System.Text.RegularExpressions;

namespace Njord.Architecture.Tests;

public sealed partial class MqttPayloadArchitectureSpec
{
    private const string PayloadNamespace = "Njord.Mqtt.Payloads";

    private static readonly string[] NodeFreeProjects = ["Njord.Weather", "Njord.Messages", "Njord.Pipeline"];

    [GeneratedRegex(@"\bJson(Object|Node|Array|Value)\b|System\.Text\.Json\.Nodes")]
    private static partial Regex JsonNodeUsage();

    [GeneratedRegex(@"System\.Text\.Json\.Nodes")]
    private static partial Regex JsonNodesNamespace();

    [Fact]
    public void Payload_records_are_sealed_except_for_abstract_bases()
    {
        var mqtt = NjordArchitecture.ProductionAssemblies.Single(a => a.GetName().Name == "Njord.Mqtt");

        var payloadTypes = mqtt.GetTypes()
            .Where(t => t.Namespace == PayloadNamespace)
            .Where(t => !t.Name.StartsWith('<'))
            .ToList();

        var offenders = payloadTypes
            .Where(t => !t.IsSealed && !t.IsAbstract)
            .Select(t => t.FullName ?? t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(payloadTypes);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Njord_Mqtt_builds_payloads_without_json_nodes()
    {
        var offenders = ProductionFiles("Njord.Mqtt")
            .Where(file => JsonNodeUsage().IsMatch(File.ReadAllText(file)))
            .Select(RelativePath)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Weather_Messages_and_Pipeline_do_not_use_System_Text_Json_Nodes()
    {
        var offenders = NodeFreeProjects
            .SelectMany(ProductionFiles)
            .Where(file => JsonNodesNamespace().IsMatch(File.ReadAllText(file)))
            .Select(RelativePath)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> ProductionFiles(string project)
        => Directory.EnumerateFiles(Path.Combine(FindSrcRoot(), project), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));

    private static string RelativePath(string file)
        => Path.GetRelativePath(FindSrcRoot(), file).Replace(Path.DirectorySeparatorChar, '/');

    private static string FindSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Njord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Njord.slnx not found above the test output directory.");
    }
}
