using System;
using System.IO;
using GammaVannaCharm.Core.MarketData.Cboe;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Tests.MarketData.Cboe;

internal static class CboeTests
{
    public static void Run()
    {
        BuildsCboeOptionsEndpoint();
        ParsesCboeOptionFixtures();
        ParsesCboePayloadWithStringNumbersAndRootOptions();
        ParsesCboePayloadWithAlternateFieldNamesAndGroupedOptions();
        ParsesCboePayloadWithZeroOpenInterestAndVolume();
        ParsesCboePayloadWithReorderedNestedDataAndStringNumbers();
        RejectsMalformedCboeJsonWithoutThrowing();
        ParsesCboeSnapshotTimestamp();
        ParsesCboeSnapshotTimestampWithOffset();
        ParsesZeroDteFromCboeFixtureExpirationDate();
        ParsesOptionExpirationAtUsMarketClose();
        ParsesSpxMonthlyAmSettledExpirationOnPriorTradingDay();
        SkipsInvalidCboeContractsWithoutThrowing();
        SkipsExpiredContractsWithoutThrowing();
        SkipsSameDayContractsAfterOptionExpirationTime();
        CalculatesEstimatedExposuresFromCboeFixtures();
        CalculatesCboeExposureBundleOutsideNinjaTrader();
    }

    public static void BuildsCboeOptionsEndpoint()
    {
        var endpoint = CboeOptionsClient.BuildOptionsEndpoint(
            CboeOptionsClient.DefaultOptionsEndpointBase,
            "_SPX");
        TestAssert.Equal("https://cdn.cboe.com/api/global/delayed_quotes/options/_SPX.json", endpoint, "CBOE options endpoint");
    }

    public static void ParsesCboeOptionFixtures()
    {
        AssertCboeFixture("SPY-options.json", "SPY");
        AssertCboeFixture("QQQ-options.json", "QQQ");
        AssertCboeFixture("SPX-options.json", "_SPX");
    }

    public static void ParsesCboePayloadWithStringNumbersAndRootOptions()
    {
        const string json = @"{
  ""timestamp"": ""2026-05-14T12:00:00Z"",
  ""current_price"": ""750.5"",
  ""options"": [
    {
      ""option"": ""SPY260515C00750000"",
      ""open_interest"": ""10"",
      ""volume"": ""0"",
      ""iv"": ""0.20"",
      ""gamma"": ""0.05"",
      ""bid"": ""1.00"",
      ""ask"": ""1.10"",
      ""last_trade_price"": ""1.05""
    }
  ]
}";

        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "CBOE parser handles root-level options and string numeric fields");
        TestAssert.Equal(750.5, snapshot.UnderlyingPrice, "Root current price parsed");
        TestAssert.Equal(1, snapshot.Contracts.Count, "Root options contract parsed");
        TestAssert.Equal(10L, snapshot.Contracts[0].OpenInterest, "String open interest parsed");
        TestAssert.Equal(0.20, snapshot.Contracts[0].ImpliedVolatility, "String IV parsed");
    }

    public static void ParsesCboePayloadWithAlternateFieldNamesAndGroupedOptions()
    {
        const string json = @"{
  ""data"": {
    ""current_price"": 750.5,
    ""options"": {
      ""calls"": [
        {
          ""symbol"": ""SPY260515C00750000"",
          ""openInterest"": 10,
          ""vol"": 2,
          ""implied_volatility"": 0.20,
          ""gamma"": 0.05,
          ""bid"": 1.00,
          ""ask"": 1.10,
          ""last_price"": 1.05
        }
      ],
      ""puts"": []
    }
  },
  ""timestamp"": ""2026-05-14T12:00:00Z""
}";

        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "CBOE parser handles grouped options and alternate field names");
        TestAssert.Equal(1, snapshot.Contracts.Count, "Grouped option contract parsed");
        TestAssert.Equal(10L, snapshot.Contracts[0].OpenInterest, "Alternate open interest parsed");
        TestAssert.Equal(2L, snapshot.Contracts[0].Volume, "Alternate volume parsed");
        TestAssert.Equal(0.20, snapshot.Contracts[0].ImpliedVolatility, "Alternate IV parsed");
        TestAssert.Equal(1.05, snapshot.Contracts[0].LastPrice, "Alternate last price parsed");
    }

    public static void ParsesCboePayloadWithZeroOpenInterestAndVolume()
    {
        const string json = @"{
  ""timestamp"": ""2026-05-14T12:00:00Z"",
  ""current_price"": 750.5,
  ""options"": [
    {
      ""option"": ""SPY260515C00750000"",
      ""open_interest"": 0,
      ""volume"": 0,
      ""iv"": 0.20,
      ""gamma"": 0.05,
      ""bid"": 1.00,
      ""ask"": 1.10,
      ""last_trade_price"": 1.05
    }
  ]
}";

        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "CBOE parser keeps valid contracts even when OI/volume are zero");
        TestAssert.Equal(1, snapshot.Contracts.Count, "Zero OI/volume contract parsed");
        TestAssert.Equal(0L, snapshot.Contracts[0].OpenInterest, "Zero open interest parsed");
        TestAssert.Equal(0L, snapshot.Contracts[0].Volume, "Zero volume parsed");
    }

    public static void ParsesCboePayloadWithReorderedNestedDataAndStringNumbers()
    {
        const string json = @"{
  ""data"": {
    ""options"": [
      {
        ""last_trade_price"": ""1.05"",
        ""metadata"": { ""source"": ""unit-test"" },
        ""ask"": ""1.10"",
        ""note"": ""escaped braces { } do not terminate rows"",
        ""gamma"": ""5e-2"",
        ""iv"": ""2.0e-1"",
        ""volume"": 0,
        ""open_interest"": ""10.0"",
        ""bid"": ""1.00"",
        ""option"": ""SPY260515C00750000""
      }
    ],
    ""current_price"": 750.5
  },
  ""timestamp"": ""2026-05-14 12:00:00""
}";

        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "CBOE parser handles nested data, reordered fields, and string numeric values");
        TestAssert.Equal(new DateTime(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc), snapshot.TimestampUtc, "Nested payload timestamp parsed");
        TestAssert.Equal(750.5, snapshot.UnderlyingPrice, "Nested current price parsed");
        TestAssert.Equal(1, snapshot.Contracts.Count, "Nested options contract parsed");
        TestAssert.Equal(0.20, snapshot.Contracts[0].ImpliedVolatility, "Scientific-notation string IV parsed");
        TestAssert.Equal(0.05, snapshot.Contracts[0].Gamma, "Scientific-notation string gamma parsed");
    }

    public static void RejectsMalformedCboeJsonWithoutThrowing()
    {
        OptionChainSnapshot snapshot;
        TestAssert.False(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            "{ \"data\": { \"options\": [ }",
            false,
            out snapshot), "CBOE parser rejects malformed JSON without throwing");
        TestAssert.Equal<OptionChainSnapshot>(null, snapshot, "Malformed JSON produces no snapshot");
    }

    private static void AssertCboeFixture(string fixtureFileName, string symbol)
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            symbol,
            ReadCboeFixture(fixtureFileName),
            false,
            out snapshot), fixtureFileName + " parses CBOE option chain");
        TestAssert.Equal(symbol, snapshot.UnderlyingSymbol, fixtureFileName + " underlying symbol");
        TestAssert.True(snapshot.UnderlyingPrice > 0.0, fixtureFileName + " underlying price");
        TestAssert.True(snapshot.Contracts.Count > 0, fixtureFileName + " contracts parsed");
        TestAssert.True(snapshot.Contracts[0].Strike > 0.0, fixtureFileName + " strike parsed");
        TestAssert.True(snapshot.Contracts[0].ImpliedVolatility > 0.0, fixtureFileName + " IV parsed");
        TestAssert.True(snapshot.Contracts[0].ExpirationUnixSeconds > 0L, fixtureFileName + " expiration parsed");
    }

    public static void ParsesCboeSnapshotTimestamp()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            false,
            out snapshot), "SPY CBOE fixture parses timestamp");

        TestAssert.Equal(new DateTime(2026, 5, 14, 22, 59, 23, DateTimeKind.Utc), snapshot.TimestampUtc, "CBOE snapshot timestamp parsed from provider payload");
    }

    public static void ParsesCboeSnapshotTimestampWithOffset()
    {
        OptionChainSnapshot snapshot;
        var json = ReadCboeFixture("SPY-options.json").Replace(
            "\"timestamp\":\"2026-05-14 22:59:23\"",
            "\"timestamp\":\"2026-05-14T18:59:23-04:00\"");
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "SPY CBOE fixture parses timestamp with offset");

        TestAssert.Equal(new DateTime(2026, 5, 14, 22, 59, 23, DateTimeKind.Utc), snapshot.TimestampUtc, "CBOE offset timestamp is normalized to UTC");
    }

    public static void ParsesZeroDteFromCboeFixtureExpirationDate()
    {
        AssertCboeZeroDteFixture("SPY-options.json", "SPY");
        AssertCboeZeroDteFixture("QQQ-options.json", "QQQ");
        AssertCboeZeroDteFixture("SPX-options.json", "_SPX");
    }

    private static void AssertCboeZeroDteFixture(string fixtureFileName, string symbol)
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            symbol,
            ReadCboeFixture(fixtureFileName),
            true,
            out snapshot), fixtureFileName + " parses CBOE 0DTE contracts");
        TestAssert.True(snapshot.Contracts.Count > 0, fixtureFileName + " 0DTE contracts parsed");
    }

    public static void ParsesOptionExpirationAtUsMarketClose()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            true,
            out snapshot), "SPY CBOE 0DTE parse for expiration time");

        TestAssert.True(snapshot.Contracts.Count > 0, "SPY CBOE snapshot has contracts");
        var expectedExpiration = new DateTime(2026, 5, 14, 20, 15, 0, DateTimeKind.Utc);
        var expectedUnixSeconds = (long)(expectedExpiration - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        OptionContractData contract = null;
        foreach (var c in snapshot.Contracts)
        {
            if (c.ExpirationUnixSeconds == expectedUnixSeconds) { contract = c; break; }
        }
        TestAssert.True(contract != null, "SPY CBOE snapshot has contract expiring at 4:15 PM US Eastern");
        var expirationTimeUtc = UnixSecondsToUtc(contract.ExpirationUnixSeconds);
        TestAssert.Equal(expectedExpiration, expirationTimeUtc, "CBOE ETF option expiration uses 4:15 PM US Eastern close");
    }

    public static void ParsesSpxMonthlyAmSettledExpirationOnPriorTradingDay()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "_SPX",
            ReadCboeFixture("SPX-options.json"),
            true,
            out snapshot), "SPX CBOE 0DTE parse for AM-settled expiration time");

        TestAssert.True(snapshot.Contracts.Count > 0, "SPX CBOE snapshot has contracts");
        var expectedExpiration = new DateTime(2026, 5, 14, 21, 0, 0, DateTimeKind.Utc);
        var expectedUnixSeconds = (long)(expectedExpiration - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        OptionContractData contract = null;
        foreach (var c in snapshot.Contracts)
        {
            if (c.ExpirationUnixSeconds == expectedUnixSeconds) { contract = c; break; }
        }
        TestAssert.True(contract != null, "SPX CBOE snapshot has contract expiring at prior trading day 5 PM US Eastern");
        var expirationTimeUtc = UnixSecondsToUtc(contract.ExpirationUnixSeconds);
        TestAssert.Equal(expectedExpiration, expirationTimeUtc, "SPX monthly AM-settled options use prior trading day 5 PM US Eastern close");
    }

    public static void SkipsInvalidCboeContractsWithoutThrowing()
    {
        OptionChainSnapshot snapshot;
        var json = ReadCboeFixture("SPY-options.json").Replace("\"iv\":8.1987", "\"iv\":10.5");
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            json,
            false,
            out snapshot), "CBOE parser skips invalid contracts without throwing");
        TestAssert.True(snapshot.Contracts.Count > 0, "CBOE parser keeps valid contracts after invalid rows");
    }

    public static void SkipsExpiredContractsWithoutThrowing()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            true,
            out snapshot), "SPY CBOE 0DTE parse for expired-contract calculation");

        var staleValuationDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var summary = new OptionChainExposureCalculator().Calculate(
            snapshot,
            staleValuationDate,
            0.0,
            0.0,
            0.01,
            1.0 / 252.0,
            100.0);
        var strikeExposures = new OptionStrikeExposureCalculator().Calculate(
            snapshot,
            staleValuationDate,
            0.0,
            0.0,
            0.01,
            1.0 / 252.0,
            100.0);

        TestAssert.Equal(0, summary.ContractCount, "Expired CBOE summary contracts skipped");
        TestAssert.Equal(0, strikeExposures.Count, "Expired CBOE strike exposures skipped");
    }

    public static void SkipsSameDayContractsAfterOptionExpirationTime()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            true,
            out snapshot), "SPY CBOE 0DTE parse for same-day expiration calculation");

        var afterExpirationTime = new DateTime(2026, 5, 14, 20, 16, 0, DateTimeKind.Utc);
        var summary = new OptionChainExposureCalculator().Calculate(
            snapshot,
            afterExpirationTime,
            0.0,
            0.0,
            0.01,
            1.0 / 252.0,
            100.0);
        var strikeExposures = new OptionStrikeExposureCalculator().Calculate(
            snapshot,
            afterExpirationTime,
            0.0,
            0.0,
            0.01,
            1.0 / 252.0,
            100.0);

        TestAssert.Equal(0, summary.ContractCount, "Same-day contracts skipped after option expiration time");
        TestAssert.Equal(0, strikeExposures.Count, "Same-day strike exposures skipped after option expiration time");
    }

    public static void CalculatesEstimatedExposuresFromCboeFixtures()
    {
        OptionChainSnapshot snapshot;
        TestAssert.True(CboeOptionsParser.TryParseOptionChainSnapshot(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            false,
            out snapshot), "SPY CBOE options parse for exposure calculation");

        var referenceDate = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
        var calculator = new OptionStrikeExposureCalculator();
        var exposures = calculator.Calculate(
            snapshot,
            referenceDate,
            0.0,
            0.0,
            0.01,
            1.0 / 252.0,
            100.0);

        TestAssert.True(exposures.Count > 0, "CBOE strike exposures calculated");
        TestAssert.True(Math.Abs(exposures[0].GammaExposure) > 0.0, "CBOE gamma exposure non-zero");
    }

    public static void CalculatesCboeExposureBundleOutsideNinjaTrader()
    {
        var calculator = new CboeExposureCalculator();
        var referenceDate = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);

        CboeExposureCalculation calculation;
        TestAssert.True(calculator.TryCalculate(
            "SPY",
            ReadCboeFixture("SPY-options.json"),
            false,
            referenceDate,
            out calculation), "CBOE exposure calculation builds outside NinjaTrader");

        TestAssert.True(calculation.UnderlyingPrice > 0.0, "CBOE exposure calculation exposes underlying price");
        TestAssert.True(calculation.Summary.ContractCount > 0, "CBOE exposure calculation exposes summary");
        TestAssert.True(calculation.StrikeExposures.Count > 0, "CBOE exposure calculation exposes strike levels");
    }

    private static DateTime UnixSecondsToUtc(long unixSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
    }

    private static string ReadCboeFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cboe", fileName));
    }
}
