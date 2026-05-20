using System;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureCardIvConditionAnalyzer
{
    public static string Analyze(
        double latestWeightedImpliedVolatility,
        double previousWeightedImpliedVolatility,
        bool hasLatestWeightedImpliedVolatility,
        bool hasPreviousWeightedImpliedVolatility)
    {
        if (!hasLatestWeightedImpliedVolatility || !IsFinite(latestWeightedImpliedVolatility) || latestWeightedImpliedVolatility <= 0.0)
            return "Not Measured";
        if (!hasPreviousWeightedImpliedVolatility || !IsFinite(previousWeightedImpliedVolatility) || previousWeightedImpliedVolatility <= 0.0)
            return "Neutral";

        var latestVolPoints = latestWeightedImpliedVolatility * 100.0;
        var previousVolPoints = previousWeightedImpliedVolatility * 100.0;
        var deltaVolPoints = latestVolPoints - previousVolPoints;
        var thresholdVolPoints = Math.Max(0.15, Math.Abs(previousVolPoints) * 0.03);
        if (deltaVolPoints >= thresholdVolPoints)
            return "Firming";
        if (deltaVolPoints <= -thresholdVolPoints)
            return "Fading";

        return "Neutral";
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
