namespace Njord.Core.Tests;

public sealed class AtomicStateHolderSpec
{
    private sealed record Sample(int Value);

    [Fact]
    public void A_new_holder_has_no_state()
    {
        Assert.Null(new AtomicStateHolder<Sample>().Current);
    }

    [Fact]
    public void Update_replaces_the_current_state()
    {
        var holder = new AtomicStateHolder<Sample>();

        holder.Update(new Sample(1));
        holder.Update(new Sample(2));

        Assert.Equal(new Sample(2), holder.Current);
    }
}
