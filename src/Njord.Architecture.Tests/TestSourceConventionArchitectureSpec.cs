namespace Njord.Architecture.Tests;

public sealed class TestSourceConventionArchitectureSpec
{
    // (rule, file or file#method, reason). Entries are exemptions for locations that legitimately break a rule;
    // an entry that no longer matches an offender fails Allow_list_entries_still_match_an_offender.
    private const string EqualityRule = "no boolean assert around an equality";

    private const string OperatorReason = "pins the == operator of the value type, which Assert.Equal does not call";

    private static readonly (string Rule, string Key, string Reason)[] AllowList =
    [
        (EqualityRule, "Njord.Model.Tests/HorizonSpec.cs", OperatorReason),
        (EqualityRule, "Njord.Model.Tests/LocationIdSpec.cs", OperatorReason),
    ];

    public static TheoryData<string> RuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var rule in TestSourceRule.All)
        {
            data.Add(rule.Name);
        }

        return data;
    }

    private static TestSourceRule Rule(string name) => TestSourceRule.All.Single(r => r.Name == name);

    private static List<SourceHit> Offenders(TestSourceRule rule, IEnumerable<SourceFile> files)
        => [.. files.SelectMany(rule.Find)];

    [Theory]
    [MemberData(nameof(RuleNames))]
    public void Spec_files_follow_the_test_source_convention(string ruleName)
    {
        var rule = Rule(ruleName);

        var offenders = Offenders(rule, TestSourceScanner.SpecFiles())
            .Where(hit => !AllowList.Any(a => a.Rule == ruleName && a.Key == hit.Key))
            .Select(hit => $"{hit.Path}:{hit.Line} {hit.Detail}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(string.Empty, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Allow_list_entries_still_match_an_offender()
    {
        var files = TestSourceScanner.SpecFiles();

        var stale = AllowList
            .Where(entry => !Offenders(Rule(entry.Rule), files).Any(hit => hit.Key == entry.Key))
            .Select(entry => $"{entry.Rule}: {entry.Key}")
            .ToList();

        Assert.Empty(stale);
    }

    [Fact]
    public void Every_allow_list_entry_names_a_known_rule_and_a_reason()
    {
        var invalid = AllowList
            .Where(entry => TestSourceRule.All.All(r => r.Name != entry.Rule) || string.IsNullOrWhiteSpace(entry.Reason))
            .Select(entry => entry.Key)
            .ToList();

        Assert.Empty(invalid);
    }

    [Fact]
    public void The_scanner_finds_spec_files_in_every_test_project()
    {
        var files = TestSourceScanner.SpecFiles();

        Assert.Multiple(
            () => Assert.Contains(files, f => f.Path == "Njord.Budget.Tests/BudgetTrackerSpec.cs"),
            () => Assert.Contains(files, f => f.Path == "Njord.Architecture.Tests/TestSourceConventionArchitectureSpec.cs"),
            () => Assert.DoesNotContain(files, f => f.Path.Contains("/obj/") || f.Path.StartsWith("Njord.Tests.Shared/", StringComparison.Ordinal)));
    }

    [Fact]
    public void Options_with_shipped_defaults_include_the_section_options_and_exclude_nested_types()
    {
        var types = TestSourceRule.OptionsWithShippedDefaults;

        Assert.Multiple(
            () => Assert.Contains("PollingOptions", types),
            () => Assert.Contains("MqttOptions", types),
            () => Assert.Contains("IndexOptions", types),
            () => Assert.DoesNotContain("LocationOptions", types));
    }

    // Negative self-tests: each convention must be detected on a synthetic offending snippet.
    public static TheoryData<string, string> OffendingSnippets() => new()
    {
        { "async test has an explicit Timeout", "public sealed class XSpec { [Fact] public async Task Runs() { await Task.Yield(); } }" },
        { "async test has an explicit Timeout", "public sealed class XSpec { [Theory(DisplayName = \"x\")] [InlineData(1)] public async Task Runs(int i) { await Task.Yield(); } }" },
        { "no Thread.Sleep", "void M() { Thread.Sleep(10); }" },
        { "no Task.Delay with a literal duration", "async Task M() { await Task.Delay(100); }" },
        { "no Task.Delay with a literal duration", "async Task M() { await Task.Delay(TimeSpan.FromSeconds(1)); }" },
        { "no wall clock", "var now = DateTimeOffset.UtcNow;" },
        { "no wall clock", "var now = DateTime.Now;" },
        { "no empty options constructor for options with shipped defaults", "var o = new PollingOptions();" },
        { "no empty options constructor for options with shipped defaults", "var o = new MqttOptions { };" },
        { "no boolean assert around an equality", "Assert.True(actual == expected);" },
        { "no boolean assert around an equality", "Assert.False(actual != expected);" },
        { "no null-forgiving member access outside JsonNode", "var x = value.ToString()!.Length;" },
        { "no null-forgiving member access outside JsonNode", "var x = Find()!.Name;" },
    };

    [Theory]
    [MemberData(nameof(OffendingSnippets))]
    public void The_scanner_detects_an_offending_snippet(string ruleName, string snippet)
    {
        var hits = Rule(ruleName).Find(Synthetic(snippet)).ToList();

        Assert.NotEmpty(hits);
    }

    public static TheoryData<string, string> CompliantSnippets() => new()
    {
        { "async test has an explicit Timeout", "[Fact(Timeout = TestTimeouts.Hosted)] public async Task Runs() { }" },
        { "async test has an explicit Timeout", "[Fact] public void Runs() { }" },
        { "async test has an explicit Timeout", "[Fact] public Task Verifies() => Verify(1);" },
        { "no Thread.Sleep", "var s = \"Thread.Sleep(1)\"; // Thread.Sleep(2)" },
        { "no Task.Delay with a literal duration", "await Task.Delay(Timeout.Infinite, ct);" },
        { "no wall clock", "var s = \"DateTime.UtcNow\"; /* DateTime.Now */ var t = time.GetUtcNow();" },
        { "no empty options constructor for options with shipped defaults", "var o = new PollingOptions { PollInterval = TimeSpan.Zero };" },
        { "no empty options constructor for options with shipped defaults", "var l = new LocationOptions();" },
        { "no boolean assert around an equality", "Assert.True(list.All(x => x == 1)); Assert.True(a <= b); Assert.False(a == b);" },
        { "no boolean assert around an equality", "Assert.True(items.Count == 0, \"a message has no Assert.Equal overload\");" },
        { "no null-forgiving member access outside JsonNode", "var v = node[\"a\"]!.GetValue<int>(); var r = JsonNode.Parse(text)!.AsObject();" },
    };

    [Theory]
    [MemberData(nameof(CompliantSnippets))]
    public void The_scanner_accepts_a_compliant_snippet(string ruleName, string snippet)
    {
        var hits = Rule(ruleName).Find(Synthetic(snippet)).ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void Stripping_blanks_comments_and_literals_but_keeps_offsets_and_lines()
    {
        const string source = "var a = \"x // y\"; // c\nvar b = $@\"p\"\"q\"; /* m\nn */ var c = 'z';\nvar d = \"\"\"\nraw quoted\n\"\"\";";

        var code = TestSourceScanner.Strip(source);

        Assert.Multiple(
            () => Assert.Equal(source.Length, code.Length),
            () => Assert.Equal(source.Count(ch => ch == '\n'), code.Count(ch => ch == '\n')),
            () => Assert.DoesNotContain("//", code),
            () => Assert.DoesNotContain("quoted", code),
            () => Assert.Contains("var a", code),
            () => Assert.Contains("var c", code),
            () => Assert.Equal(3, TestSourceScanner.LineOf(code, code.IndexOf("var c", StringComparison.Ordinal))));
    }

    private static SourceFile Synthetic(string text) => new("Synthetic/XSpec.cs", text, TestSourceScanner.Strip(text));
}
