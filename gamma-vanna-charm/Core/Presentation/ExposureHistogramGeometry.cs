using System;
using GammaVannaCharm.Core.MarketData;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureHistogramGeometry
{
    public static double GetRowLowerPrice(HistogramRenderLevel level, FuturesFamily futuresFamily, SpotProxySymbol proxySymbol)
    {
        return level.Price - (level.PriceBand * 0.5);
    }

    public static double GetRowUpperPrice(HistogramRenderLevel level, FuturesFamily futuresFamily, SpotProxySymbol proxySymbol)
    {
        return level.Price + (level.PriceBand * 0.5);
    }

    [Obsolete("Price-grid quantization is disabled; row bounds are derived from HistogramRenderLevel.PriceBand.")]
    public static double GetPriceGridWidth(FuturesFamily futuresFamily, SpotProxySymbol proxySymbol)
    {
        return 0.0;
    }
}
