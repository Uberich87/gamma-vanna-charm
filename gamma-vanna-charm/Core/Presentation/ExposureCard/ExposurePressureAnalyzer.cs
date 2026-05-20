using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Presentation;

internal enum ExposurePressureStrength
{
    None,
    Light,
    Moderate,
    Strong
}

internal static class ExposurePressureAnalyzer
{
    private const double StrongPressureRatio = 0.60;
    private const double ModeratePressureRatio = 0.25;
    private const double LightPressureRatio = 0.10;

    internal static ExposurePressureStrength GetPressureStrength(HistogramRenderLevel level, IList<HistogramRenderLevel> levels)
    {
        if (level == null || levels == null || levels.Count == 0)
            return ExposurePressureStrength.None;

        var maxAbs = 0.0;
        for (var index = 0; index < levels.Count; index++)
        {
            var candidate = levels[index];
            if (candidate == null)
                continue;

            maxAbs = Math.Max(maxAbs, Math.Abs(candidate.MetricValue));
        }

        if (maxAbs <= 0.0)
            return ExposurePressureStrength.None;

        var ratio = Math.Abs(level.MetricValue) / maxAbs;
        if (ratio >= StrongPressureRatio)
            return ExposurePressureStrength.Strong;
        if (ratio >= ModeratePressureRatio)
            return ExposurePressureStrength.Moderate;
        if (ratio >= LightPressureRatio)
            return ExposurePressureStrength.Light;

        return ExposurePressureStrength.None;
    }
}
