namespace Njord.Compute.Contracts;

public sealed record ScalarDerived(
    double? DiurnalAmplitude,
    double? SunshinePct,
    bool? Inversion);
