using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureHistogramRenderSignature
{
    public static long Build(IList<HistogramRenderLevel> renderLevels)
    {
        if (renderLevels == null || renderLevels.Count == 0)
            return 0L;

        unchecked
        {
            var hash = 17L;
            for (var index = 0; index < renderLevels.Count; index++)
            {
                var level = renderLevels[index];
                hash = (hash * 31L) + Quantize(level.Strike, 10000.0);
                hash = (hash * 31L) + Quantize(level.Price, 10000.0);
                hash = (hash * 31L) + Quantize(level.PriceBand, 10000.0);
                hash = (hash * 31L) + Quantize(level.MetricValue, 100.0);
                hash = (hash * 31L) + Quantize(level.Intensity, 1000000.0);
                hash = (hash * 31L) + Quantize(level.Opacity, 1000000.0);
            }

            return hash;
        }
    }

    public static long Quantize(double value, double multiplier)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0L;

        return (long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
    }
}
