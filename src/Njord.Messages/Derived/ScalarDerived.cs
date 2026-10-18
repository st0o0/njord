namespace Njord.Messages.Derived;

public sealed record ScalarDerived(
    double? DiurnalAmplitude,
    double? SunshinePct,
    bool? Inversion);
