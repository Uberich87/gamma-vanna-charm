using System;

namespace GammaVannaCharm.Core.MarketData;

public static class SpotProxyLevelMapper
{
    public static bool TryMapToFuturesLevel(
        FuturesFamily futuresFamily,
        SpotProxySymbol proxySymbol,
        double proxyLevel,
        double currentProxyPrice,
        double currentIndexPrice,
        double currentFuturesPrice,
        SpotProxyConversionMultipliers fallbackMultipliers,
        out double futuresLevel)
    {
        futuresLevel = 0.0;

        if (proxyLevel <= 0.0 || currentProxyPrice <= 0.0 || currentFuturesPrice <= 0.0)
            return false;
        if (!SpotProxySymbolResolver.IsAllowed(futuresFamily, proxySymbol))
            return false;

        double ratio;
        if (!TryResolveRatio(
            futuresFamily,
            proxySymbol,
            currentProxyPrice,
            currentIndexPrice,
            currentFuturesPrice,
            fallbackMultipliers,
            out ratio))
        {
            return false;
        }

        futuresLevel = currentFuturesPrice + ((proxyLevel - currentProxyPrice) * ratio);
        return IsValidPositive(futuresLevel);
    }

    private static bool TryResolveRatio(
        FuturesFamily futuresFamily,
        SpotProxySymbol proxySymbol,
        double currentProxyPrice,
        double currentIndexPrice,
        double currentFuturesPrice,
        SpotProxyConversionMultipliers fallbackMultipliers,
        out double ratio)
    {
        ratio = 0.0;

        if (futuresFamily == FuturesFamily.SP500 && proxySymbol == SpotProxySymbol.SPX)
        {
            ratio = 1.0;
            return true;
        }

        if (futuresFamily == FuturesFamily.SP500 && proxySymbol == SpotProxySymbol.SPY)
        {
            if (IsValidPositive(currentIndexPrice))
                ratio = currentIndexPrice / currentProxyPrice;
            else if (fallbackMultipliers != null)
                ratio = fallbackMultipliers.SpyToEs;

            return IsValidPositive(ratio);
        }

        if (futuresFamily == FuturesFamily.Nasdaq100 && proxySymbol == SpotProxySymbol.QQQ)
        {
            ratio = currentFuturesPrice / currentProxyPrice;
            return IsValidPositive(ratio);
        }

        return false;
    }

    private static bool IsValidPositive(double value)
    {
        return value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
