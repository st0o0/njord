using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record DayScoreSet(
    [property: JsonProperty("day_offset")] int DayOffset,
    [property: JsonProperty("laundry")] int Laundry,
    [property: JsonProperty("outdoor")] int Outdoor,
    [property: JsonProperty("running")] int Running,
    [property: JsonProperty("cycling")] int Cycling,
    [property: JsonProperty("bbq")] int Bbq,
    [property: JsonProperty("irrigation")] int Irrigation,
    [property: JsonProperty("solar")] int Solar,
    [property: JsonProperty("night_ventilation")] int NightVentilation,
    [property: JsonProperty("hours_included")] int HoursIncluded,
    [property: JsonProperty("laundryEnvelope")] ScoreEnvelope? LaundryEnvelope = null,
    [property: JsonProperty("outdoorEnvelope")] ScoreEnvelope? OutdoorEnvelope = null,
    [property: JsonProperty("runningEnvelope")] ScoreEnvelope? RunningEnvelope = null,
    [property: JsonProperty("cyclingEnvelope")] ScoreEnvelope? CyclingEnvelope = null,
    [property: JsonProperty("bbqEnvelope")] ScoreEnvelope? BbqEnvelope = null,
    [property: JsonProperty("irrigationEnvelope")] ScoreEnvelope? IrrigationEnvelope = null,
    [property: JsonProperty("solarEnvelope")] ScoreEnvelope? SolarEnvelope = null,
    [property: JsonProperty("nightVentilationEnvelope")] ScoreEnvelope? NightVentilationEnvelope = null);
