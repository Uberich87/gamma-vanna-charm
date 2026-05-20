using System;
using System.Collections.Generic;
using System.Globalization;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static partial class ExposureCardInsightBuilder
{
    private static ExposureCardThesisState BuildThesisState(
        IList<HistogramRenderLevel> renderLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        HistogramRenderLevel nearestAbove,
        HistogramRenderLevel nearestBelow,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice || !HasUsableLevels(renderLevels) || totalAbs <= 0.0)
            return ExposureCardThesisState.Waiting;

        var nearest = GetNearestOfTwo(nearestAbove, nearestBelow, currentFuturesPrice);
        if (nearest != null && Math.Abs(nearest.Price - currentFuturesPrice) <= NearSpotPoints)
            return nearest.MetricValue >= 0.0 ? ExposureCardThesisState.Positive : ExposureCardThesisState.Negative;

        var aboveSign = GetBalanceSign(aboveBalance, totalAbs);
        var belowSign = GetBalanceSign(belowBalance, totalAbs);
        if (aboveSign > 0 && belowSign > 0)
            return ExposureCardThesisState.Positive;
        if (aboveSign < 0 && belowSign < 0)
            return ExposureCardThesisState.Negative;
        if ((aboveSign > 0 && belowSign < 0) || (aboveSign < 0 && belowSign > 0))
            return ExposureCardThesisState.Mixed;
        if (aboveSign > 0 || belowSign > 0)
            return ExposureCardThesisState.Positive;
        if (aboveSign < 0 || belowSign < 0)
            return ExposureCardThesisState.Negative;

        return ExposureCardThesisState.Waiting;
    }

    private static string BuildRegimeText(IList<HistogramRenderLevel> renderLevels, OptionExposureMetric exposureMetric)
    {
        if (renderLevels == null || renderLevels.Count == 0)
            return "No exposure levels";

        if (exposureMetric == OptionExposureMetric.Vanna)
            return "VEX: IV-Driven Hedge Pressure";
        if (exposureMetric == OptionExposureMetric.Charm)
            return "CEX: Time-Decay / Late-Session Pressure";

        var positiveAbs = 0.0;
        var negativeAbs = 0.0;
        for (var index = 0; index < renderLevels.Count; index++)
        {
            var value = renderLevels[index].MetricValue;
            if (value >= 0.0)
                positiveAbs += Math.Abs(value);
            else
                negativeAbs += Math.Abs(value);
        }

        if (positiveAbs <= 0.0 && negativeAbs <= 0.0)
            return "No Clear GEX Regime";
        if (positiveAbs >= negativeAbs * DirectionalDominanceRatio)
            return "Positive GEX: Pin / Mean-Reversion Regime";
        if (negativeAbs >= positiveAbs * DirectionalDominanceRatio)
            return "Negative GEX: Expansion / Momentum Regime";

        return "Mixed GEX: Wait for Confirmation";
    }

    private static string BuildMetricThesisText(
        OptionExposureMetric exposureMetric,
        HistogramRenderLevel nearestAbove,
        HistogramRenderLevel nearestBelow,
        IList<HistogramRenderLevel> renderLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (exposureMetric == OptionExposureMetric.Vanna)
            return BuildMetricPressureMapText("VEX", renderLevels, currentFuturesPrice, hasCurrentFuturesPrice, aboveBalance, belowBalance, totalAbs);
        if (exposureMetric == OptionExposureMetric.Charm)
            return BuildMetricPressureMapText("CEX", renderLevels, currentFuturesPrice, hasCurrentFuturesPrice, aboveBalance, belowBalance, totalAbs);

        if (nearestAbove != null && nearestAbove.MetricValue >= 0.0 && nearestBelow != null && nearestBelow.MetricValue < 0.0)
            return "GEX: mean-reversion risk above; expansion risk below.";
        if (nearestAbove != null && nearestAbove.MetricValue >= 0.0)
            return "GEX: overhead +GEX favors failed rallies/pinning unless accepted.";
        if (nearestBelow != null && nearestBelow.MetricValue >= 0.0)
            return "GEX: below-spot +GEX favors failed dips/pinning unless accepted.";
        if ((nearestAbove != null && nearestAbove.MetricValue < 0.0) || (nearestBelow != null && nearestBelow.MetricValue < 0.0))
            return "GEX: nearby -GEX can support expansion after acceptance.";

        return "Mixed GEX; trade level-to-level and wait for confirmation.";
    }

    private static string BuildOverallRegimeText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        HistogramRenderLevel nearestAbove,
        HistogramRenderLevel nearestBelow,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice || !HasUsableLevels(gammaLevels))
            return "Waiting for Trigger Map";

        var above = BuildTriggerStackRead("Above", true, nearestAbove, vannaLevels, charmLevels);
        var below = BuildTriggerStackRead("Below", false, nearestBelow, vannaLevels, charmLevels);
        return BuildPressureStackHeadline(above, below);
    }

    private static string BuildOverallThesisText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        HistogramRenderLevel nearestAbove,
        HistogramRenderLevel nearestBelow,
        double aboveBalance,
        double belowBalance,
        double totalAbs)
    {
        if (!hasCurrentFuturesPrice || !HasUsableLevels(gammaLevels))
            return BuildPressureStackThesisText(null, null);

        var above = BuildTriggerStackRead("Above", true, nearestAbove, vannaLevels, charmLevels);
        var below = BuildTriggerStackRead("Below", false, nearestBelow, vannaLevels, charmLevels);
        return BuildPressureStackThesisText(above, below);
    }

}
