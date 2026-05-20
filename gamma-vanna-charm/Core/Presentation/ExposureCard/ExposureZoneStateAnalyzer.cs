using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public sealed class ExposureZoneStateRead
{
    public ExposureZoneStateRead(string stateText, string migrationText)
    {
        StateText = stateText ?? string.Empty;
        MigrationText = migrationText ?? string.Empty;
    }

    public string StateText { get; private set; }

    public string MigrationText { get; private set; }
}

public static class ExposureZoneStateAnalyzer
{
    private const double BuildingThresholdRatio = 0.08;
    private const double SoftTrendThresholdRatio = 0.03;
    private const double NearSpotActivationRatio = 0.50;
    private const double MinimumConditionalDistancePoints = 5.0;
    private const double ConditionalDistanceSpotRatio = 0.006;
    private const double MinimumCenterMigrationThresholdPoints = 0.25;
    private const double CenterMigrationThresholdRatio = 0.0001;
    private const double NearSpotActivationScore = 0.5;
    private const double ContextScore = 0.5;
    private const double ContextMinimumBaseScore = -0.5;
    private const double BuildingScoreThreshold = 2.0;
    private const double UnwindingScoreThreshold = -2.0;
    private const double PriceComparisonEpsilon = 0.0000001;

    public static ExposureZoneStateRead Analyze(
        ExposureCardLevelInsight level,
        IList<double> exposureHistoryValues,
        OptionExposureStrikeTrend strikeTrend,
        bool hasVannaContext,
        bool hasCharmContext)
    {
        if (level == null)
            return new ExposureZoneStateRead("Conditional", "Flat");

        var migration = ResolveMigration(level, strikeTrend);
        if (IsConditional(level))
            return new ExposureZoneStateRead("Conditional", migration);

        var score = 0.0;
        score += GetNotionalChangeScore(exposureHistoryValues);
        score += GetTrendSlopeScore(exposureHistoryValues);
        score += GetRankRotationScore(level, strikeTrend);
        score += GetMigrationScore(migration);

        if (level.HasDistanceFromSpot && Math.Abs(level.DistanceFromSpot) <= GetConditionalDistanceThreshold(level) * NearSpotActivationRatio)
            score += NearSpotActivationScore;

        if ((hasVannaContext || hasCharmContext) && score >= ContextMinimumBaseScore)
            score += ContextScore;

        if (score >= BuildingScoreThreshold)
            return new ExposureZoneStateRead("Building", migration);
        if (score <= UnwindingScoreThreshold)
            return new ExposureZoneStateRead("Unwinding", migration);

        return new ExposureZoneStateRead("Stable", migration);
    }

    public static bool IsConditionalDistance(ExposureCardLevelInsight level)
    {
        return IsConditional(level);
    }

    private static bool IsConditional(ExposureCardLevelInsight level)
    {
        return level != null
            && level.HasDistanceFromSpot
            && Math.Abs(level.DistanceFromSpot) > GetConditionalDistanceThreshold(level);
    }

    private static double GetConditionalDistanceThreshold(ExposureCardLevelInsight level)
    {
        if (level == null || !level.HasDistanceFromSpot)
            return double.MaxValue;

        var estimatedSpot = level.Price - level.DistanceFromSpot;
        if (!IsFinite(estimatedSpot) || Math.Abs(estimatedSpot) <= PriceComparisonEpsilon)
            return MinimumConditionalDistancePoints;

        return Math.Max(MinimumConditionalDistancePoints, Math.Abs(estimatedSpot) * ConditionalDistanceSpotRatio);
    }

    private static string ResolveMigration(ExposureCardLevelInsight level, OptionExposureStrikeTrend strikeTrend)
    {
        if (level == null)
            return "Flat";

        if (strikeTrend == null || strikeTrend.SnapshotCount < 2)
            return ResolveFallbackLocationMigration(level);

        var centerChange = strikeTrend.WeightedStrikeChange;
        var threshold = GetCenterMigrationThreshold(level);
        if (Math.Abs(centerChange) <= threshold)
            return "Flat";

        if (!level.HasDistanceFromSpot || Math.Abs(level.DistanceFromSpot) <= PriceComparisonEpsilon)
            return centerChange > 0.0 ? "Higher" : "Lower";

        if (level.DistanceFromSpot > 0.0)
            return centerChange > 0.0 ? "Toward" : "Away";

        return centerChange < 0.0 ? "Toward" : "Away";
    }

    private static string ResolveFallbackLocationMigration(ExposureCardLevelInsight level)
    {
        if (level == null || !level.HasDistanceFromSpot)
            return "Flat";
        if (Math.Abs(level.DistanceFromSpot) <= GetConditionalDistanceThreshold(level))
            return "Flat";

        return level.DistanceFromSpot >= 0.0 ? "Higher" : "Lower";
    }

    private static double GetCenterMigrationThreshold(ExposureCardLevelInsight level)
    {
        if (level == null || !IsFinite(level.Price) || Math.Abs(level.Price) <= PriceComparisonEpsilon)
            return MinimumCenterMigrationThresholdPoints;

        return Math.Max(MinimumCenterMigrationThresholdPoints, Math.Abs(level.Price) * CenterMigrationThresholdRatio);
    }

    private static double GetMigrationScore(string migration)
    {
        if (string.Equals(migration, "Toward", StringComparison.OrdinalIgnoreCase))
            return 1.0;
        if (string.Equals(migration, "Away", StringComparison.OrdinalIgnoreCase))
            return -1.0;

        return 0.0;
    }

    private static double GetNotionalChangeScore(IList<double> values)
    {
        if (values == null || values.Count < 2)
            return 0.0;

        var first = Math.Abs(values[0]);
        var last = Math.Abs(values[values.Count - 1]);
        var ratio = (last - first) / Math.Max(Math.Max(first, last), 1.0);
        if (ratio >= BuildingThresholdRatio)
            return 2.0;
        if (ratio <= -BuildingThresholdRatio)
            return -2.0;
        if (ratio >= SoftTrendThresholdRatio)
            return 1.0;
        if (ratio <= -SoftTrendThresholdRatio)
            return -1.0;

        return 0.0;
    }

    private static double GetTrendSlopeScore(IList<double> values)
    {
        if (values == null || values.Count < 3)
            return 0.0;

        var count = values.Count;
        var xMean = (count - 1) / 2.0;
        var yMean = 0.0;
        var maxAbs = 1.0;
        for (var index = 0; index < count; index++)
        {
            var value = Math.Abs(values[index]);
            yMean += value;
            maxAbs = Math.Max(maxAbs, value);
        }

        yMean /= count;
        var numerator = 0.0;
        var denominator = 0.0;
        for (var index = 0; index < count; index++)
        {
            var xDelta = index - xMean;
            numerator += xDelta * (Math.Abs(values[index]) - yMean);
            denominator += xDelta * xDelta;
        }

        if (denominator <= PriceComparisonEpsilon)
            return 0.0;

        var slope = numerator / denominator;
        var spanRatio = (slope * (count - 1)) / maxAbs;
        if (spanRatio >= BuildingThresholdRatio)
            return 1.0;
        if (spanRatio <= -BuildingThresholdRatio)
            return -1.0;

        return 0.0;
    }

    private static double GetRankRotationScore(ExposureCardLevelInsight level, OptionExposureStrikeTrend strikeTrend)
    {
        if (level == null || strikeTrend == null || strikeTrend.SnapshotCount < 2)
            return 0.0;

        var wasTop = IsSameStrike(level.Strike, strikeTrend.FirstTopStrike);
        var isTop = IsSameStrike(level.Strike, strikeTrend.LatestTopStrike);
        if (isTop && !wasTop)
            return 1.0;
        if (!isTop && wasTop)
            return -1.0;
        if (isTop)
            return 0.5;

        return 0.0;
    }

    private static bool IsSameStrike(double left, double right)
    {
        return Math.Abs(left - right) <= PriceComparisonEpsilon;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
