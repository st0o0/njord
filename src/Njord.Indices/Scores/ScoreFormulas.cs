namespace Njord.Indices.Scores;

internal static class ScoreFormulas
{
    public const double Neutral = 50;

    public static int Clamp(double value) => (int)Math.Round(Math.Clamp(value, 0, 100));

    public static double Penalize(double rawScore, double sensitivity) =>
        Math.Clamp(100 - (100 - rawScore) * sensitivity, 0, 100);

    public static double TempScore(double? temp, double sensitivity) =>
        temp is not { } t ? Neutral : Penalize(Math.Clamp((t - 5) / 20 * 100, 0, 100), sensitivity);

    public static double HumidityScore(double? humidity, double sensitivity) =>
        humidity is not { } h ? Neutral : Penalize(Math.Clamp((70 - h) / 40 * 100, 0, 100), sensitivity);

    public static double WindScore(double? wind, double sensitivity) =>
        wind is not { } w ? Neutral : Penalize(Math.Clamp(w / 4 * 100, 0, 100), sensitivity);

    public static double RainScore(double? rainProb, double sensitivity) =>
        rainProb is not { } r ? Neutral : Penalize(Math.Clamp((60 - r) / 60 * 100, 0, 100), sensitivity);

    public static double CloudScore(double? cloudCover) =>
        cloudCover is not { } c ? Neutral : Math.Clamp(100 - c, 0, 100);

    public static double RangeTempScore(double? temp, double low, double high, double sensitivity)
    {
        if (temp is not { } t)
        {
            return Neutral;
        }

        double raw;
        if (t >= low && t <= high)
        {
            var mid = (low + high) / 2;
            var diff = Math.Abs(t - mid);
            raw = Math.Clamp(100 - diff * diff * 0.5, 0, 100);
        }
        else if (t < low)
        {
            raw = Math.Clamp(100 - (low - t) * 15, 0, 100);
        }
        else
        {
            raw = Math.Clamp(100 - (t - high) * 5, 0, 100);
        }

        var penalty = 100 - raw;
        return Math.Clamp(100 - penalty * sensitivity, 0, 100);
    }
}
