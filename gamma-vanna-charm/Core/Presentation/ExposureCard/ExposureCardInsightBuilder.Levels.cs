using System;
using System.Collections.Generic;
using System.Globalization;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static partial class ExposureCardInsightBuilder
{
    private static string BuildMetricActionText(
        OptionExposureMetric exposureMetric,
        IList<HistogramRenderLevel> renderLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (exposureMetric == OptionExposureMetric.Vanna)
            return BuildPressureActionText("VEX", "VEX", renderLevels, currentFuturesPrice, hasCurrentFuturesPrice, aboveBalance, belowBalance, totalAbs);

        if (exposureMetric == OptionExposureMetric.Charm)
            return BuildPressureActionText("CEX", "CEX", renderLevels, currentFuturesPrice, hasCurrentFuturesPrice, aboveBalance, belowBalance, totalAbs);

        return BuildGexPlanText(renderLevels, currentFuturesPrice, hasCurrentFuturesPrice);
    }

    private static string BuildGexPlanText(IList<HistogramRenderLevel> gammaLevels, double currentFuturesPrice, bool hasCurrentFuturesPrice)
    {
        if (!hasCurrentFuturesPrice || gammaLevels == null || gammaLevels.Count == 0)
            return "wait for spot and mapped GEX levels.";

        var totalAbs = SumAbsMetric(gammaLevels);
        if (totalAbs <= 0.0)
            return "wait for spot and mapped GEX levels.";

        var maxAbsMetric = GetMaxAbsMetric(gammaLevels);
        var nearestAbove = FindNearestMajorLevel(gammaLevels, currentFuturesPrice, hasCurrentFuturesPrice, maxAbsMetric, true);
        var nearestBelow = FindNearestMajorLevel(gammaLevels, currentFuturesPrice, hasCurrentFuturesPrice, maxAbsMetric, false);
        var aboveSign = GetBalanceSign(CalculateBalance(gammaLevels, currentFuturesPrice, true, true), totalAbs);
        var belowSign = GetBalanceSign(CalculateBalance(gammaLevels, currentFuturesPrice, true, false), totalAbs);
        var aboveLevel = FormatLevelReference(nearestAbove, "upside");
        var belowLevel = FormatLevelReference(nearestBelow, "downside");

        if (aboveSign > 0 && belowSign < 0)
            return "Fade failed rallies near " + aboveLevel + "; if price accepts below " + belowLevel + ", respect expansion risk.";
        if (aboveSign < 0 && belowSign > 0)
            return "If price accepts above " + aboveLevel + ", respect expansion risk; fade failed dips near " + belowLevel + ".";
        if (aboveSign > 0 && belowSign > 0)
            return "Inside " + belowLevel + "-" + aboveLevel + ", favor failed breaks/range; acceptance outside weakens the pin.";
        if (aboveSign < 0 && belowSign < 0)
            return "If price accepts beyond " + belowLevel + "/" + aboveLevel + ", avoid blind fades; expansion risk rises.";
        if (aboveSign > 0)
            return "Fade failed rallies near " + aboveLevel + "; cleaner confirmation needed below.";
        if (aboveSign < 0)
            return "If price accepts above " + aboveLevel + ", respect expansion risk; cleaner confirmation needed below.";
        if (belowSign > 0)
            return "Fade failed dips near " + belowLevel + "; cleaner confirmation needed above.";
        if (belowSign < 0)
            return "If price accepts below " + belowLevel + ", respect expansion risk; cleaner confirmation needed above.";

        return "Mixed exposure; trade level-to-level and wait for confirmation.";
    }

    private static string FormatLevelReference(HistogramRenderLevel level, string fallback)
    {
        return level == null ? fallback : FormatPrice(level.Price);
    }

    private static bool HasUsableLevels(IList<HistogramRenderLevel> renderLevels)
    {
        return renderLevels != null && renderLevels.Count > 0 && SumAbsMetric(renderLevels) > 0.0;
    }

    private static double SumAbsMetric(IList<HistogramRenderLevel> renderLevels)
    {
        var total = 0.0;
        if (renderLevels == null)
            return total;

        for (var index = 0; index < renderLevels.Count; index++)
            total += Math.Abs(renderLevels[index].MetricValue);

        return total;
    }

    private static int GetBalanceSign(double balance, double totalAbs)
    {
        var threshold = totalAbs * BalanceNeutralThresholdRatio;
        if (balance > threshold)
            return 1;
        if (balance < -threshold)
            return -1;

        return 0;
    }

    private static string BuildWatchText(OptionExposureMetric exposureMetric, HistogramRenderLevel nearestAbove, HistogramRenderLevel nearestBelow)
    {
        if (exposureMetric == OptionExposureMetric.Vanna)
            return "Use VEX only with IV repricing and accepted price near levels.";
        if (exposureMetric == OptionExposureMetric.Charm)
            return "Use CEX as late-session/0DTE time-decay context near levels.";

        if (nearestAbove != null && nearestBelow != null)
            return "GEX trigger zones; wait for price acceptance or rejection.";
        if (nearestAbove != null)
            return "Accepted trade above nearest GEX trigger.";
        if (nearestBelow != null)
            return "Accepted trade below nearest GEX trigger.";

        return "Acceptance beyond highlighted levels.";
    }

    private static string BuildNearestText(string prefix, HistogramRenderLevel level, OptionExposureMetric exposureMetric, double currentFuturesPrice, bool hasCurrentFuturesPrice)
    {
        if (level == null)
            return prefix + ": --";

        var distance = hasCurrentFuturesPrice ? level.Price - currentFuturesPrice : 0.0;
        var sign = level.MetricValue >= 0.0 ? "+" : "-";
        return prefix
            + ": "
            + FormatPrice(level.Price)
            + " "
            + sign
            + GetMetricAbbreviation(exposureMetric)
            + " ("
            + FormatDistance(distance, hasCurrentFuturesPrice)
            + ")";
    }

    private static string BuildBalanceText(string prefix, double balance, OptionExposureMetric exposureMetric, bool hasCurrentFuturesPrice)
    {
        if (!hasCurrentFuturesPrice)
            return prefix + ": --";

        return prefix
            + ": "
            + ExposureCardTextFormatter.FormatSignedCompactExposure(balance)
            + " "
            + GetMetricAbbreviation(exposureMetric);
    }

    private static string BuildBalanceExplanationText(OptionExposureMetric exposureMetric, double aboveBalance, double belowBalance, double totalAbs, bool hasCurrentFuturesPrice)
    {
        if (!hasCurrentFuturesPrice || totalAbs <= 0.0)
            return "Balance unavailable; wait for mapped levels.";

        if (exposureMetric == OptionExposureMetric.Vanna)
            return BuildVannaBalanceRead(aboveBalance, belowBalance);
        if (exposureMetric == OptionExposureMetric.Charm)
            return BuildCharmBalanceRead(aboveBalance, belowBalance);

        return BuildGammaBalanceRead(aboveBalance, belowBalance, totalAbs);
    }

    private static string BuildGammaBalanceRead(double aboveBalance, double belowBalance, double totalAbs)
    {
        var aboveSign = GetBalanceSign(aboveBalance, totalAbs);
        var belowSign = GetBalanceSign(belowBalance, totalAbs);

        if (aboveSign > 0 && belowSign < 0)
            return "Trader implication: mean-reversion risk above; expansion risk below.";
        if (aboveSign < 0 && belowSign > 0)
            return "Trader implication: expansion risk above; mean-reversion risk below.";
        if (aboveSign > 0 && belowSign > 0)
            return "Trader implication: failed breaks/range behavior more likely until acceptance.";
        if (aboveSign < 0 && belowSign < 0)
            return "Trader implication: accepted breaks can move faster; avoid blind fades.";
        if (aboveSign > 0)
            return "Trader implication: mean-reversion risk above; cleaner confirmation needed below.";
        if (aboveSign < 0)
            return "Trader implication: expansion risk above; cleaner confirmation needed below.";
        if (belowSign > 0)
            return "Trader implication: mean-reversion risk below; cleaner confirmation needed above.";
        if (belowSign < 0)
            return "Trader implication: expansion risk below; cleaner confirmation needed above.";

        return "Mixed exposure; trade level-to-level and wait for confirmation.";
    }

    private static string BuildVannaBalanceRead(double aboveBalance, double belowBalance)
    {
        var absAbove = Math.Abs(aboveBalance);
        var absBelow = Math.Abs(belowBalance);
        if (absAbove <= 0.0 && absBelow <= 0.0)
            return "IV-driven hedge sensitivity is neutral; keep GEX primary.";
        if (absAbove >= absBelow * DirectionalDominanceRatio)
            return "IV-driven hedge sensitivity is concentrated above; use with IV repricing.";
        if (absBelow >= absAbove * DirectionalDominanceRatio)
            return "IV-driven hedge sensitivity is concentrated below; use with IV repricing.";

        return "VEX context is mixed; do not trade alone.";
    }

    private static string BuildCharmBalanceRead(double aboveBalance, double belowBalance)
    {
        var absAbove = Math.Abs(aboveBalance);
        var absBelow = Math.Abs(belowBalance);
        if (absAbove <= 0.0 && absBelow <= 0.0)
            return "Time-decay hedge sensitivity is neutral; keep GEX primary.";
        if (absAbove >= absBelow * DirectionalDominanceRatio)
            return "Time-decay hedge sensitivity is concentrated above; most useful late-session/0DTE.";
        if (absBelow >= absAbove * DirectionalDominanceRatio)
            return "Time-decay hedge sensitivity is concentrated below; most useful late-session/0DTE.";

        return "CEX context is mixed; do not trade alone.";
    }

    private static List<HistogramRenderLevel> FilterLevelsNearSpot(IList<HistogramRenderLevel> renderLevels, double currentFuturesPrice, bool hasCurrentFuturesPrice, double maxDistanceRatio)
    {
        var levels = new List<HistogramRenderLevel>();
        if (renderLevels == null)
            return levels;
        if (!hasCurrentFuturesPrice || currentFuturesPrice <= 0.0 || maxDistanceRatio <= 0.0)
        {
            for (var index = 0; index < renderLevels.Count; index++)
            {
                if (renderLevels[index] != null)
                    levels.Add(renderLevels[index]);
            }

            SortLevelCandidatesByRelevance(levels, currentFuturesPrice, false);
            return levels;
        }

        var maxDistance = Math.Abs(currentFuturesPrice) * maxDistanceRatio;
        for (var index = 0; index < renderLevels.Count; index++)
        {
            var level = renderLevels[index];
            if (level != null && Math.Abs(level.Price - currentFuturesPrice) <= maxDistance)
                levels.Add(level);
        }

        SortLevelCandidatesByRelevance(levels, currentFuturesPrice, hasCurrentFuturesPrice);
        return levels;
    }

    private static void SortLevelCandidatesByRelevance(List<HistogramRenderLevel> levels, double currentFuturesPrice, bool hasCurrentFuturesPrice)
    {
        if (levels == null || levels.Count <= 1)
            return;

        levels.Sort((left, right) =>
        {
            var metricComparison = Math.Abs(right.MetricValue).CompareTo(Math.Abs(left.MetricValue));
            if (metricComparison != 0)
                return metricComparison;

            if (hasCurrentFuturesPrice)
            {
                var distanceComparison = Math.Abs(left.Price - currentFuturesPrice).CompareTo(Math.Abs(right.Price - currentFuturesPrice));
                if (distanceComparison != 0)
                    return distanceComparison;
            }

            return left.Price.CompareTo(right.Price);
        });
    }

    private static HistogramRenderLevel FindNearestMajorLevel(IList<HistogramRenderLevel> renderLevels, double currentFuturesPrice, bool hasCurrentFuturesPrice, double maxAbsMetric, bool above)
    {
        if (!hasCurrentFuturesPrice || renderLevels == null || renderLevels.Count == 0 || maxAbsMetric <= 0.0)
            return null;

        var threshold = maxAbsMetric * MajorLevelThresholdRatio;
        var nearest = FindNearestLevel(renderLevels, currentFuturesPrice, threshold, above);
        return nearest ?? FindNearestLevel(renderLevels, currentFuturesPrice, 0.0, above);
    }

    private static HistogramRenderLevel FindNearestLevel(IList<HistogramRenderLevel> renderLevels, double currentFuturesPrice, double minAbsMetric, bool above)
    {
        HistogramRenderLevel nearest = null;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < renderLevels.Count; index++)
        {
            var level = renderLevels[index];
            if (Math.Abs(level.MetricValue) < minAbsMetric)
                continue;

            var distance = level.Price - currentFuturesPrice;
            if (above && distance <= 0.0)
                continue;
            if (!above && distance >= 0.0)
                continue;

            var absDistance = Math.Abs(distance);
            if (absDistance >= nearestDistance)
                continue;

            nearest = level;
            nearestDistance = absDistance;
        }

        return nearest;
    }

    private static double CalculateBalance(IList<HistogramRenderLevel> renderLevels, double currentFuturesPrice, bool hasCurrentFuturesPrice, bool above)
    {
        if (!hasCurrentFuturesPrice || renderLevels == null)
            return 0.0;

        var balance = 0.0;
        for (var index = 0; index < renderLevels.Count; index++)
        {
            var distance = renderLevels[index].Price - currentFuturesPrice;
            if (above && distance > 0.0)
                balance += renderLevels[index].MetricValue;
            else if (!above && distance < 0.0)
                balance += renderLevels[index].MetricValue;
        }

        return balance;
    }

    private static double GetMaxAbsMetric(IList<HistogramRenderLevel> renderLevels)
    {
        var maxAbsMetric = 0.0;
        if (renderLevels == null)
            return maxAbsMetric;

        for (var index = 0; index < renderLevels.Count; index++)
            maxAbsMetric = Math.Max(maxAbsMetric, Math.Abs(renderLevels[index].MetricValue));

        return maxAbsMetric;
    }

    private static string GetLevelLabel(OptionExposureMetric exposureMetric, double metricValue, double distance, bool hasDistance)
    {
        var isPositive = metricValue >= 0.0;
        if (exposureMetric == OptionExposureMetric.Vanna)
            return isPositive ? "+VEX IV Sensitivity" : "-VEX IV Sensitivity";
        if (exposureMetric == OptionExposureMetric.Charm)
            return isPositive ? "+CEX Time-Decay Sensitivity" : "-CEX Time-Decay Sensitivity";

        if (isPositive && hasDistance && Math.Abs(distance) <= NearSpotPoints)
            return "+GEX Dealer-Pin Zone";

        return isPositive ? "+GEX Dealer-Suppress Zone" : "-GEX Dealer-Amplify Zone";
    }

    private static string FormatPrice(double price)
    {
        return price.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
