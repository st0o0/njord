namespace Njord.Compute.Contracts;

public sealed record DayScoreSet(
    int DayOffset,
    int Laundry,
    int Outdoor,
    int Running,
    int Cycling,
    int Bbq,
    int Irrigation,
    int Solar,
    int NightVentilation,
    int HoursIncluded,
    ScoreEnvelope? LaundryEnvelope = null,
    ScoreEnvelope? OutdoorEnvelope = null,
    ScoreEnvelope? RunningEnvelope = null,
    ScoreEnvelope? CyclingEnvelope = null,
    ScoreEnvelope? BbqEnvelope = null,
    ScoreEnvelope? IrrigationEnvelope = null,
    ScoreEnvelope? SolarEnvelope = null,
    ScoreEnvelope? NightVentilationEnvelope = null);
