using System.Text;

namespace Njord.Egress;

public static class TopicSlug
{
    public static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant())
        {
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_');
        }
        return builder.ToString();
    }
}
