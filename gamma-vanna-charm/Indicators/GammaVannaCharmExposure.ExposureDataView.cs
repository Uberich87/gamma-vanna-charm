#region Using declarations
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    private OptionExposureMetric SelectedExposureMetric
    {
        get { return ExposureMetricPresentation.GetPrimaryMetric(ExposureMetric); }
    }

    private FuturesFamily ExposureFuturesFamily
    {
        get { return _exposureFuturesFamily; }
    }

    private SpotProxySymbol ResolvedExposureProxySymbol
    {
        get { return _resolvedExposureProxySymbol; }
    }

    private int ExposureHistogramMaxWidthPercent
    {
        get { return HeatmapMaxWidthPercent; }
    }

    private string LastExposureStatusText
    {
        get { return _lastOptionStatusText; }
    }
}
