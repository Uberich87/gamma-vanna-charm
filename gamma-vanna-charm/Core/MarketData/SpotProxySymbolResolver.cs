namespace GammaVannaCharm.Core.MarketData;

public static class SpotProxySymbolResolver
{
    public static bool TryResolve(FuturesFamily futuresFamily, SpotProxySymbol requestedSymbol, out SpotProxySymbol resolvedSymbol)
    {
        resolvedSymbol = requestedSymbol;

        if (requestedSymbol == SpotProxySymbol.Auto)
        {
            if (futuresFamily == FuturesFamily.SP500)
            {
                resolvedSymbol = SpotProxySymbol.SPX;
                return true;
            }

            if (futuresFamily == FuturesFamily.Nasdaq100)
            {
                resolvedSymbol = SpotProxySymbol.QQQ;
                return true;
            }

            resolvedSymbol = SpotProxySymbol.Auto;
            return false;
        }

        return IsAllowed(futuresFamily, requestedSymbol);
    }

    public static bool IsAllowed(FuturesFamily futuresFamily, SpotProxySymbol symbol)
    {
        if (futuresFamily == FuturesFamily.SP500)
            return symbol == SpotProxySymbol.SPY || symbol == SpotProxySymbol.SPX;

        if (futuresFamily == FuturesFamily.Nasdaq100)
            return symbol == SpotProxySymbol.QQQ;

        return false;
    }
}
