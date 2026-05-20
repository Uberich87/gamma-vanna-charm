using System;
using System.Collections.Generic;
using System.Globalization;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static partial class ExposureCardInsightBuilder
{
    private const double MajorLevelThresholdRatio = 0.25;
    private const double NearSpotPoints = 5.0;
    private const int DefaultCardLevelCount = 5;
    private const double NearSpotFilterDistanceRatio = 0.05;
    private const double BalanceNeutralThresholdRatio = 0.10;
    private const double DirectionalDominanceRatio = 1.25;
    private const double PriceMatchMinimumTolerance = 5.0;
    private const double ScoreComparisonEpsilon = 0.0000001;

    public static ExposureCardInsight Build(
        IList<HistogramRenderLevel> renderLevels,
        OptionExposureMetric exposureMetric,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        int maxLevelCount,
        string combinedActionText)
    {
        if (renderLevels == null)
            renderLevels = new List<HistogramRenderLevel>();

        if (maxLevelCount <= 0)
            maxLevelCount = DefaultCardLevelCount;

        var isSpotValid = hasCurrentFuturesPrice && IsFinite(currentFuturesPrice);
        var maxAbsMetric = GetMaxAbsMetric(renderLevels);
        var levelCandidates = FilterLevelsNearSpot(renderLevels, currentFuturesPrice, isSpotValid, NearSpotFilterDistanceRatio);
        var levels = new List<ExposureCardLevelInsight>();
        var count = Math.Min(maxLevelCount, levelCandidates.Count);
        for (var index = 0; index < count; index++)
        {
            var level = levelCandidates[index];
            var hasDistance = isSpotValid;
            var distance = hasDistance ? level.Price - currentFuturesPrice : 0.0;
            levels.Add(new ExposureCardLevelInsight(
                level.Strike,
                level.Price,
                level.MetricValue,
                distance,
                hasDistance,
                FormatPrice(level.Strike),
                FormatPrice(level.Price),
                ExposureCardTextFormatter.FormatSignedCompactExposure(level.MetricValue),
                FormatDistance(distance, hasDistance),
                GetLevelLabel(exposureMetric, level.MetricValue, distance, hasDistance),
                level.MetricValue >= 0.0,
                level.Intensity));
        }

        var nearestAbove = FindNearestMajorLevel(renderLevels, currentFuturesPrice, isSpotValid, maxAbsMetric, true);
        var nearestBelow = FindNearestMajorLevel(renderLevels, currentFuturesPrice, isSpotValid, maxAbsMetric, false);
        var aboveBalance = CalculateBalance(renderLevels, currentFuturesPrice, isSpotValid, true);
        var belowBalance = CalculateBalance(renderLevels, currentFuturesPrice, isSpotValid, false);
        var totalAbs = SumAbsMetric(renderLevels);

        return new ExposureCardInsight(
            BuildRegimeText(renderLevels, exposureMetric),
            BuildThesisState(renderLevels, currentFuturesPrice, isSpotValid, nearestAbove, nearestBelow, aboveBalance, belowBalance, totalAbs),
            BuildMetricThesisText(exposureMetric, nearestAbove, nearestBelow, renderLevels, currentFuturesPrice, isSpotValid, aboveBalance, belowBalance, totalAbs),
            string.IsNullOrWhiteSpace(combinedActionText) ? BuildMetricActionText(exposureMetric, renderLevels, currentFuturesPrice, isSpotValid, aboveBalance, belowBalance, totalAbs) : combinedActionText,
            BuildWatchText(exposureMetric, nearestAbove, nearestBelow),
            BuildNearestText("Above", nearestAbove, exposureMetric, currentFuturesPrice, isSpotValid),
            BuildNearestText("Below", nearestBelow, exposureMetric, currentFuturesPrice, isSpotValid),
            BuildBalanceText("Above spot", aboveBalance, exposureMetric, isSpotValid),
            BuildBalanceText("Below spot", belowBalance, exposureMetric, isSpotValid),
            BuildBalanceExplanationText(exposureMetric, aboveBalance, belowBalance, totalAbs, isSpotValid),
            levels);
    }

    public static ExposureCardInsight Build(
        IList<HistogramRenderLevel> renderLevels,
        OptionExposureMetric exposureMetric,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        int maxLevelCount)
    {
        return Build(renderLevels, exposureMetric, currentFuturesPrice, hasCurrentFuturesPrice, maxLevelCount, null);
    }

    public static ExposureCardInsight BuildOverall(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        int maxLevelCount,
        bool usesFlowImpliedDealerModel)
    {
        var isSpotValid = hasCurrentFuturesPrice && IsFinite(currentFuturesPrice);
        var gammaInsight = Build(gammaLevels, OptionExposureMetric.Gamma, currentFuturesPrice, hasCurrentFuturesPrice, maxLevelCount);
        var maxAbsMetric = GetMaxAbsMetric(gammaLevels);
        var nearestAbove = FindNearestMajorLevel(gammaLevels, currentFuturesPrice, isSpotValid, maxAbsMetric, true);
        var nearestBelow = FindNearestMajorLevel(gammaLevels, currentFuturesPrice, isSpotValid, maxAbsMetric, false);
        var aboveBalance = CalculateBalance(gammaLevels, currentFuturesPrice, isSpotValid, true);
        var belowBalance = CalculateBalance(gammaLevels, currentFuturesPrice, isSpotValid, false);
        var totalAbs = SumAbsMetric(gammaLevels);

        return new ExposureCardInsight(
            BuildOverallRegimeText(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, isSpotValid, nearestAbove, nearestBelow, aboveBalance, belowBalance, totalAbs),
            BuildThesisState(gammaLevels, currentFuturesPrice, isSpotValid, nearestAbove, nearestBelow, aboveBalance, belowBalance, totalAbs),
            BuildOverallThesisText(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, isSpotValid, nearestAbove, nearestBelow, aboveBalance, belowBalance, totalAbs),
            BuildOverallActionText(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, isSpotValid, usesFlowImpliedDealerModel),
            BuildOverallWatchText(gammaLevels, currentFuturesPrice, isSpotValid, nearestAbove, nearestBelow),
            gammaInsight.NearestAboveText,
            gammaInsight.NearestBelowText,
            gammaInsight.AboveBalanceText,
            gammaInsight.BelowBalanceText,
            gammaInsight.BalanceExplanationText,
            BuildOverallLevelInsights(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, isSpotValid, maxLevelCount));
    }

    public static ExposureCardInsight BuildOverall(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        int maxLevelCount)
    {
        return BuildOverall(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, hasCurrentFuturesPrice, maxLevelCount, false);
    }

    public static string BuildCombinedActionText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        bool usesFlowImpliedDealerModel)
    {
        return "Plan: "
            + BuildGexPlanText(gammaLevels, currentFuturesPrice, hasCurrentFuturesPrice)
            + " "
            + BuildPressureAgreementText(usesFlowImpliedDealerModel ? "FLOW VEX" : "VEX", gammaLevels, vannaLevels, currentFuturesPrice, hasCurrentFuturesPrice)
            + " "
            + BuildPressureAgreementText("CEX", gammaLevels, charmLevels, currentFuturesPrice, hasCurrentFuturesPrice);
    }

    public static string BuildCombinedActionText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice)
    {
        return BuildCombinedActionText(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, hasCurrentFuturesPrice, false);
    }

    public static string FormatDistance(double distance, bool hasDistance)
    {
        if (!hasDistance || !IsFinite(distance))
            return "--";

        if (Math.Abs(distance) < 0.005)
            return "At spot";

        return (distance > 0.0 ? "+" : string.Empty)
            + distance.ToString("0.##", CultureInfo.InvariantCulture)
            + " pts";
    }

    public static string GetMetricAbbreviation(OptionExposureMetric exposureMetric)
    {
        return ExposureMetricPresentation.GetAbbreviation(exposureMetric);
    }
}
