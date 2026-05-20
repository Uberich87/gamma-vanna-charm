using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Tests.MarketData;
using GammaVannaCharm.Core.Tests.MarketData.Cboe;
using GammaVannaCharm.Core.Tests.Options;
using GammaVannaCharm.Core.Tests.Presentation;

namespace GammaVannaCharm.Core.Tests;

internal static class CoreTestSuite
{
    public static void RunAll()
    {
        MarketDataMappings();
        SpotProxyLevelMapper();
        Cboe();
        OptionDataSource();
        OptionGreeks();
        OptionExposureFormulas();
        OptionFlowImpliedDealer();
        ExposurePresentation();
    }

    public static void MarketDataMappings()
    {
        ResolvesFuturesFamilies();
        ResolvesAllowedProxySymbols();
        RejectsInvalidProxySymbols();
        ConvertsProxyPricesToFuturesSpot();
        RejectsInvalidConversions();
    }

    public static void SpotProxyLevelMapper()
    {
        SpotProxyLevelMapperTests.Run();
    }

    public static void Cboe()
    {
        CboeTests.Run();
    }

    public static void OptionDataSource()
    {
        OptionDataSourceTests.Run();
    }

    public static void OptionGreeks()
    {
        OptionGreekCalculatorTests.Run();
    }

    public static void OptionExposureFormulas()
    {
        OptionExposureFormulaTests.Run();
    }

    public static void OptionFlowImpliedDealer()
    {
        OptionFlowImpliedDealerTests.Run();
    }

    public static void ExposurePresentation()
    {
        ExposurePresentationTests.Run();
    }

    public static void ResolvesFuturesFamilies()
    {
        TestAssert.Equal(FuturesFamily.SP500, FuturesFamilyResolver.Resolve("ES"), "ES family");
        TestAssert.Equal(FuturesFamily.SP500, FuturesFamilyResolver.Resolve("MES"), "MES family");
        TestAssert.Equal(FuturesFamily.SP500, FuturesFamilyResolver.Resolve("ES 06-26"), "ES contract family");
        TestAssert.Equal(FuturesFamily.Nasdaq100, FuturesFamilyResolver.Resolve("NQ"), "NQ family");
        TestAssert.Equal(FuturesFamily.Nasdaq100, FuturesFamilyResolver.Resolve("MNQ"), "MNQ family");
        TestAssert.Equal(FuturesFamily.Unknown, FuturesFamilyResolver.Resolve("CL"), "Unknown family");
    }

    public static void ResolvesAllowedProxySymbols()
    {
        SpotProxySymbol resolved;

        TestAssert.True(SpotProxySymbolResolver.TryResolve(FuturesFamily.SP500, SpotProxySymbol.Auto, out resolved), "ES auto resolves");
        TestAssert.Equal(SpotProxySymbol.SPX, resolved, "ES auto uses SPX");

        TestAssert.True(SpotProxySymbolResolver.TryResolve(FuturesFamily.Nasdaq100, SpotProxySymbol.Auto, out resolved), "NQ auto resolves");
        TestAssert.Equal(SpotProxySymbol.QQQ, resolved, "NQ auto uses QQQ");

        TestAssert.True(SpotProxySymbolResolver.TryResolve(FuturesFamily.SP500, SpotProxySymbol.SPY, out resolved), "ES allows SPY");
        TestAssert.True(SpotProxySymbolResolver.TryResolve(FuturesFamily.SP500, SpotProxySymbol.SPX, out resolved), "ES allows SPX");
        TestAssert.True(SpotProxySymbolResolver.TryResolve(FuturesFamily.Nasdaq100, SpotProxySymbol.QQQ, out resolved), "NQ allows QQQ");
    }

    public static void RejectsInvalidProxySymbols()
    {
        SpotProxySymbol resolved;

        TestAssert.False(SpotProxySymbolResolver.TryResolve(FuturesFamily.SP500, SpotProxySymbol.QQQ, out resolved), "ES rejects QQQ");
        TestAssert.False(SpotProxySymbolResolver.TryResolve(FuturesFamily.Nasdaq100, SpotProxySymbol.SPY, out resolved), "NQ rejects SPY");
        TestAssert.False(SpotProxySymbolResolver.TryResolve(FuturesFamily.Nasdaq100, SpotProxySymbol.SPX, out resolved), "NQ rejects SPX");
        TestAssert.False(SpotProxySymbolResolver.TryResolve(FuturesFamily.Unknown, SpotProxySymbol.Auto, out resolved), "Unknown rejects auto");
    }

    public static void ConvertsProxyPricesToFuturesSpot()
    {
        var multipliers = new SpotProxyConversionMultipliers
        {
            SpyToEs = 10.0,
            SpxToEs = 1.0,
            QqqToNq = 40.0
        };

        double spot;

        TestAssert.True(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.SP500, SpotProxySymbol.SPY, 500.0, multipliers, out spot), "SPY converts");
        TestAssert.Equal(5000.0, spot, "SPY to ES conversion");

        TestAssert.True(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.SP500, SpotProxySymbol.SPX, 5000.0, multipliers, out spot), "SPX converts");
        TestAssert.Equal(5000.0, spot, "SPX to ES conversion");

        TestAssert.True(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.Nasdaq100, SpotProxySymbol.QQQ, 450.0, multipliers, out spot), "QQQ converts");
        TestAssert.Equal(18000.0, spot, "QQQ to NQ conversion");
    }

    public static void RejectsInvalidConversions()
    {
        var multipliers = SpotProxyConversionMultipliers.CreateDefault();
        double spot;

        TestAssert.False(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.SP500, SpotProxySymbol.QQQ, 450.0, multipliers, out spot), "ES rejects QQQ conversion");
        TestAssert.False(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.Nasdaq100, SpotProxySymbol.SPX, 5000.0, multipliers, out spot), "NQ rejects SPX conversion");
        TestAssert.False(SpotProxyPriceConverter.TryConvertToFuturesSpot(FuturesFamily.SP500, SpotProxySymbol.SPY, 0.0, multipliers, out spot), "Rejects zero price");
    }
}
