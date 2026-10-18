using Njord.Messages.Derived;

namespace Njord.Messages.Indices;

public sealed record VpdInfo(
    VpdCategory Category,
    double Vpd);
