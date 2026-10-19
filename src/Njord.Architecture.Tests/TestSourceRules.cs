using System.Text.RegularExpressions;

namespace Njord.Architecture.Tests;

// One entry per machine-checked test source convention. A rule reports every offending location of one file.
internal sealed partial class TestSourceRule(string name, Func<SourceFile, IEnumerable<SourceHit>> find)
{
    public string Name { get; } = name;

    public IEnumerable<SourceHit> Find(SourceFile file) => find(file);

    public override string ToString() => Name;

    [GeneratedRegex(@"\bThread\s*\.\s*Sleep\b")]
    private static partial Regex ThreadSleep();

    [GeneratedRegex(@"\bTask\s*\.\s*Delay\s*\(\s*(?:\d|TimeSpan\s*\.\s*From|new\s+TimeSpan)")]
    private static partial Regex LiteralTaskDelay();

    [GeneratedRegex(@"\b(?:DateTime|DateTimeOffset)\s*\.\s*(?:UtcNow|Now)\b")]
    private static partial Regex WallClock();

    [GeneratedRegex(@"\bAssert\s*\.\s*(?<kind>True|False)\s*\(")]
    private static partial Regex BooleanAssert();

    [GeneratedRegex(@"!\s*\.")]
    private static partial Regex NullForgivingAccess();

    [GeneratedRegex(@"\b(?:JsonNode|JsonObject|JsonArray)\s*\.\s*Parse\s*$")]
    private static partial Regex JsonParseCall();

    public static IReadOnlyList<TestSourceRule> All { get; } =
    [
        new("async test has an explicit Timeout", file => TestSourceScanner.TestMethods(file.Code)
            .Where(m => m.IsAsync && !Regex.IsMatch(m.AttributeArguments, @"\bTimeout\b"))
            .Select(m => new SourceHit(file.Path, m.Line, $"{m.Name}: async test without Timeout", $"{file.Path}#{m.Name}"))),

        new("no Thread.Sleep", file => Matches(file, ThreadSleep(), "Thread.Sleep")),

        new("no Task.Delay with a literal duration", file => Matches(file, LiteralTaskDelay(), "Task.Delay with a literal duration")),

        new("no wall clock", file => Matches(file, WallClock(), "wall-clock read; use FakeTimeProvider or a fixed timestamp")),

        new("no boolean assert around an equality", file => BooleanAssert().Matches(file.Code)
            .Where(m =>
            {
                var args = TestSourceScanner.Arguments(file.Code, m.Index + m.Length - 1);
                // xUnit has no message overload for Assert.Equal, so a call with a message is a deliberate Assert.True.
                if (args.Count != 1)
                {
                    return false;
                }

                var op = m.Groups["kind"].Value == "True" ? "==" : "!=";
                return TestSourceScanner.HasTopLevelOperator(args[0], op);
            })
            .Select(m => Hit(file, m.Index, $"Assert.{m.Groups["kind"].Value} around a comparison; use Assert.Equal/NotEqual"))),

        new("no null-forgiving member access outside JsonNode", file => file.Path.StartsWith("Njord.Architecture.Tests/", StringComparison.Ordinal)
            ? []
            : NullForgivingAccess().Matches(file.Code)
                .Where(m => !IsJsonNodeReceiver(file.Code, m.Index))
                .Select(m => Hit(file, m.Index, "'!.' on a nullable receiver; use Assert.NotNull first"))),
    ];

    private static IEnumerable<SourceHit> Matches(SourceFile file, Regex pattern, string detail)
        => pattern.Matches(file.Code).Select(m => Hit(file, m.Index, detail));

    private static SourceHit Hit(SourceFile file, int index, string detail)
        => new(file.Path, TestSourceScanner.LineOf(file.Code, index), detail, file.Path);

    // `x["a"]!.Foo()` (an indexer, the JsonNode idiom) and `JsonNode.Parse(...)!.Foo()` are the allowed receivers.
    private static bool IsJsonNodeReceiver(string code, int bangIndex)
    {
        var before = bangIndex - 1;
        while (before >= 0 && char.IsWhiteSpace(code[before]))
        {
            before--;
        }

        if (before < 0)
        {
            return false;
        }

        if (code[before] == ']')
        {
            return true;
        }

        if (code[before] != ')')
        {
            return false;
        }

        var depth = 0;
        for (var i = before; i >= 0; i--)
        {
            if (code[i] == ')')
            {
                depth++;
            }
            else if (code[i] == '(' && --depth == 0)
            {
                return JsonParseCall().IsMatch(code[..i]);
            }
        }

        return false;
    }
}
