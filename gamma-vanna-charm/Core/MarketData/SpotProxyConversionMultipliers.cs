namespace GammaVannaCharm.Core.MarketData;

public sealed class SpotProxyConversionMultipliers
{
    public double SpyToEs { get; set; }

    public double SpxToEs { get; set; }

    public double QqqToNq { get; set; }

    public static SpotProxyConversionMultipliers CreateDefault()
    {
        return new SpotProxyConversionMultipliers
        {
            SpyToEs = 10.0,
            SpxToEs = 1.0,
            QqqToNq = 40.0
        };
    }
}
