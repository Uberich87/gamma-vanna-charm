using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static partial class ExposureCardInsightBuilder
{
    private static string BuildOverallActionText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        bool usesFlowImpliedDealerModel)
    {
        return BuildOverallStackActionText(gammaLevels, vannaLevels, charmLevels, currentFuturesPrice, hasCurrentFuturesPrice);
    }

    private static string BuildOverallWatchText(
        IList<HistogramRenderLevel> gammaLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        HistogramRenderLevel nearestAbove,
        HistogramRenderLevel nearestBelow)
    {
        if (!hasCurrentFuturesPrice || !HasUsableLevels(gammaLevels))
            return "Wait for spot and mapped GEX levels.";
        if (nearestAbove != null && nearestBelow != null)
            return "Use " + FormatPrice(nearestAbove.Price) + " / " + FormatPrice(nearestBelow.Price) + " as trigger zones; wait for acceptance or rejection.";
        if (nearestAbove != null)
            return "Use " + FormatPrice(nearestAbove.Price) + " above spot as a trigger zone; wait for acceptance or rejection.";
        if (nearestBelow != null)
            return "Use " + FormatPrice(nearestBelow.Price) + " below spot as a trigger zone; wait for acceptance or rejection.";

        return "Use highlighted GEX levels as trigger zones, not predictions.";
    }

    private static string BuildPressureMapText(
        string label,
        string exposureLabel,
        IList<HistogramRenderLevel> levels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice)
    {
        return BuildPressureMapText(
            label,
            exposureLabel,
            levels,
            currentFuturesPrice,
            hasCurrentFuturesPrice,
            CalculateBalance(levels, currentFuturesPrice, hasCurrentFuturesPrice, true),
            CalculateBalance(levels, currentFuturesPrice, hasCurrentFuturesPrice, false),
            SumAbsMetric(levels));
    }

    private static string BuildPressureMapText(
        string label,
        string exposureLabel,
        IList<HistogramRenderLevel> levels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice)
            return label + ": wait for spot.";
        if (!HasUsableLevels(levels) || totalAbs <= 0.0)
            return label + ": no mapped pressure.";

        return label
            + ": "
            + BuildSidePressureText("above", exposureLabel, aboveBalance, totalAbs)
            + " / "
            + BuildSidePressureText("below", exposureLabel, belowBalance, totalAbs)
            + "; "
            + BuildPressureWeightText(label, aboveBalance, belowBalance);
    }

    private static string BuildSidePressureText(string side, string exposureLabel, double balance, double totalAbs)
    {
        var sign = GetBalanceSign(balance, totalAbs);
        if (sign > 0)
            return "+" + exposureLabel + " " + side;
        if (sign < 0)
            return "-" + exposureLabel + " " + side;

        return "mixed " + exposureLabel + " " + side;
    }

    private static string BuildPressureWeightText(string label, double aboveBalance, double belowBalance)
    {
        var absAbove = Math.Abs(aboveBalance);
        var absBelow = Math.Abs(belowBalance);
        var context = label.IndexOf("CEX", StringComparison.OrdinalIgnoreCase) >= 0 ? "time-decay sensitivity" : "IV sensitivity";
        if (absAbove <= 0.0 && absBelow <= 0.0)
            return context + " is flat.";
        if (absAbove >= absBelow * DirectionalDominanceRatio)
            return context + " is concentrated above.";
        if (absBelow >= absAbove * DirectionalDominanceRatio)
            return context + " is concentrated below.";

        return "mixed exposure; cleaner confirmation needed.";
    }

    private static string BuildMetricPressureMapText(
        string exposureLabel,
        IList<HistogramRenderLevel> levels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice)
            return "Wait for spot.";
        if (!HasUsableLevels(levels) || totalAbs <= 0.0)
            return "No mapped pressure.";

        return BuildSidePressureText("above", exposureLabel, aboveBalance, totalAbs)
            + " / "
            + BuildSidePressureText("below", exposureLabel, belowBalance, totalAbs)
            + "; "
            + BuildPressureWeightText(exposureLabel, aboveBalance, belowBalance);
    }

    private static string BuildPressureActionText(
        string label,
        string exposureLabel,
        IList<HistogramRenderLevel> levels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice)
            return "Wait for spot.";
        if (!HasUsableLevels(levels) || totalAbs <= 0.0)
            return "No mapped pressure.";

        var absAbove = Math.Abs(aboveBalance);
        var absBelow = Math.Abs(belowBalance);
        if (absAbove <= 0.0 && absBelow <= 0.0)
            return "Flat; no read from this tab.";
        if (absAbove >= absBelow * DirectionalDominanceRatio)
            return BuildDirectionalPressureActionText(label, exposureLabel, "above", "upside", aboveBalance, totalAbs);
        if (absBelow >= absAbove * DirectionalDominanceRatio)
            return BuildDirectionalPressureActionText(label, exposureLabel, "below", "downside", belowBalance, totalAbs);

        return "Mixed exposure; use only as context and keep GEX primary.";
    }

    private static string BuildDirectionalPressureActionText(string label, string exposureLabel, string side, string direction, double balance, double totalAbs)
    {
        if (label == "CEX")
        {
            return "Time-decay hedge sensitivity is concentrated "
                + side
                + " ("
                + BuildSignedExposureLabel(exposureLabel, balance, totalAbs)
                + "); most useful late-session/0DTE; keep GEX primary.";
        }

        return "IV-driven hedge sensitivity is concentrated "
            + side
            + " ("
            + BuildSignedExposureLabel(exposureLabel, balance, totalAbs)
            + "); use with IV repricing and keep GEX primary.";
    }

    private static string BuildSignedExposureLabel(string exposureLabel, double balance, double totalAbs)
    {
        var sign = GetBalanceSign(balance, totalAbs);
        if (sign > 0)
            return "+" + exposureLabel;
        if (sign < 0)
            return "-" + exposureLabel;

        return "mixed " + exposureLabel;
    }

    private static string BuildPressureAgreementText(
        string label,
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> pressureLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice)
    {
        if (!hasCurrentFuturesPrice)
            return label + ": wait.";
        if (!HasUsableLevels(pressureLevels))
            return label + ": no map.";

        var isCex = label.IndexOf("CEX", StringComparison.OrdinalIgnoreCase) >= 0;
        var context = isCex ? "time-decay sensitivity" : "IV sensitivity";
        var suffix = isCex ? "; late-session/0DTE context." : "; use with IV repricing.";
        var absAbove = Math.Abs(CalculateBalance(pressureLevels, currentFuturesPrice, true, true));
        var absBelow = Math.Abs(CalculateBalance(pressureLevels, currentFuturesPrice, true, false));
        if (absAbove <= 0.0 && absBelow <= 0.0)
            return label + ": no concentration.";
        if (absAbove >= absBelow * DirectionalDominanceRatio)
            return label + ": " + context + " concentrated above" + suffix;
        if (absBelow >= absAbove * DirectionalDominanceRatio)
            return label + ": " + context + " concentrated below" + suffix;

        return label + ": mixed context; do not trade alone.";
    }

    private static HistogramRenderLevel GetNearestOfTwo(HistogramRenderLevel first, HistogramRenderLevel second, double currentFuturesPrice)
    {
        if (first == null)
            return second;
        if (second == null)
            return first;

        return Math.Abs(first.Price - currentFuturesPrice) <= Math.Abs(second.Price - currentFuturesPrice) ? first : second;
    }
}
