using Njord.Egress;

namespace Njord.Tests.Egress;

public sealed class TopicSlugSpec
{
    [Fact]
    public void Converts_to_lowercase()
    {
        Assert.Equal("icon_d2", TopicSlug.Slug("ICON_D2"));
    }

    [Fact]
    public void Replaces_special_characters_with_underscores()
    {
        Assert.Equal("my_location", TopicSlug.Slug("My Location"));
    }

    [Fact]
    public void Preserves_digits()
    {
        Assert.Equal("model123", TopicSlug.Slug("model123"));
    }

    [Fact]
    public void Replaces_dots_and_hyphens()
    {
        Assert.Equal("icon_eu_2_5km", TopicSlug.Slug("icon-eu.2.5km"));
    }

    [Fact]
    public void Already_clean_string_is_unchanged()
    {
        Assert.Equal("gfs_seamless", TopicSlug.Slug("gfs_seamless"));
    }
}
