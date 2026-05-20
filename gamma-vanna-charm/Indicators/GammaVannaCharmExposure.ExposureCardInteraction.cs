#region Using declarations
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    private const float ExposureCardWidth = 470.0f;
    private const float ExposureCardHeight = 240.0f;
    private const float ExposureCardCollapsedSize = 54.0f;

    private void SelectExposureMetricFromCard(OptionExposureMetric metric)
    {
        lock (_optionDataProcessingSyncRoot)
        {
            if (ExposureMetric == metric)
                return;

            ExposureMetric = metric;
            RefreshOptionExposureRenderState();
        }

        RequestChartRenderRefresh();
    }

    private void SelectExposureProxyFromCard(SpotProxySymbol proxySymbol)
    {
        if (!SpotProxySymbolResolver.IsAllowed(_exposureFuturesFamily, proxySymbol) || _resolvedExposureProxySymbol == proxySymbol)
            return;

        lock (_optionDataProcessingSyncRoot)
        {
            ExposureProxySymbol = proxySymbol;
            ConfigureSpotPriceProvider();
            DrawOptionStatusText();
        }

        RequestChartRenderRefresh();
    }

    private void ToggleExposureDataScopeFromCard()
    {
        lock (_optionDataProcessingSyncRoot)
        {
            UseZeroDteOptions = !UseZeroDteOptions;
            RefreshOptionExposureRenderState();
        }

        RequestChartRenderRefresh();
    }

    private bool HasExposureProxyButtons()
    {
        return _exposureFuturesFamily == FuturesFamily.SP500;
    }

    private static int GetExposureProxyButtonCount()
    {
        return 2;
    }

    private static SpotProxySymbol GetExposureProxyByButtonIndex(int index)
    {
        return index == 0 ? SpotProxySymbol.SPX : SpotProxySymbol.SPY;
    }

    private static string GetExposureProxyButtonText(int index)
    {
        return index == 0 ? "SPX" : "SPY";
    }

    private SharpDX.RectangleF GetExposureCardRectangle(float width, float height)
    {
        const float margin = 12.0f;
        var left = (float)ChartPanel.X + margin;
        var top = (float)(ChartPanel.Y + ChartPanel.H - height - margin);

        left += TableXOffsetPixels;
        top += TableYOffsetPixels;
        return new SharpDX.RectangleF(left, top, width, height);
    }

    private string GetExposureCardHeaderSymbolText()
    {
        return ExposureCardTextFormatter.FormatHeaderSymbol(GetOptionsSymbol(_resolvedExposureProxySymbol), _underlyingPrice);
    }
}
