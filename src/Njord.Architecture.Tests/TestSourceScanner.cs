using System.Text;
using System.Text.RegularExpressions;

namespace Njord.Architecture.Tests;

internal sealed record SourceFile(string Path, string Text, string Code);

internal sealed record TestMethod(string Name, int Line, string AttributeArguments, bool IsAsync);

internal sealed record SourceHit(string Path, int Line, string Detail, string Key);

// Token-aware scan of test sources: comments and literals are blanked (newlines kept, so offsets and line numbers
// stay valid) before any pattern runs, so a rule never fires on text inside a string or a comment.
internal static partial class TestSourceScanner
{
    [GeneratedRegex(@"\[\s*(?:Fact|Theory)\b\s*(?:\((?<args>[^)]*)\))?\s*\]")]
    private static partial Regex TestAttribute();

    [GeneratedRegex(@"\G\s*(?:\[[^\]]*\]\s*)*(?<mods>(?:(?:public|private|internal|protected|static|sealed|override|async|virtual|new)\s+)*)(?<ret>[\w<>\[\],.?\s]+?)\s+(?<name>\w+)\s*(?:<[^>]*>)?\s*\(")]
    private static partial Regex MethodHeader();

    public static string FindSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Njord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Njord.slnx not found above the test output directory.");
    }

    public static IReadOnlyList<SourceFile> SpecFiles()
    {
        var srcRoot = FindSrcRoot();
        return
        [
            .. Directory.GetDirectories(srcRoot, "Njord*Tests")
                .SelectMany(dir => Directory.EnumerateFiles(dir, "*Spec.cs", SearchOption.AllDirectories))
                .Where(file => !IsBuildOutput(file))
                .Select(file =>
                {
                    var text = File.ReadAllText(file);
                    return new SourceFile(
                        System.IO.Path.GetRelativePath(srcRoot, file).Replace(System.IO.Path.DirectorySeparatorChar, '/'),
                        text,
                        Strip(text));
                })
                .OrderBy(f => f.Path, StringComparer.Ordinal),
        ];
    }

    private static bool IsBuildOutput(string file)
        => file.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    // Replaces comments and the content of string and character literals with spaces. String delimiters stay,
    // so `Assert.True("a" == b)` keeps its shape.
    public static string Strip(string source)
    {
        var result = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '/' && Peek(source, i + 1) == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    result.Append(source[i] == '\r' ? '\r' : ' ');
                    i++;
                }
            }
            else if (c == '/' && Peek(source, i + 1) == '*')
            {
                result.Append("  ");
                i += 2;
                while (i < source.Length && !(source[i] == '*' && Peek(source, i + 1) == '/'))
                {
                    result.Append(source[i] is '\n' or '\r' ? source[i] : ' ');
                    i++;
                }

                if (i < source.Length)
                {
                    result.Append("  ");
                    i += 2;
                }
            }
            else if (c == '"' || (c is '$' or '@' && StartsString(source, i)))
            {
                i = BlankString(source, i, result);
            }
            else if (c == '\'')
            {
                result.Append('\'');
                i++;
                while (i < source.Length && source[i] != '\'')
                {
                    var escaped = source[i] == '\\';
                    result.Append(' ');
                    i++;
                    if (escaped && i < source.Length)
                    {
                        result.Append(' ');
                        i++;
                    }
                }

                if (i < source.Length)
                {
                    result.Append('\'');
                    i++;
                }
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    private static char Peek(string s, int index) => index < s.Length ? s[index] : '\0';

    private static bool StartsString(string s, int index)
    {
        var j = index;
        while (j < s.Length && s[j] is '$' or '@')
        {
            j++;
        }

        return j < s.Length && s[j] == '"';
    }

    private static int BlankString(string s, int start, StringBuilder result)
    {
        var i = start;
        var verbatim = false;
        while (s[i] is '$' or '@')
        {
            verbatim |= s[i] == '@';
            result.Append(s[i]);
            i++;
        }

        var quotes = 0;
        while (i + quotes < s.Length && s[i + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            result.Append('"', quotes);
            i += quotes;
            while (i < s.Length)
            {
                if (s[i] == '"' && CountRun(s, i, '"') >= quotes)
                {
                    result.Append('"', quotes);
                    return i + quotes;
                }

                result.Append(s[i] is '\n' or '\r' ? s[i] : ' ');
                i++;
            }

            return i;
        }

        result.Append('"');
        i++;
        while (i < s.Length)
        {
            if (verbatim)
            {
                if (s[i] == '"' && Peek(s, i + 1) == '"')
                {
                    result.Append("  ");
                    i += 2;
                    continue;
                }

                if (s[i] == '"')
                {
                    break;
                }
            }
            else
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    result.Append("  ");
                    i += 2;
                    continue;
                }

                if (s[i] == '"')
                {
                    break;
                }
            }

            result.Append(s[i] is '\n' or '\r' ? s[i] : ' ');
            i++;
        }

        if (i < s.Length)
        {
            result.Append('"');
            i++;
        }

        return i;
    }

    private static int CountRun(string s, int index, char c)
    {
        var n = 0;
        while (index + n < s.Length && s[index + n] == c)
        {
            n++;
        }

        return n;
    }

    public static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    public static IReadOnlyList<TestMethod> TestMethods(string code)
    {
        var methods = new List<TestMethod>();
        foreach (Match attribute in TestAttribute().Matches(code))
        {
            var header = MethodHeader().Match(code, attribute.Index + attribute.Length);
            if (!header.Success)
            {
                continue;
            }

            methods.Add(new TestMethod(
                header.Groups["name"].Value,
                LineOf(code, attribute.Index),
                attribute.Groups["args"].Value,
                Regex.IsMatch(header.Groups["mods"].Value, @"\basync\b")));
        }

        return methods;
    }

    // The arguments of the call whose opening parenthesis is at `open`, split at top-level commas.
    public static IReadOnlyList<string> Arguments(string code, int open)
    {
        var args = new List<string>();
        var depth = 0;
        var start = open + 1;
        for (var i = open; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    if (depth == 0)
                    {
                        args.Add(code[start..i]);
                        return args;
                    }

                    break;
                case ',' when depth == 1:
                    args.Add(code[start..i]);
                    start = i + 1;
                    break;
            }
        }

        return args;
    }

    // True when the top level of `expression` (nested parentheses, brackets and lambdas ignored) contains `op`.
    public static bool HasTopLevelOperator(string expression, string op)
    {
        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (depth == 0 && string.CompareOrdinal(expression, i, op, 0, op.Length) == 0)
            {
                var before = i > 0 ? expression[i - 1] : ' ';
                var after = i + op.Length < expression.Length ? expression[i + op.Length] : ' ';
                if (op == "==" && (before is '=' or '!' or '<' or '>' || after == '='))
                {
                    continue;
                }

                if (op == "!=" && after == '=')
                {
                    continue;
                }

                if (expression.AsSpan(0, i).LastIndexOf("=>") >= 0)
                {
                    return false;
                }

                return true;
            }
        }

        return false;
    }
}
