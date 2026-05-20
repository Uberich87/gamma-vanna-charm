using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.MarketData.Cboe;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;

namespace GammaVannaCharm.Core.Tests.Presentation;

internal static class ExposurePresentationTests
{
    public static void Run()
    {
        BinsAndRanksStrikeExposuresOutsideNinjaTrader();
        BuildsMappedHistogramLevelsOutsideNinjaTrader();
        ReportsZeroMetricAndUnmappedHistogramStatus();
        ComputesHistogramGeometryOutsideNinjaTrader();
        FormatsExposureCardTextOutsideNinjaTrader();
        ResolvesExposureMetricPresentationDefinitionsOutsideNinjaTrader();
        BuildsExposureCardInsightsOutsideNinjaTrader();
        BuildsOverallExposureCardInsightsOutsideNinjaTrader();
        AnalyzesExposureCardIvConditionOutsideNinjaTrader();
        BuildsExposureZoneStateReadsOutsideNinjaTrader();
        BuildsExposureCardStrikeBiasOutsideNinjaTrader();
        BuildsOptionsStatusTextOutsideNinjaTrader();
        ResolvesCboeOptionsSymbolsOutsideNinjaTrader();
    }

    public static void BinsAndRanksStrikeExposuresOutsideNinjaTrader()
    {
        var exposures = new List<OptionStrikeExposure>
        {
            new OptionStrikeExposure(5001.0, 10.0, 1.0, 2.0, 5, 1),
            new OptionStrikeExposure(5002.0, -4.0, 3.0, -1.0, 7, 2),
            new OptionStrikeExposure(5010.0, -12.0, 2.0, 5.0, 11, 3)
        };

        var ranked = ExposureHistogramBuilder.GetRankedStrikeExposures(exposures, SpotProxySymbol.SPX, OptionExposureMetric.Gamma);

        TestAssert.Equal(2, ranked.Count, "SPX strike exposures are binned into 5-point buckets");
        TestAssert.Equal(5010.0, ranked[0].Strike, "Largest absolute gamma bucket ranks first");
        TestAssert.Equal(-12.0, ranked[0].GammaExposure, "Ranked bucket keeps gamma total");

        var bucket = FindStrike(ranked, 5000.0);
        TestAssert.Equal(6.0, bucket.GammaExposure, "Binned bucket sums gamma exposure");
        TestAssert.Equal(4.0, bucket.VannaExposure, "Binned bucket sums vanna exposure");
        TestAssert.Equal(1.0, bucket.CharmExposure, "Binned bucket sums charm exposure");
        TestAssert.Equal(12L, bucket.OpenInterest, "Binned bucket sums open interest");
        TestAssert.Equal(3, bucket.ContractCount, "Binned bucket sums contracts");

        var vannaRanked = ExposureHistogramBuilder.GetRankedStrikeExposures(exposures, SpotProxySymbol.SPX, OptionExposureMetric.Vanna);
        TestAssert.Equal(5000.0, vannaRanked[0].Strike, "Largest absolute vanna bucket ranks first");
        TestAssert.Equal(4.0, vannaRanked[0].VannaExposure, "Vanna ranking uses vanna exposure");

        var charmRanked = ExposureHistogramBuilder.GetRankedStrikeExposures(exposures, SpotProxySymbol.SPX, OptionExposureMetric.Charm);
        TestAssert.Equal(5010.0, charmRanked[0].Strike, "Largest absolute charm bucket ranks first");
        TestAssert.Equal(5.0, charmRanked[0].CharmExposure, "Charm ranking uses charm exposure");

        var tiedExposures = new List<OptionStrikeExposure>
        {
            new OptionStrikeExposure(5010.0, -10.0, 0.0, 0.0, 1, 1),
            new OptionStrikeExposure(5000.0, 10.0, 0.0, 0.0, 1, 1)
        };

        var tiedRanked = ExposureHistogramBuilder.GetRankedStrikeExposures(tiedExposures, SpotProxySymbol.Auto, OptionExposureMetric.Gamma);
        TestAssert.Equal(5000.0, tiedRanked[0].Strike, "Tied absolute gamma exposure ranks by strike");
    }

    public static void BuildsMappedHistogramLevelsOutsideNinjaTrader()
    {
        var exposures = new List<OptionStrikeExposure>
        {
            new OptionStrikeExposure(6050.0, 10.0, 2.0, 1.0, 5, 1),
            new OptionStrikeExposure(6060.0, -5.0, -12.0, 3.0, 6, 1)
        };

        string status;
        var levels = ExposureHistogramBuilder.Build(
            exposures,
            FuturesFamily.SP500,
            SpotProxySymbol.SPX,
            OptionExposureMetric.Gamma,
            6030.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            75,
            out status);

        TestAssert.Equal<string>(null, status, "Mapped histogram has no status error");
        TestAssert.Equal(2, levels.Count, "Mapped histogram level count");
        TestAssert.Equal(6050.0, levels[0].Strike, "Largest gamma exposure ranks first");
        TestAssert.Equal(6060.0, levels[0].Price, "SPX strike maps by index-point distance to ES");
        TestAssert.Equal(5.0, levels[0].PriceBand, "SPX 5-point bucket maps to 5 ES points");
        TestAssert.Equal(10.0, levels[0].MetricValue, "Selected metric is gamma");
        TestAssert.Equal(1.0, levels[0].Intensity, "Largest metric has full intensity");

        var overallLevels = ExposureHistogramBuilder.Build(
            exposures,
            FuturesFamily.SP500,
            SpotProxySymbol.SPX,
            OptionExposureMetric.Overall,
            6030.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            75,
            out status);
        TestAssert.Equal(levels[0].MetricValue, overallLevels[0].MetricValue, "Overall histogram uses gamma metric values");
        TestAssert.Equal(levels[0].Price, overallLevels[0].Price, "Overall histogram maps gamma levels");
        TestAssert.Equal(0.75, levels[0].Opacity, "Opacity uses capped configured percentage");

        levels = ExposureHistogramBuilder.Build(
            exposures,
            FuturesFamily.SP500,
            SpotProxySymbol.SPX,
            OptionExposureMetric.Gamma,
            6030.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            -25,
            out status);

        TestAssert.Equal(0.0, levels[0].Opacity, "Opacity clamps negative configured percentage");

        var spyLevels = ExposureHistogramBuilder.Build(
            new List<OptionStrikeExposure> { new OptionStrikeExposure(450.0, 10.0, 0.0, 0.0, 1, 1) },
            FuturesFamily.SP500,
            SpotProxySymbol.SPY,
            OptionExposureMetric.Gamma,
            450.0,
            0.0,
            4500.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            75,
            out status);

        TestAssert.Equal(10.0, spyLevels[0].PriceBand, "SPY 1-point bucket maps to 10 ES points");
    }

    public static void ReportsZeroMetricAndUnmappedHistogramStatus()
    {
        var zeroExposures = new List<OptionStrikeExposure>
        {
            new OptionStrikeExposure(6050.0, 0.0, 0.0, 0.0, 5, 1)
        };

        string status;
        var levels = ExposureHistogramBuilder.Build(
            zeroExposures,
            FuturesFamily.SP500,
            SpotProxySymbol.SPX,
            OptionExposureMetric.Gamma,
            6030.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            100,
            out status);

        TestAssert.Equal(0, levels.Count, "Zero metric produces no render levels");
        TestAssert.Equal("Options data loaded, but selected exposure values are zero.", status, "Zero metric status");

        var unmappedExposures = new List<OptionStrikeExposure>
        {
            new OptionStrikeExposure(452.0, 1.0, 0.0, 0.0, 5, 1)
        };

        levels = ExposureHistogramBuilder.Build(
            unmappedExposures,
            FuturesFamily.SP500,
            SpotProxySymbol.QQQ,
            OptionExposureMetric.Gamma,
            450.0,
            0.0,
            6040.0,
            SpotProxyConversionMultipliers.CreateDefault(),
            0.25,
            12.0,
            100,
            out status);

        TestAssert.Equal(0, levels.Count, "Unmapped proxy produces no render levels");
        TestAssert.Equal("Options data loaded, but no option levels mapped to this chart. Check chart instrument and proxy symbol.", status, "Unmapped status");
    }

    public static void ComputesHistogramGeometryOutsideNinjaTrader()
    {
        var level = new HistogramRenderLevel(6050.0, 6061.0, 5.0, 10.0, 1.0, 1.0);

        TestAssert.Equal(6058.5, ExposureHistogramGeometry.GetRowLowerPrice(level, FuturesFamily.SP500, SpotProxySymbol.SPX), "SPX row lower uses mapped 5-point price band");
        TestAssert.Equal(6063.5, ExposureHistogramGeometry.GetRowUpperPrice(level, FuturesFamily.SP500, SpotProxySymbol.SPX), "SPX row upper uses mapped 5-point price band");

        var spyLevel = new HistogramRenderLevel(450.0, 4500.0, 10.0, 10.0, 1.0, 1.0);
        TestAssert.Equal(4495.0, ExposureHistogramGeometry.GetRowLowerPrice(spyLevel, FuturesFamily.SP500, SpotProxySymbol.SPY), "SPY row lower uses mapped 1-point ETF price band");
        TestAssert.Equal(4505.0, ExposureHistogramGeometry.GetRowUpperPrice(spyLevel, FuturesFamily.SP500, SpotProxySymbol.SPY), "SPY row upper uses mapped 1-point ETF price band");

        TestAssert.Equal(6058.5, ExposureHistogramGeometry.GetRowLowerPrice(level, FuturesFamily.Nasdaq100, SpotProxySymbol.QQQ), "Non-SP500 row lower uses price band");
        TestAssert.Equal(6063.5, ExposureHistogramGeometry.GetRowUpperPrice(level, FuturesFamily.Nasdaq100, SpotProxySymbol.QQQ), "Non-SP500 row upper uses price band");
    }

    public static void FormatsExposureCardTextOutsideNinjaTrader()
    {
        TestAssert.Equal("1.25B", ExposureCardTextFormatter.FormatCompactExposure(1250000000.0), "Formats billions");
        TestAssert.Equal("-2.5M", ExposureCardTextFormatter.FormatCompactExposure(-2500000.0), "Formats negative millions");
        TestAssert.Equal("750", ExposureCardTextFormatter.FormatCompactExposure(750.0), "Formats small values");
        TestAssert.Equal("+1.25B", ExposureCardTextFormatter.FormatSignedCompactExposure(1250000000.0), "Formats signed positive exposure");
        TestAssert.Equal("-2.5M", ExposureCardTextFormatter.FormatSignedCompactExposure(-2500000.0), "Formats signed negative exposure");
        TestAssert.Equal("WAIT", ExposureCardTextFormatter.GetStatusBadgeText("GVC: waiting for options data."), "Waiting status badge");
        TestAssert.Equal("ERROR", ExposureCardTextFormatter.GetStatusBadgeText("GVC: options data error: timeout"), "Error status badge");
        TestAssert.Equal("Checking options data...", ExposureCardTextFormatter.CleanStatusText("GVC: waiting for options data."), "Cleans waiting status");
        TestAssert.Equal("Options data error: timeout", ExposureCardTextFormatter.CleanStatusText("GVC: options data error: timeout"), "Cleans API error status");
        TestAssert.Equal("Options data error", ExposureCardTextFormatter.CleanStatusText("GVC: options data error: "), "Cleans empty API error status");
        TestAssert.Equal("loaded", ExposureCardTextFormatter.CleanStatusText("GVC: loaded "), "Trims cleaned GVC status");
        TestAssert.Equal("SPX 6030.25", ExposureCardTextFormatter.FormatHeaderSymbol("_SPX", 6030.25), "Formats header symbol");
        TestAssert.Equal("SPX 7408.50", ExposureCardTextFormatter.FormatHeaderSymbol("_SPX", 7408.5), "Formats header price with trailing zero");
        TestAssert.Equal("6030.25", ExposureCardTextFormatter.FormatHeaderSymbol(null, 6030.25), "Formats header without empty symbol whitespace");

        var unspecifiedUtc = new DateTime(2026, 5, 15, 12, 34, 56, DateTimeKind.Unspecified);
        var expectedLocalTime = DateTime.SpecifyKind(unspecifiedUtc, DateTimeKind.Utc).ToLocalTime();
        TestAssert.Equal(expectedLocalTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), ExposureCardTextFormatter.FormatTime(unspecifiedUtc), "Formats unspecified timestamp as UTC");
        TestAssert.Equal(expectedLocalTime.ToString("MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), ExposureCardTextFormatter.FormatDateTime(unspecifiedUtc), "Formats timestamp date and time");
        TestAssert.Equal("05/15 12:34:56", ExposureCardTextFormatter.FormatUtcDateTime(unspecifiedUtc), "Formats UTC timestamp date and time without local conversion");
        TestAssert.Equal("--/-- --:--:--", ExposureCardTextFormatter.FormatDateTime(DateTime.MinValue), "Formats missing timestamp date and time");
        TestAssert.Equal("--/-- --:--:--", ExposureCardTextFormatter.FormatUtcDateTime(DateTime.MinValue), "Formats missing UTC timestamp date and time");
    }

    public static void ResolvesExposureMetricPresentationDefinitionsOutsideNinjaTrader()
    {
        TestAssert.Equal(4, ExposureMetricPresentation.CardMetricCount, "Card metric order includes overall plus GEX/VEX/CEX");
        TestAssert.Equal(OptionExposureMetric.Overall, ExposureMetricPresentation.GetCardMetric(0), "Overall is the first card metric");
        TestAssert.Equal(OptionExposureMetric.Gamma, ExposureMetricPresentation.GetCardMetric(1), "Gamma is the second card metric");
        TestAssert.Equal("Overall", ExposureMetricPresentation.GetDisplayName(OptionExposureMetric.Overall), "Overall display label");
        TestAssert.Equal("GEX", ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Overall), "Overall resolves to GEX abbreviation");
        TestAssert.Equal(OptionExposureMetric.Gamma, ExposureMetricPresentation.GetPrimaryMetric(OptionExposureMetric.Overall), "Overall uses Gamma as the primary metric");
        TestAssert.Equal("VEX", ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Vanna), "Vanna abbreviation");
        TestAssert.Equal("CEX", ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Charm), "Charm abbreviation");
    }

    public static void BuildsExposureCardInsightsOutsideNinjaTrader()
    {
        var levels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(5300.0, 5322.5, 5.0, 1800000000.0, 1.0, 1.0),
            new HistogramRenderLevel(5275.0, 5297.5, 5.0, -1100000000.0, 0.61, 1.0),
            new HistogramRenderLevel(5350.0, 5372.5, 5.0, 300000000.0, 0.17, 1.0)
        };

        var insight = ExposureCardInsightBuilder.Build(levels, OptionExposureMetric.Gamma, 5316.25, true, 5);

        TestAssert.Equal("Positive GEX: Pin / Mean-Reversion Regime", insight.RegimeText, "Builds gamma regime text");
        TestAssert.Equal(ExposureCardThesisState.Mixed, insight.ThesisState, "Builds mixed thesis state from +GEX above and -GEX below");
        TestAssert.Equal("Above: 5322.5 +GEX (+6.25 pts)", insight.NearestAboveText, "Builds nearest major level above");
        TestAssert.Equal("Below: 5297.5 -GEX (-18.75 pts)", insight.NearestBelowText, "Builds nearest major level below");
        TestAssert.Equal("Fade failed rallies near 5322.5; if price accepts below 5297.5, respect expansion risk.", insight.ActionText, "Builds gamma market read");
        TestAssert.Equal("Plan: Fade failed rallies near 5322.5; if price accepts below 5297.5, respect expansion risk. VEX: IV sensitivity concentrated above; use with IV repricing. CEX: time-decay sensitivity concentrated above; late-session/0DTE context.", ExposureCardInsightBuilder.BuildCombinedActionText(levels, levels, levels, 5316.25, true), "Builds combined market read");
        TestAssert.Equal("Plan: Fade failed rallies near 5322.5; if price accepts below 5297.5, respect expansion risk. FLOW VEX: IV sensitivity concentrated above; use with IV repricing. CEX: time-decay sensitivity concentrated above; late-session/0DTE context.", ExposureCardInsightBuilder.BuildCombinedActionText(levels, levels, levels, 5316.25, true, true), "Builds flow-implied combined market read");
        TestAssert.Equal("GEX trigger zones; wait for price acceptance or rejection.", insight.WatchText, "Builds watch guidance");
        TestAssert.Equal("Above spot: +2.1B GEX", insight.AboveBalanceText, "Builds above-spot balance");
        TestAssert.Equal("Below spot: -1.1B GEX", insight.BelowBalanceText, "Builds below-spot balance");
        TestAssert.Equal("Trader implication: mean-reversion risk above; expansion risk below.", insight.BalanceExplanationText, "Builds gamma balance explanation");
        TestAssert.Equal(3, insight.Levels.Count, "Builds level insight rows");
        TestAssert.Equal("+1.8B", insight.Levels[0].ExposureText, "Builds signed level exposure text");
        TestAssert.Equal("+6.25 pts", insight.Levels[0].DistanceText, "Builds distance from spot");
        TestAssert.Equal("+GEX Dealer-Suppress Zone", insight.Levels[0].LabelText, "Builds positive gamma level label");
        TestAssert.Equal("-GEX Dealer-Amplify Zone", insight.Levels[1].LabelText, "Builds negative gamma level label");

        var priceSortedLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(96.0, 96.0, 5.0, 100.0, 0.05, 1.0),
            new HistogramRenderLevel(98.0, 98.0, 5.0, -900.0, 0.45, 1.0),
            new HistogramRenderLevel(101.0, 101.0, 5.0, 2000.0, 1.0, 1.0),
            new HistogramRenderLevel(104.0, 104.0, 5.0, -500.0, 0.25, 1.0)
        };
        var rankedInsight = ExposureCardInsightBuilder.Build(priceSortedLevels, OptionExposureMetric.Gamma, 100.0, true, 2);
        TestAssert.Equal(101.0, rankedInsight.Levels[0].Price, "Card level rows rank nearby zones by absolute exposure");
        TestAssert.Equal(98.0, rankedInsight.Levels[1].Price, "Card level rows keep second-largest nearby exposure");

        var missingDistanceInsight = ExposureCardInsightBuilder.Build(levels, OptionExposureMetric.Vanna, 0.0, false, 1);
        TestAssert.Equal("VEX: IV-Driven Hedge Pressure", missingDistanceInsight.RegimeText, "Builds selected Vanna regime text");
        TestAssert.Equal("Above: --", missingDistanceInsight.NearestAboveText, "Missing spot suppresses nearest above");
        TestAssert.Equal("--", missingDistanceInsight.Levels[0].DistanceText, "Missing spot suppresses level distance");
        TestAssert.Equal("+VEX IV Sensitivity", missingDistanceInsight.Levels[0].LabelText, "Builds Vanna level label");
        TestAssert.Equal("Balance unavailable; wait for mapped levels.", missingDistanceInsight.BalanceExplanationText, "Builds Vanna-specific balance explanation");
        TestAssert.Equal("Wait for spot.", missingDistanceInsight.ActionText, "Builds Vanna market read");

        var vannaInsight = ExposureCardInsightBuilder.Build(levels, OptionExposureMetric.Vanna, 5316.25, true, 1);
        TestAssert.Equal("+VEX above / -VEX below; IV sensitivity is concentrated above.", vannaInsight.ThesisText, "Builds Vanna thesis pressure map");
        TestAssert.Equal("IV-driven hedge sensitivity is concentrated above (+VEX); use with IV repricing and keep GEX primary.", vannaInsight.ActionText, "Builds Vanna market read");

        var charmInsight = ExposureCardInsightBuilder.Build(levels, OptionExposureMetric.Charm, 5316.25, true, 1);
        TestAssert.Equal("Time-decay hedge sensitivity is concentrated above; most useful late-session/0DTE.", charmInsight.BalanceExplanationText, "Builds Charm-specific balance explanation");
        TestAssert.Equal("+CEX above / -CEX below; time-decay sensitivity is concentrated above.", charmInsight.ThesisText, "Builds Charm thesis pressure map");
        TestAssert.Equal("Time-decay hedge sensitivity is concentrated above (+CEX); most useful late-session/0DTE; keep GEX primary.", charmInsight.ActionText, "Builds Charm market read");
    }

    public static void BuildsOverallExposureCardInsightsOutsideNinjaTrader()
    {
        var rangeGammaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 1000000000.0, 1.0, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, 800000000.0, 0.8, 1.0)
        };
        var vannaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 500000000.0, 1.0, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, 100000000.0, 0.2, 1.0)
        };
        var charmLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 50000000.0, 0.1, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, 500000000.0, 1.0, 1.0)
        };

        var rangeInsight = ExposureCardInsightBuilder.BuildOverall(rangeGammaLevels, vannaLevels, charmLevels, 100.0, true, 5);
        TestAssert.Equal("GEX-Led Pin Risk", rangeInsight.RegimeText, "Overall builds range/pin pressure stack from +GEX above and below");
        TestAssert.Equal(ExposureCardThesisState.Positive, rangeInsight.ThesisState, "Overall marks range/pin state as positive");
        TestAssert.Equal("GEX: Above pin / mean-reversion / Below pin / mean-reversion; base price-response layer.\nVEX: Above strong if IV reprices / Below light IV context; watch IV: if IV reprices on the break, VEX can add conditional hedge pressure.\nCEX: Above light clock context / Below strong late-day; watch time: clock pressure is light early and rises into the final hour.\nOverall: below has gex + cex pin risk; below accepts: failed-break / pin risk remains first; CEX matters more late day.", rangeInsight.ThesisText, "Overall uses stacked GEX/VEX/CEX map for range thesis");
        TestAssert.Equal("Plan: GEX + CEX Pin Risk below. If price accepts below the trigger, expect pin/fade risk unless price holds away; cex matters more late day.", rangeInsight.ActionText, "Overall uses compact pressure-stack market read");
        TestAssert.Equal("Use 110 / 90 as trigger zones; wait for acceptance or rejection.", rangeInsight.WatchText, "Overall builds GEX acceptance watch text");
        TestAssert.Equal("Above: 110 +GEX (+10 pts)", rangeInsight.NearestAboveText, "Overall reuses GEX nearest above");
        TestAssert.Equal("Trader implication: failed breaks/range behavior more likely until acceptance.", rangeInsight.BalanceExplanationText, "Overall reuses GEX balance read");

        var expansionGammaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, -1000000000.0, 1.0, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, -800000000.0, 0.8, 1.0)
        };
        var expansionInsight = ExposureCardInsightBuilder.BuildOverall(expansionGammaLevels, new List<HistogramRenderLevel>(), new List<HistogramRenderLevel>(), 100.0, true, 5);
        TestAssert.Equal("GEX-Led Expansion", expansionInsight.RegimeText, "Overall builds expansion pressure stack from -GEX above and below");
        TestAssert.Equal(ExposureCardThesisState.Negative, expansionInsight.ThesisState, "Overall marks expansion state as negative");
        TestAssert.Equal("GEX: Above expansion / Below expansion; base price-response layer.\nVEX: Above light / neutral / Below light / neutral; watch IV: if IV reprices on the break, VEX can add conditional hedge pressure.\nCEX: Above light / neutral / Below light / neutral; watch time: clock pressure is light early and rises into the final hour.\nOverall: below has gex-led expansion; below accepts: continuation risk increases; avoid blind fades; VEX/CEX context is not strong at this trigger.", expansionInsight.ThesisText, "Overall builds expansion pressure-stack thesis");
        TestAssert.Equal("Plan: GEX-Led Expansion below. If price accepts below the trigger, avoid blind fades; continuation risk increases; vex/cex context is not strong at this trigger.", expansionInsight.ActionText, "Overall handles unavailable VEX/CEX");

        var mixedGammaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 1000000000.0, 1.0, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, -800000000.0, 0.8, 1.0)
        };
        var mixedInsight = ExposureCardInsightBuilder.BuildOverall(mixedGammaLevels, vannaLevels, charmLevels, 100.0, true, 5, true);
        TestAssert.Equal("GEX-Led Expansion Below", mixedInsight.RegimeText, "Overall builds mixed side pressure stack");
        TestAssert.Equal(ExposureCardThesisState.Mixed, mixedInsight.ThesisState, "Overall marks split-side state as mixed");
        TestAssert.Equal("GEX: Above pin / mean-reversion / Below expansion; base price-response layer.\nVEX: Above strong if IV reprices / Below light IV context; watch IV: if IV reprices on the break, VEX can add conditional hedge pressure.\nCEX: Above light clock context / Below strong late-day; watch time: clock pressure is light early and rises into the final hour.\nOverall: below has gex + cex expansion; below accepts: continuation risk increases; avoid blind fades; CEX matters more late day.", mixedInsight.ThesisText, "Overall builds mixed side pressure-stack thesis");
        TestAssert.Equal("Plan: GEX + CEX Expansion below. If price accepts below the trigger, avoid blind fades; continuation risk increases; cex matters more late day.", mixedInsight.ActionText, "Overall notes reinforced downside pressure stack");

        var missingSpotInsight = ExposureCardInsightBuilder.BuildOverall(rangeGammaLevels, new List<HistogramRenderLevel>(), new List<HistogramRenderLevel>(), 0.0, false, 5);
        TestAssert.Equal("Waiting for Trigger Map", missingSpotInsight.RegimeText, "Overall waits without spot");
        TestAssert.Equal(ExposureCardThesisState.Waiting, missingSpotInsight.ThesisState, "Overall marks missing spot state as waiting");
        TestAssert.Equal("Primary: wait for mapped GEX trigger zones.\nVEX: wait for same/nearby-strike IV context.\nCEX: wait for same/nearby-strike clock context.\nOverall: no pressure stack until GEX, VEX, and CEX maps are available.", missingSpotInsight.ThesisText, "Overall builds missing spot thesis");
        TestAssert.Equal("Plan: wait for spot and mapped GEX/VEX/CEX trigger zones.", missingSpotInsight.ActionText, "Overall builds missing spot market read fallback");

        var emptyInsight = ExposureCardInsightBuilder.BuildOverall(new List<HistogramRenderLevel>(), new List<HistogramRenderLevel>(), new List<HistogramRenderLevel>(), 100.0, true, 5);
        TestAssert.Equal("Waiting for Trigger Map", emptyInsight.RegimeText, "Overall waits without GEX levels");
        TestAssert.Equal("Wait for spot and mapped GEX levels.", emptyInsight.WatchText, "Overall builds empty levels watch fallback");
    }

    public static void AnalyzesExposureCardIvConditionOutsideNinjaTrader()
    {
        TestAssert.Equal("Not Measured", ExposureCardIvConditionAnalyzer.Analyze(0.0, 0.0, false, false), "IV condition waits for latest weighted IV");
        TestAssert.Equal("Not Measured", ExposureCardIvConditionAnalyzer.Analyze(double.NaN, 0.20, true, true), "IV condition rejects invalid latest weighted IV");
        TestAssert.Equal("Neutral", ExposureCardIvConditionAnalyzer.Analyze(0.20, 0.0, true, false), "IV condition is neutral until a previous weighted IV exists");
        TestAssert.Equal("Neutral", ExposureCardIvConditionAnalyzer.Analyze(0.205, 0.20, true, true), "Small weighted-IV changes remain neutral");
        TestAssert.Equal("Firming", ExposureCardIvConditionAnalyzer.Analyze(0.207, 0.20, true, true), "Weighted-IV increase past threshold is firming");
        TestAssert.Equal("Fading", ExposureCardIvConditionAnalyzer.Analyze(0.193, 0.20, true, true), "Weighted-IV decrease past threshold is fading");
    }

    public static void BuildsExposureZoneStateReadsOutsideNinjaTrader()
    {
        var buildingLevel = new ExposureCardLevelInsight(
            103.0,
            103.0,
            -100.0,
            3.0,
            true,
            "103",
            "103",
            "-100",
            "+3 pts",
            "Stacked expansion zone|-GEX / VEX-supported / CEX late",
            false,
            0.9);
        var buildingRead = ExposureZoneStateAnalyzer.Analyze(
            buildingLevel,
            new List<double> { -100.0, -115.0, -135.0 },
            new OptionExposureStrikeTrend(OptionExposureMetric.Gamma, 3, 101.0, 103.0, 100.0, 102.0),
            true,
            true);
        TestAssert.Equal("Building", buildingRead.StateText, "Zone state combines notional growth, rank rotation, center migration, and VEX/CEX reinforcement");
        TestAssert.Equal("Toward", buildingRead.MigrationText, "Zone migration uses exposure-center movement toward the zone");

        var unwindingLevel = new ExposureCardLevelInsight(
            97.0,
            97.0,
            -100.0,
            -3.0,
            true,
            "97",
            "97",
            "-100",
            "-3 pts",
            "Expansion zone|-GEX / light VEX / light CEX",
            false,
            0.6);
        var unwindingRead = ExposureZoneStateAnalyzer.Analyze(
            unwindingLevel,
            new List<double> { -150.0, -120.0, -95.0 },
            new OptionExposureStrikeTrend(OptionExposureMetric.Gamma, 3, 97.0, 103.0, 99.0, 101.0),
            false,
            false);
        TestAssert.Equal("Unwinding", unwindingRead.StateText, "Zone state detects fading exposure and top-strike rotation away");
        TestAssert.Equal("Away", unwindingRead.MigrationText, "Zone migration detects center moving away from a below-spot zone");

        var conditionalLevel = new ExposureCardLevelInsight(
            115.0,
            115.0,
            -100.0,
            15.0,
            true,
            "115",
            "115",
            "-100",
            "+15 pts",
            "Expansion zone|-GEX / light VEX / light CEX",
            false,
            0.6);
        var conditionalRead = ExposureZoneStateAnalyzer.Analyze(
            conditionalLevel,
            new List<double> { -100.0, -130.0 },
            null,
            false,
            false);
        TestAssert.Equal("Conditional", conditionalRead.StateText, "Far zones stay conditional even when exposure is rising");
        TestAssert.Equal("Higher", conditionalRead.MigrationText, "Conditional zones without trend keep higher/lower location context");
    }

    public static void BuildsExposureCardStrikeBiasOutsideNinjaTrader()
    {
        var gammaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 1000000000.0, 1.0, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, -900000000.0, 0.9, 1.0)
        };
        var vannaLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, -200000000.0, 0.2, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, 300000000.0, 0.3, 1.0)
        };
        var charmLevels = new List<HistogramRenderLevel>
        {
            new HistogramRenderLevel(110.0, 110.0, 5.0, 500000000.0, 0.5, 1.0),
            new HistogramRenderLevel(90.0, 90.0, 5.0, -500000000.0, 0.5, 1.0)
        };

        var bias = ExposureCardStrikeBiasBuilder.Build(gammaLevels, vannaLevels, charmLevels, 100.0, true, new DateTime(2026, 5, 15, 19, 45, 0, DateTimeKind.Utc));

        TestAssert.True(bias.IndexOf("Above 110: High Confluence Pin Risk", StringComparison.Ordinal) >= 0, "Strike bias identifies overhead +GEX dealer suppression risk with VEX/CEX reinforcement");
        TestAssert.True(bias.IndexOf("Below 90: High Confluence Expansion Below", StringComparison.Ordinal) >= 0, "Strike bias identifies downside -GEX dealer amplification risk with VEX/CEX reinforcement");
        TestAssert.True(bias.IndexOf("CEX", StringComparison.Ordinal) >= 0, "Strike bias includes time-weighted CEX context");
    }

    public static void BuildsOptionsStatusTextOutsideNinjaTrader()
    {
        TestAssert.Equal("GVC: bad proxy", OptionsStatusTextBuilder.Build("bad proxy", null, true, null, false), "Spot price status wins");
        TestAssert.Equal("GVC: options data error: timeout", OptionsStatusTextBuilder.Build(null, "timeout", true, null, false), "API error status");
        TestAssert.Equal("GVC: CBOE response received, but no option contracts parsed.", OptionsStatusTextBuilder.Build(null, "CBOE response received, but no option contracts parsed.", true, null, false), "Provider parse status is data status");
        TestAssert.Equal("GVC: Use 0DTE is off; waiting for the latest options response to reparse.", OptionsStatusTextBuilder.Build(null, null, false, "no 0DTE contracts", false), "0DTE off reparse status");
        TestAssert.Equal<string>(null, OptionsStatusTextBuilder.Build(null, null, true, "loaded", true), "Histogram levels suppress status card");
        TestAssert.Equal("GVC: loaded", OptionsStatusTextBuilder.Build(null, null, true, "loaded", false), "Data status message");
    }

    public static void ResolvesCboeOptionsSymbolsOutsideNinjaTrader()
    {
        TestAssert.Equal("SPY", CboeOptionsSymbolResolver.GetOptionsSymbol(SpotProxySymbol.SPY, "SPY", "_SPX", "QQQ"), "SPY CBOE symbol");
        TestAssert.Equal("_SPX", CboeOptionsSymbolResolver.GetOptionsSymbol(SpotProxySymbol.SPX, "SPY", "_SPX", "QQQ"), "SPX CBOE symbol");
        TestAssert.Equal("QQQ", CboeOptionsSymbolResolver.GetOptionsSymbol(SpotProxySymbol.QQQ, "SPY", "_SPX", "QQQ"), "QQQ CBOE symbol");
        TestAssert.Equal<string>(null, CboeOptionsSymbolResolver.GetOptionsSymbol(SpotProxySymbol.Auto, "SPY", "_SPX", "QQQ"), "Auto has no direct CBOE symbol");
    }

    private static OptionStrikeExposure FindStrike(List<OptionStrikeExposure> exposures, double strike)
    {
        for (var index = 0; index < exposures.Count; index++)
        {
            if (Math.Abs(exposures[index].Strike - strike) <= 0.0000001)
                return exposures[index];
        }

        throw new InvalidOperationException("Strike not found: " + strike);
    }
}
