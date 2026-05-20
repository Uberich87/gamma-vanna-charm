using System;
using System.Collections.Generic;
using System.Reflection;
using GammaVannaCharm.Core.Tests.MarketData;
using GammaVannaCharm.Core.Tests.MarketData.Cboe;
using GammaVannaCharm.Core.Tests.Options;
using GammaVannaCharm.Core.Tests.Presentation;
using Xunit;

namespace GammaVannaCharm.Core.Tests;

public sealed class CoreRegressionTests
{
    public static IEnumerable<object[]> TestCases
    {
        get
        {
            yield return Create("CoreTestSuite.ResolvesFuturesFamilies", CoreTestSuite.ResolvesFuturesFamilies);
            yield return Create("CoreTestSuite.ResolvesAllowedProxySymbols", CoreTestSuite.ResolvesAllowedProxySymbols);
            yield return Create("CoreTestSuite.RejectsInvalidProxySymbols", CoreTestSuite.RejectsInvalidProxySymbols);
            yield return Create("CoreTestSuite.ConvertsProxyPricesToFuturesSpot", CoreTestSuite.ConvertsProxyPricesToFuturesSpot);
            yield return Create("CoreTestSuite.RejectsInvalidConversions", CoreTestSuite.RejectsInvalidConversions);
            yield return Create("SpotProxyLevelMapperTests.MapsSpyLevelToEsWithLiveSpxRatio", SpotProxyLevelMapperTests.MapsSpyLevelToEsWithLiveSpxRatio);
            yield return Create("SpotProxyLevelMapperTests.MapsSpyLevelToEsWithFallbackMultiplier", SpotProxyLevelMapperTests.MapsSpyLevelToEsWithFallbackMultiplier);
            yield return Create("SpotProxyLevelMapperTests.MapsSpxLevelToEsByIndexPointDistance", SpotProxyLevelMapperTests.MapsSpxLevelToEsByIndexPointDistance);
            yield return Create("SpotProxyLevelMapperTests.MapsQqqLevelToNqWithFuturesRatioFallback", SpotProxyLevelMapperTests.MapsQqqLevelToNqWithFuturesRatioFallback);
            yield return Create("SpotProxyLevelMapperTests.RejectsInvalidInputs", SpotProxyLevelMapperTests.RejectsInvalidInputs);
            yield return Create("CboeTests.BuildsCboeOptionsEndpoint", CboeTests.BuildsCboeOptionsEndpoint);
            yield return Create("CboeTests.ParsesCboeOptionFixtures", CboeTests.ParsesCboeOptionFixtures);
            yield return Create("CboeTests.ParsesCboePayloadWithStringNumbersAndRootOptions", CboeTests.ParsesCboePayloadWithStringNumbersAndRootOptions);
            yield return Create("CboeTests.ParsesCboePayloadWithAlternateFieldNamesAndGroupedOptions", CboeTests.ParsesCboePayloadWithAlternateFieldNamesAndGroupedOptions);
            yield return Create("CboeTests.ParsesCboePayloadWithZeroOpenInterestAndVolume", CboeTests.ParsesCboePayloadWithZeroOpenInterestAndVolume);
            yield return Create("CboeTests.ParsesCboePayloadWithReorderedNestedDataAndStringNumbers", CboeTests.ParsesCboePayloadWithReorderedNestedDataAndStringNumbers);
            yield return Create("CboeTests.RejectsMalformedCboeJsonWithoutThrowing", CboeTests.RejectsMalformedCboeJsonWithoutThrowing);
            yield return Create("CboeTests.ParsesCboeSnapshotTimestamp", CboeTests.ParsesCboeSnapshotTimestamp);
            yield return Create("CboeTests.ParsesCboeSnapshotTimestampWithOffset", CboeTests.ParsesCboeSnapshotTimestampWithOffset);
            yield return Create("CboeTests.ParsesZeroDteFromCboeFixtureExpirationDate", CboeTests.ParsesZeroDteFromCboeFixtureExpirationDate);
            yield return Create("CboeTests.ParsesOptionExpirationAtUsMarketClose", CboeTests.ParsesOptionExpirationAtUsMarketClose);
            yield return Create("CboeTests.ParsesSpxMonthlyAmSettledExpirationOnPriorTradingDay", CboeTests.ParsesSpxMonthlyAmSettledExpirationOnPriorTradingDay);
            yield return Create("CboeTests.SkipsInvalidCboeContractsWithoutThrowing", CboeTests.SkipsInvalidCboeContractsWithoutThrowing);
            yield return Create("CboeTests.SkipsExpiredContractsWithoutThrowing", CboeTests.SkipsExpiredContractsWithoutThrowing);
            yield return Create("CboeTests.SkipsSameDayContractsAfterOptionExpirationTime", CboeTests.SkipsSameDayContractsAfterOptionExpirationTime);
            yield return Create("CboeTests.CalculatesEstimatedExposuresFromCboeFixtures", CboeTests.CalculatesEstimatedExposuresFromCboeFixtures);
            yield return Create("CboeTests.CalculatesCboeExposureBundleOutsideNinjaTrader", CboeTests.CalculatesCboeExposureBundleOutsideNinjaTrader);
            yield return Create("OptionDataSourceTests.CalculatesProviderNeutralExposureBundle", OptionDataSourceTests.CalculatesProviderNeutralExposureBundle);
            yield return Create("OptionDataSourceTests.ReportsWaitingStatusBeforeFirstProviderSnapshot", OptionDataSourceTests.ReportsWaitingStatusBeforeFirstProviderSnapshot);
            yield return Create("OptionDataSourceTests.PollsInjectedOptionChainProvider", OptionDataSourceTests.PollsInjectedOptionChainProvider);
            yield return Create("OptionDataSourceTests.DataSourcePollCompletedPublishesCalculatedSnapshot", OptionDataSourceTests.DataSourcePollCompletedPublishesCalculatedSnapshot);
            yield return Create("OptionDataSourceTests.PublishesCompletedPollsForUnchangedSnapshotTimestamps", OptionDataSourceTests.PublishesCompletedPollsForUnchangedSnapshotTimestamps);
            yield return Create("OptionDataSourceTests.IgnoresProviderTimestampOnlyChanges", OptionDataSourceTests.IgnoresProviderTimestampOnlyChanges);
            yield return Create("OptionDataSourceTests.CalculatesLatestExpiredZeroDteSnapshotAtExpiration", OptionDataSourceTests.CalculatesLatestExpiredZeroDteSnapshotAtExpiration);
            yield return Create("OptionDataSourceTests.PollerCanRestartImmediatelyAfterStoppingNonCooperativeProvider", OptionDataSourceTests.PollerCanRestartImmediatelyAfterStoppingNonCooperativeProvider);
            yield return Create("OptionDataSourceTests.PollerDiscardsStoppedProviderErrorsAfterRestart", OptionDataSourceTests.PollerDiscardsStoppedProviderErrorsAfterRestart);
            yield return Create("OptionDataSourceTests.PollerStopCanBeRepeatedWhileProviderCallsAreInFlight", OptionDataSourceTests.PollerStopCanBeRepeatedWhileProviderCallsAreInFlight);
            yield return Create("OptionDataSourceTests.CalculatesFromInjectedDataSourceProvider", OptionDataSourceTests.CalculatesFromInjectedDataSourceProvider);
            yield return Create("OptionDataSourceTests.ProducesEquivalentExposuresFromDifferentProviderMappings", OptionDataSourceTests.ProducesEquivalentExposuresFromDifferentProviderMappings);
            yield return Create("OptionGreekCalculatorTests.MatchesKnownBlackScholesGreekBenchmarks", OptionGreekCalculatorTests.MatchesKnownBlackScholesGreekBenchmarks);
            yield return Create("OptionGreekCalculatorTests.CallAndPutShareGammaAndVannaForSameInputs", OptionGreekCalculatorTests.CallAndPutShareGammaAndVannaForSameInputs);
            yield return Create("OptionGreekCalculatorTests.GammaMatchesFiniteDifferenceDeltaByUnderlying", OptionGreekCalculatorTests.GammaMatchesFiniteDifferenceDeltaByUnderlying);
            yield return Create("OptionGreekCalculatorTests.VannaMatchesFiniteDifferenceDeltaByVolatility", OptionGreekCalculatorTests.VannaMatchesFiniteDifferenceDeltaByVolatility);
            yield return Create("OptionGreekCalculatorTests.CharmMatchesFiniteDifferenceDeltaByCalendarTime", OptionGreekCalculatorTests.CharmMatchesFiniteDifferenceDeltaByCalendarTime);
            yield return Create("OptionGreekCalculatorTests.KeepsNearExpiryGreeksFinite", OptionGreekCalculatorTests.KeepsNearExpiryGreeksFinite);
            yield return Create("OptionGreekCalculatorTests.RejectsInvalidGreekInputs", OptionGreekCalculatorTests.RejectsInvalidGreekInputs);
            yield return Create("OptionExposureFormulaTests.AppliesExpectedGexVexCexScalesAndNaiveSigns", OptionExposureFormulaTests.AppliesExpectedGexVexCexScalesAndNaiveSigns);
            yield return Create("OptionExposureFormulaTests.BalancedCallPutOpenInterestCancelsNaiveExposures", OptionExposureFormulaTests.BalancedCallPutOpenInterestCancelsNaiveExposures);
            yield return Create("OptionExposureFormulaTests.LegacyNaiveExposureAliasesRemainCompatible", OptionExposureFormulaTests.LegacyNaiveExposureAliasesRemainCompatible);
            yield return Create("OptionExposureFormulaTests.ChainExposureUsesProviderGammaOverrideAndCalculatedVannaCharm", OptionExposureFormulaTests.ChainExposureUsesProviderGammaOverrideAndCalculatedVannaCharm);
            yield return Create("OptionExposureFormulaTests.CalculatorReportsWeightedImpliedVolatility", OptionExposureFormulaTests.CalculatorReportsWeightedImpliedVolatility);
            yield return Create("OptionExposureFormulaTests.NaiveResolverAlwaysUsesOpenInterest", OptionExposureFormulaTests.NaiveResolverAlwaysUsesOpenInterest);
            yield return Create("OptionExposureFormulaTests.DefaultCalculatorUsesCalendarDayForCharmExposure", OptionExposureFormulaTests.DefaultCalculatorUsesCalendarDayForCharmExposure);
            yield return Create("OptionExposureFormulaTests.FlowImpliedDealerContractsUseSignedDealerPosition", OptionExposureFormulaTests.FlowImpliedDealerContractsUseSignedDealerPosition);
            yield return Create("OptionFlowImpliedDealerTests.RejectsInvalidFlowIntervals", OptionFlowImpliedDealerTests.RejectsInvalidFlowIntervals);
            yield return Create("OptionFlowImpliedDealerTests.FirstSnapshotSeedsFlowImpliedExposure", OptionFlowImpliedDealerTests.FirstSnapshotSeedsFlowImpliedExposure);
            yield return Create("OptionFlowImpliedDealerTests.AppliesInferredFlowToSeededDealerState", OptionFlowImpliedDealerTests.AppliesInferredFlowToSeededDealerState);
            yield return Create("OptionFlowImpliedDealerTests.ResetsFlowStateWhenSnapshotDateChanges", OptionFlowImpliedDealerTests.ResetsFlowStateWhenSnapshotDateChanges);
            yield return Create("OptionFlowImpliedDealerTests.PersistsExposureHistoryDatabase", OptionFlowImpliedDealerTests.PersistsExposureHistoryDatabase);
            yield return Create("OptionFlowImpliedDealerTests.LoadsRecentExposureHistoryAndLatestDealerState", OptionFlowImpliedDealerTests.LoadsRecentExposureHistoryAndLatestDealerState);
            yield return Create("OptionFlowImpliedDealerTests.LoadsRecentExposureHistoryWithAbsoluteLevelTotals", OptionFlowImpliedDealerTests.LoadsRecentExposureHistoryWithAbsoluteLevelTotals);
            yield return Create("OptionFlowImpliedDealerTests.LoadsStrikeHistoryAcrossDateShards", OptionFlowImpliedDealerTests.LoadsStrikeHistoryAcrossDateShards);
            yield return Create("OptionFlowImpliedDealerTests.InitializesExposureHistoryDatabase", OptionFlowImpliedDealerTests.InitializesExposureHistoryDatabase);
            yield return Create("OptionFlowImpliedDealerTests.CountsEmptyExistingHistoryDatabaseAsZero", OptionFlowImpliedDealerTests.CountsEmptyExistingHistoryDatabaseAsZero);
            yield return Create("OptionFlowImpliedDealerTests.PersistsEffectiveTimestampWhenSnapshotTimestampIsMissing", OptionFlowImpliedDealerTests.PersistsEffectiveTimestampWhenSnapshotTimestampIsMissing);
            yield return Create("OptionFlowImpliedDealerTests.SeparatesExposureHistoryByDataScope", OptionFlowImpliedDealerTests.SeparatesExposureHistoryByDataScope);
            yield return Create("ExposurePresentationTests.BinsAndRanksStrikeExposuresOutsideNinjaTrader", ExposurePresentationTests.BinsAndRanksStrikeExposuresOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsMappedHistogramLevelsOutsideNinjaTrader", ExposurePresentationTests.BuildsMappedHistogramLevelsOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.ReportsZeroMetricAndUnmappedHistogramStatus", ExposurePresentationTests.ReportsZeroMetricAndUnmappedHistogramStatus);
            yield return Create("ExposurePresentationTests.ComputesHistogramGeometryOutsideNinjaTrader", ExposurePresentationTests.ComputesHistogramGeometryOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.FormatsExposureCardTextOutsideNinjaTrader", ExposurePresentationTests.FormatsExposureCardTextOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.ResolvesExposureMetricPresentationDefinitionsOutsideNinjaTrader", ExposurePresentationTests.ResolvesExposureMetricPresentationDefinitionsOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsExposureCardInsightsOutsideNinjaTrader", ExposurePresentationTests.BuildsExposureCardInsightsOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsOverallExposureCardInsightsOutsideNinjaTrader", ExposurePresentationTests.BuildsOverallExposureCardInsightsOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.AnalyzesExposureCardIvConditionOutsideNinjaTrader", ExposurePresentationTests.AnalyzesExposureCardIvConditionOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsExposureZoneStateReadsOutsideNinjaTrader", ExposurePresentationTests.BuildsExposureZoneStateReadsOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsExposureCardStrikeBiasOutsideNinjaTrader", ExposurePresentationTests.BuildsExposureCardStrikeBiasOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.BuildsOptionsStatusTextOutsideNinjaTrader", ExposurePresentationTests.BuildsOptionsStatusTextOutsideNinjaTrader);
            yield return Create("ExposurePresentationTests.ResolvesCboeOptionsSymbolsOutsideNinjaTrader", ExposurePresentationTests.ResolvesCboeOptionsSymbolsOutsideNinjaTrader);
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CoreRegressionCasePasses(string name, Action action)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        action();
    }

    [Fact]
    public void AllPublicRegressionMethodsAreExplicitlyRegistered()
    {
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in TestCases)
            registered.Add((string)testCase[0]);

        AssertPublicRegressionMethodsRegistered(registered, typeof(CoreTestSuite));
        AssertPublicRegressionMethodsRegistered(registered, typeof(SpotProxyLevelMapperTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(CboeTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(OptionDataSourceTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(OptionGreekCalculatorTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(OptionExposureFormulaTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(OptionFlowImpliedDealerTests));
        AssertPublicRegressionMethodsRegistered(registered, typeof(ExposurePresentationTests));
    }

    private static void AssertPublicRegressionMethodsRegistered(HashSet<string> registered, Type fixtureType)
    {
        var methods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        for (var index = 0; index < methods.Length; index++)
        {
            var method = methods[index];
            if (ShouldIgnorePublicRegressionMethod(method))
                continue;

            var name = fixtureType.Name + "." + method.Name;
            Assert.True(registered.Contains(name), "Public regression method is not registered in CoreRegressionTests.TestCases: " + name);
        }
    }

    private static bool ShouldIgnorePublicRegressionMethod(MethodInfo method)
    {
        if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0 || method.ContainsGenericParameters)
            return true;
        if (method.Name == "Run" || method.Name == "RunAll")
            return true;
        if (method.Name == "MarketDataMappings"
            || method.Name == "SpotProxyLevelMapper"
            || method.Name == "Cboe"
            || method.Name == "OptionDataSource"
            || method.Name == "OptionGreeks"
            || method.Name == "OptionExposureFormulas"
            || method.Name == "OptionFlowImpliedDealer"
            || method.Name == "ExposurePresentation")
        {
            return true;
        }

        return false;
    }

    private static object[] Create(string name, Action action)
    {
        return new object[] { name, action };
    }
}
