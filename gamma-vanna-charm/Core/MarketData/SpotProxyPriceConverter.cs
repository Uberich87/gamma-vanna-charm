using System;

namespace GammaVannaCharm.Core.MarketData;

public static class SpotProxyPriceConverter
{
    public static bool TryConvertToFuturesSpot(
        FuturesFamily futuresFamily,
        SpotProxySymbol proxySymbol,
        double proxyPrice,
        SpotProxyConversionMultipliers multipliers,
        out double futuresSpot)
    {
        futuresSpot = 0.0;

        if (proxyPrice <= 0.0 || multipliers == null)
            return false;

        if (!SpotProxySymbolResolver.IsAllowed(futuresFamily, proxySymbol))
            return false;

        double multiplier;
        if (proxySymbol == SpotProxySymbol.SPY && futuresFamily == FuturesFamily.SP500)
            multiplier = multipliers.SpyToEs;
        else if (proxySymbol == SpotProxySymbol.SPX && futuresFamily == FuturesFamily.SP500)
            multiplier = multipliers.SpxToEs;
        else if (proxySymbol == SpotProxySymbol.QQQ && futuresFamily == FuturesFamily.Nasdaq100)
            multiplier = multipliers.QqqToNq;
        else
            return false;

        if (multiplier <= 0.0 || double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            return false;

        futuresSpot = proxyPrice * multiplier;
        return !double.IsNaN(futuresSpot) && !double.IsInfinity(futuresSpot) && futuresSpot > 0.0;
    }
}
