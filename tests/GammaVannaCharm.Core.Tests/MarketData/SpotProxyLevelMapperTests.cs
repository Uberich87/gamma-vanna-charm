using GammaVannaCharm.Core.MarketData;

namespace GammaVannaCharm.Core.Tests.MarketData;

internal static class SpotProxyLevelMapperTests
{
    public static void Run()
    {
        MapsSpyLevelToEsWithLiveSpxRatio();
        MapsSpyLevelToEsWithFallbackMultiplier();
        MapsSpxLevelToEsByIndexPointDistance();
        MapsQqqLevelToNqWithFuturesRatioFallback();
        RejectsInvalidInputs();
    }

    public static void MapsSpyLevelToEsWithLiveSpxRatio()
    {
        double level;

        TestAssert.True(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.SPY,
            602.0,
            600.0,
            6030.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "SPY level maps to ES with SPX ratio");

        TestAssert.Equal(6060.1, level, "SPY to ES uses live SPX/SPY ratio");
    }

    public static void MapsSpyLevelToEsWithFallbackMultiplier()
    {
        double level;

        TestAssert.True(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.SPY,
            602.0,
            600.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "SPY level maps to ES with fallback multiplier");

        TestAssert.Equal(6060.0, level, "SPY to ES uses fallback multiplier");
    }

    public static void MapsSpxLevelToEsByIndexPointDistance()
    {
        double level;

        TestAssert.True(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.SPX,
            6050.0,
            6030.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "SPX level maps to ES");

        TestAssert.Equal(6060.0, level, "SPX to ES maps index point distance");
    }

    public static void MapsQqqLevelToNqWithFuturesRatioFallback()
    {
        double level;

        TestAssert.True(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.Nasdaq100,
            SpotProxySymbol.QQQ,
            452.0,
            450.0,
            0.0,
            18125.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "QQQ level maps to NQ with futures fallback ratio");

        TestAssert.Equal(18205.5555555556, level, "QQQ to NQ uses live NQ/QQQ fallback ratio");
    }

    public static void RejectsInvalidInputs()
    {
        double level;

        TestAssert.False(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.SPY,
            0.0,
            600.0,
            6030.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "Rejects zero proxy level");

        TestAssert.False(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.SPY,
            602.0,
            0.0,
            6030.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "Rejects zero proxy price");

        TestAssert.False(SpotProxyLevelMapper.TryMapToFuturesLevel(
            FuturesFamily.SP500,
            SpotProxySymbol.QQQ,
            452.0,
            450.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            out level), "Rejects proxy not allowed for futures family");
    }
}
