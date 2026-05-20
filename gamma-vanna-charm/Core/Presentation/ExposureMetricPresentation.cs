using System;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureMetricPresentation
{
    public const string CardTitle = "GEX · VEX · CEX";

    public static int CardMetricCount
    {
        get { return 4; }
    }

    public static OptionExposureMetric GetCardMetric(int index)
    {
        switch (index)
        {
            case 0:
                return OptionExposureMetric.Overall;
            case 1:
                return OptionExposureMetric.Gamma;
            case 2:
                return OptionExposureMetric.Vanna;
            case 3:
                return OptionExposureMetric.Charm;
            default:
                throw new ArgumentOutOfRangeException("index", index, "Card metric index is out of range.");
        }
    }

    public static OptionExposureMetric GetPrimaryMetric(OptionExposureMetric metric)
    {
        return metric == OptionExposureMetric.Overall ? OptionExposureMetric.Gamma : metric;
    }

    public static string GetDisplayName(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Overall)
            return "Overall";
        if (metric == OptionExposureMetric.Vanna)
            return "Vanna";
        if (metric == OptionExposureMetric.Charm)
            return "Charm";

        return "Gamma";
    }

    public static string GetAbbreviation(OptionExposureMetric metric)
    {
        var primaryMetric = GetPrimaryMetric(metric);
        if (primaryMetric == OptionExposureMetric.Vanna)
            return "VEX";
        if (primaryMetric == OptionExposureMetric.Charm)
            return "CEX";

        return "GEX";
    }
}
