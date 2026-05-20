namespace GammaVannaCharm.Core.MarketData.Cboe;

public static class CboeOptionsSymbolResolver
{
    public static string GetOptionsSymbol(SpotProxySymbol proxySymbol, string spyOptionsSymbol, string spxOptionsSymbol, string qqqOptionsSymbol)
    {
        if (proxySymbol == SpotProxySymbol.SPY)
            return spyOptionsSymbol;
        if (proxySymbol == SpotProxySymbol.SPX)
            return spxOptionsSymbol;
        if (proxySymbol == SpotProxySymbol.QQQ)
            return qqqOptionsSymbol;

        return null;
    }
}
