using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Exposure;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Tests.Options;

internal static class OptionExposureFormulaTests
{
    public static void Run()
    {
        AppliesExpectedGexVexCexScalesAndNaiveSigns();
        BalancedCallPutOpenInterestCancelsNaiveExposures();
        LegacyNaiveExposureAliasesRemainCompatible();
        ChainExposureUsesProviderGammaOverrideAndCalculatedVannaCharm();
        CalculatorReportsWeightedImpliedVolatility();
        NaiveResolverAlwaysUsesOpenInterest();
        DefaultCalculatorUsesCalendarDayForCharmExposure();
        FlowImpliedDealerContractsUseSignedDealerPosition();
    }

    public static void AppliesExpectedGexVexCexScalesAndNaiveSigns()
    {
        var model = new NaiveExposureModel();
        var marketContext = new MarketContext
        {
            Spot = 50.0,
            IvMove = ExposureDefaults.OnePercentMove,
            TimeStep = ExposureDefaults.OneCalendarDay
        };
        var call = new OptionExposureInput
        {
            Right = OptionRight.Call,
            Gamma = 0.02,
            Vanna = 0.30,
            Charm = -0.40,
            OpenInterest = 10.0,
            ContractSize = 100.0
        };
        var put = new OptionExposureInput
        {
            Right = OptionRight.Put,
            Gamma = call.Gamma,
            Vanna = call.Vanna,
            Charm = call.Charm,
            OpenInterest = call.OpenInterest,
            ContractSize = call.ContractSize
        };

        TestAssert.Equal(500.0, model.CalculateGammaExposure(call, marketContext), "Call GEX scale");
        TestAssert.Equal(150.0, model.CalculateVannaExposure(call, marketContext), "Call VEX scale");
        TestAssert.Equal(-20000.0 / 365.0, model.CalculateCharmExposure(call, marketContext), "Call CEX calendar-day scale");

        TestAssert.Equal(-500.0, model.CalculateGammaExposure(put, marketContext), "Put GEX sign");
        TestAssert.Equal(-150.0, model.CalculateVannaExposure(put, marketContext), "Put VEX sign");
        TestAssert.Equal(20000.0 / 365.0, model.CalculateCharmExposure(put, marketContext), "Put CEX sign");
    }

    public static void BalancedCallPutOpenInterestCancelsNaiveExposures()
    {
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var snapshot = new OptionChainSnapshot(
            "TEST",
            100.0,
            valuationTimeUtc,
            new List<OptionContractData>
            {
                CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0)),
                CreateContract("TEST260619P00100000", OptionRight.Put, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0))
            });

        var calculation = new OptionExposureCalculator().Calculate(
            snapshot,
            valuationTimeUtc,
            0.0,
            0.0,
            ExposureDefaults.OnePercentMove,
            ExposureDefaults.OneCalendarDay,
            ExposureDefaults.ContractSize);

        TestAssert.Equal(2, calculation.NaiveSummary.ContractCount, "Balanced call/put contract count");
        TestAssert.True(Math.Abs(calculation.NaiveSummary.GammaExposure) < 0.0000001, "Balanced call/put GEX cancels");
        TestAssert.True(Math.Abs(calculation.NaiveSummary.VannaExposure) < 0.0000001, "Balanced call/put VEX cancels");
        TestAssert.True(Math.Abs(calculation.NaiveSummary.CharmExposure) < 0.0000001, "Balanced call/put CEX cancels");
        TestAssert.Equal(1, calculation.NaiveStrikeExposures.Count, "Balanced call/put strike bucket count");
        TestAssert.True(Math.Abs(calculation.NaiveStrikeExposures[0].VannaExposure) < 0.0000001, "Balanced call/put strike VEX cancels");
        TestAssert.True(Math.Abs(calculation.NaiveStrikeExposures[0].CharmExposure) < 0.0000001, "Balanced call/put strike CEX cancels");
    }

    public static void LegacyNaiveExposureAliasesRemainCompatible()
    {
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var snapshot = new OptionChainSnapshot(
            "TEST",
            100.0,
            valuationTimeUtc,
            new List<OptionContractData>
            {
                CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0))
            });

        var calculation = new OptionExposureCalculator().Calculate(snapshot, valuationTimeUtc);

#pragma warning disable CS0618
        TestAssert.Equal(calculation.NaiveSummary, calculation.Summary, "Legacy Summary alias remains NaiveSummary");
        TestAssert.Equal(calculation.NaiveStrikeExposures, calculation.StrikeExposures, "Legacy StrikeExposures alias remains NaiveStrikeExposures");
#pragma warning restore CS0618
    }

    public static void ChainExposureUsesProviderGammaOverrideAndCalculatedVannaCharm()
    {
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var contract = CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0));
        contract.Gamma = 0.05;
        var snapshot = new OptionChainSnapshot("TEST", 100.0, valuationTimeUtc, new List<OptionContractData> { contract });
        var greekCalculator = new RecordingGreekCalculator(new OptionGreeks(999.0, 2.0, 3.0));
        var calculator = new OptionChainExposureCalculator(greekCalculator, new NaiveExposureModel());

        var summary = calculator.Calculate(
            snapshot,
            valuationTimeUtc,
            0.03,
            0.01,
            ExposureDefaults.OnePercentMove,
            ExposureDefaults.OneCalendarDay,
            ExposureDefaults.ContractSize);

        TestAssert.Equal(1, greekCalculator.CallCount, "Greek calculator is called once for the contract");
        TestAssert.Equal(0.03, greekCalculator.LastInput.RiskFreeRate, "Greek calculator receives risk-free rate");
        TestAssert.Equal(0.01, greekCalculator.LastInput.DividendYield, "Greek calculator receives dividend yield");
        TestAssert.Equal(1, summary.ContractCount, "Chain summary includes contract");
        TestAssert.Equal(5000.0, summary.GammaExposure, "Provider gamma overrides calculated gamma for GEX");
        TestAssert.Equal(2000.0, summary.VannaExposure, "Calculated vanna drives VEX");
        TestAssert.Equal(300000.0 / 365.0, summary.CharmExposure, "Calculated charm drives CEX");
    }

    public static void CalculatorReportsWeightedImpliedVolatility()
    {
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var snapshot = new OptionChainSnapshot(
            "TEST",
            100.0,
            valuationTimeUtc,
            new List<OptionContractData>
            {
                CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0)),
                CreateContract("TEST260619C00105000", OptionRight.Call, 105.0, 30L, 0.40, valuationTimeUtc.AddDays(35.0))
            });

        var calculation = new OptionExposureCalculator().Calculate(snapshot, valuationTimeUtc);

        TestAssert.Equal(0.35, calculation.NaiveSummary.WeightedImpliedVolatility, "Weighted IV uses exposure-size weights for Vanna IV condition");
    }

    public static void NaiveResolverAlwaysUsesOpenInterest()
    {
        // NAIVE mode is a static "open positioning" proxy and must use open interest for
        // every expiration. Intraday flow inference lives in FLOW mode (OptionFlowImpliedDealerStore)
        // so users can rely on a clean mental model when toggling between modes.
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var nearExpiryContract = CreateContract("TEST260515C00100000", OptionRight.Call, 100.0, 1000L, 0.20, valuationTimeUtc.AddHours(6.0));
        nearExpiryContract.Volume = 5L;
        nearExpiryContract.Gamma = 0.05;
        var laterExpiryContract = CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 1000L, 0.20, valuationTimeUtc.AddDays(35.0));
        laterExpiryContract.Volume = 5L;
        laterExpiryContract.Gamma = 0.05;

        var calculator = new OptionExposureCalculator();
        var nearExpiryCalculation = calculator.Calculate(
            new OptionChainSnapshot("TEST", 100.0, valuationTimeUtc, new List<OptionContractData> { nearExpiryContract }),
            valuationTimeUtc);
        var laterExpiryCalculation = calculator.Calculate(
            new OptionChainSnapshot("TEST", 100.0, valuationTimeUtc, new List<OptionContractData> { laterExpiryContract }),
            valuationTimeUtc);

        TestAssert.Equal(500000.0, nearExpiryCalculation.NaiveSummary.GammaExposure, "Near-expiry naive DDOI uses open interest, not volume");
        TestAssert.Equal(500000.0, laterExpiryCalculation.NaiveSummary.GammaExposure, "Later-expiry naive DDOI uses open interest");
    }

    public static void DefaultCalculatorUsesCalendarDayForCharmExposure()
    {
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var snapshot = new OptionChainSnapshot(
            "TEST",
            100.0,
            valuationTimeUtc,
            new List<OptionContractData>
            {
                CreateContract("TEST260619C00100000", OptionRight.Call, 100.0, 10L, 0.20, valuationTimeUtc.AddDays(35.0))
            });
        var calculator = new OptionExposureCalculator();

        var defaultCalculation = calculator.Calculate(snapshot, valuationTimeUtc);
        var explicitCalendarDayCalculation = calculator.Calculate(
            snapshot,
            valuationTimeUtc,
            0.0,
            0.0,
            ExposureDefaults.OnePercentMove,
            ExposureDefaults.OneCalendarDay,
            ExposureDefaults.ContractSize);
        var explicitTradingDayCalculation = calculator.Calculate(
            snapshot,
            valuationTimeUtc,
            0.0,
            0.0,
            ExposureDefaults.OnePercentMove,
            ExposureDefaults.OneTradingDay,
            ExposureDefaults.ContractSize);

        TestAssert.Equal(explicitCalendarDayCalculation.NaiveSummary.CharmExposure, defaultCalculation.NaiveSummary.CharmExposure, "Default CEX uses one calendar day");
        TestAssert.True(Math.Abs(defaultCalculation.NaiveSummary.CharmExposure - explicitTradingDayCalculation.NaiveSummary.CharmExposure) > 0.0000001, "Default CEX does not use one trading day");
    }

    public static void FlowImpliedDealerContractsUseSignedDealerPosition()
    {
        // Drive the store with two snapshots so FLOW dealer state starts from the OI baseline
        // and is then adjusted by a poll-to-poll classified delta.
        var valuationTimeUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var firstContract = CreateContract("TEST260619P00100000", OptionRight.Put, 100.0, 100L, 0.20, valuationTimeUtc.AddDays(35.0));
        firstContract.Volume = 0L;
        firstContract.Gamma = 0.05;
        firstContract.Bid = 1.00;
        firstContract.Ask = 1.20;
        firstContract.LastPrice = 1.10;
        var secondContract = CreateContract("TEST260619P00100000", OptionRight.Put, 100.0, 100L, 0.22, valuationTimeUtc.AddDays(35.0));
        secondContract.Volume = 10L;
        secondContract.Gamma = 0.05;
        secondContract.Bid = 1.00;
        secondContract.Ask = 1.20;
        secondContract.LastPrice = 1.20;
        var firstSnapshot = new OptionChainSnapshot("TEST", 100.0, valuationTimeUtc, new List<OptionContractData> { firstContract });
        var secondSnapshot = new OptionChainSnapshot("TEST", 100.0, valuationTimeUtc.AddSeconds(60.0), new List<OptionContractData> { secondContract });

        var store = new OptionFlowImpliedDealerStore();
        store.Update(firstSnapshot);
        var impliedDealerSnapshot = store.Update(secondSnapshot);

        double dealerContracts;
        TestAssert.True(impliedDealerSnapshot.TryGetDealerContracts(secondContract.Symbol, out dealerContracts), "Flow-implied DDOI contract exists after second snapshot");
        // Seeded put baseline = -100 contracts. Quote classifier: last=ask -> side=+1
        // (customer buy), conf=0.55. IV rose 0.20 -> 0.22: score += 0.4,
        // conf += 0.25 -> 0.80. Dealer delta = -side * volume * conf = -8.0.
        TestAssert.Equal(-108.0, dealerContracts, "Customer buy at ask with rising IV adjusts seeded signed dealer contracts");

        var calculation = new OptionExposureCalculator().Calculate(secondSnapshot, valuationTimeUtc.AddSeconds(60.0), impliedDealerSnapshot);

        TestAssert.True(calculation.HasFlowImpliedDealerExposure, "Flow-implied dealer exposure is available");
        TestAssert.Equal(1, calculation.FlowImpliedDealerSummary.ContractCount, "Flow-implied dealer contract count");
        // GEX = gamma * dealerContracts * S^2 * contractSize * 0.01 = 0.05 * (-108) * 10000 * 100 * 0.01 = -54000.
        TestAssert.Equal(-54000.0, calculation.FlowImpliedDealerSummary.GammaExposure, "Flow-implied dealer GEX uses seeded signed dealer contracts without naive put inversion");
    }

    private sealed class RecordingGreekCalculator : IOptionGreekCalculator
    {
        private readonly OptionGreeks _greeks;

        public RecordingGreekCalculator(OptionGreeks greeks)
        {
            _greeks = greeks;
        }

        public int CallCount { get; private set; }

        public OptionGreekInput LastInput { get; private set; }

        public OptionGreeks Calculate(OptionGreekInput input)
        {
            CallCount++;
            LastInput = input;
            return _greeks;
        }
    }

    private static OptionContractData CreateContract(string symbol, OptionRight right, double strike, long openInterest, double impliedVolatility, DateTime expirationTimeUtc)
    {
        return new OptionContractData
        {
            Symbol = symbol,
            Right = right,
            Strike = strike,
            OpenInterest = openInterest,
            Volume = 0L,
            ImpliedVolatility = impliedVolatility,
            Gamma = 0.0,
            Bid = 1.00,
            Ask = 1.20,
            LastPrice = 1.10,
            ExpirationUnixSeconds = ToUnixSeconds(expirationTimeUtc)
        };
    }

    private static long ToUnixSeconds(DateTime timestampUtc)
    {
        return (long)(timestampUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}
