using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Presentation;

public static partial class ExposureCardInsightBuilder
{
    private sealed class TriggerStackRead
    {
        public TriggerStackRead(
            string side,
            bool above,
            HistogramRenderLevel gammaLevel,
            HistogramRenderLevel vannaLevel,
            HistogramRenderLevel charmLevel,
            ExposurePressureStrength vannaStrength,
            ExposurePressureStrength charmStrength)
        {
            Side = side ?? string.Empty;
            Above = above;
            GammaLevel = gammaLevel;
            VannaLevel = vannaLevel;
            CharmLevel = charmLevel;
            VannaStrength = vannaStrength;
            CharmStrength = charmStrength;
        }

        public string Side { get; private set; }

        public bool Above { get; private set; }

        public HistogramRenderLevel GammaLevel { get; private set; }

        public HistogramRenderLevel VannaLevel { get; private set; }

        public HistogramRenderLevel CharmLevel { get; private set; }

        public ExposurePressureStrength VannaStrength { get; private set; }

        public ExposurePressureStrength CharmStrength { get; private set; }

        public bool HasGamma { get { return GammaLevel != null; } }

        public bool IsExpansion { get { return GammaLevel != null && GammaLevel.MetricValue < 0.0; } }

        public bool HasVannaContext { get { return VannaStrength >= ExposurePressureStrength.Moderate; } }

        public bool HasCharmContext { get { return CharmStrength >= ExposurePressureStrength.Moderate; } }

        public int ContextCount { get { return (HasVannaContext ? 1 : 0) + (HasCharmContext ? 1 : 0); } }
    }

    private static TriggerStackRead BuildTriggerStackRead(
        string side,
        bool above,
        HistogramRenderLevel gammaLevel,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels)
    {
        var vannaLevel = FindMatchingPressureLevel(vannaLevels, gammaLevel);
        var charmLevel = FindMatchingPressureLevel(charmLevels, gammaLevel);
        return new TriggerStackRead(
            side,
            above,
            gammaLevel,
            vannaLevel,
            charmLevel,
            ExposurePressureAnalyzer.GetPressureStrength(vannaLevel, vannaLevels),
            ExposurePressureAnalyzer.GetPressureStrength(charmLevel, charmLevels));
    }

    private static string BuildPressureStackHeadline(TriggerStackRead above, TriggerStackRead below)
    {
        if ((above == null || !above.HasGamma) && (below == null || !below.HasGamma))
            return "Waiting for Trigger Map";

        if (above != null && above.IsExpansion && below != null && below.IsExpansion)
            return HasHighStack(above) || HasHighStack(below) ? "Stacked Expansion" : "GEX-Led Expansion";
        if (above != null && above.HasGamma && !above.IsExpansion && below != null && below.HasGamma && !below.IsExpansion)
            return HasHighStack(above) || HasHighStack(below) ? "Pin / Magnet Risk" : "GEX-Led Pin Risk";
        if (above != null && above.IsExpansion)
            return HasHighStack(above) ? "Stacked Expansion Above" : "GEX-Led Expansion Above";
        if (below != null && below.IsExpansion)
            return HasHighStack(below) ? "Stacked Expansion Below" : "GEX-Led Expansion Below";
        if (above != null && above.HasGamma && !above.IsExpansion)
            return "Pin Risk Above";
        if (below != null && below.HasGamma && !below.IsExpansion)
            return "Pin Risk Below";

        return "Mixed / Wait";
    }

    private static bool HasHighStack(TriggerStackRead read)
    {
        return read != null && read.ContextCount >= 2;
    }

    private static string BuildPressureStackThesisText(TriggerStackRead above, TriggerStackRead below)
    {
        if ((above == null || !above.HasGamma) && (below == null || !below.HasGamma))
            return "Primary: wait for mapped GEX trigger zones.\nVEX: wait for same/nearby-strike IV context.\nCEX: wait for same/nearby-strike clock context.\nOverall: no pressure stack until GEX, VEX, and CEX maps are available.";

        return "GEX: "
            + BuildGexStackColumnText(above, below)
            + "; base price-response layer.\nVEX: "
            + BuildModifierStackColumnText(above, below, true)
            + "; watch IV: if IV reprices on the break, VEX can add conditional hedge pressure.\nCEX: "
            + BuildModifierStackColumnText(above, below, false)
            + "; watch time: clock pressure is light early and rises into the final hour.\nOverall: "
            + BuildOverallStackSummary(above, below);
    }

    private static string BuildGexStackColumnText(TriggerStackRead above, TriggerStackRead below)
    {
        return "Above " + BuildGexLayerText(above) + " / Below " + BuildGexLayerText(below);
    }

    private static string BuildGexLayerText(TriggerStackRead read)
    {
        if (read == null || !read.HasGamma)
            return "no trigger";

        return read.IsExpansion ? "expansion" : "pin / mean-reversion";
    }

    private static string BuildModifierStackColumnText(TriggerStackRead above, TriggerStackRead below, bool vanna)
    {
        return "Above " + BuildModifierLayerText(above, vanna) + " / Below " + BuildModifierLayerText(below, vanna);
    }

    private static string BuildModifierLayerText(TriggerStackRead read, bool vanna)
    {
        if (read == null || !read.HasGamma)
            return "no trigger";

        var strength = vanna ? read.VannaStrength : read.CharmStrength;
        if (strength >= ExposurePressureStrength.Strong)
            return vanna ? "strong if IV reprices" : "strong late-day";
        if (strength >= ExposurePressureStrength.Moderate)
            return vanna ? "active if IV reprices" : "active later";
        if (strength >= ExposurePressureStrength.Light)
            return vanna ? "light IV context" : "light clock context";

        return "light / neutral";
    }

    private static string BuildOverallStackSummary(TriggerStackRead above, TriggerStackRead below)
    {
        var dominant = ChooseDominantStackRead(above, below);
        if (dominant == null || !dominant.HasGamma)
            return "mixed stack; cleaner confirmation needed.";

        var sideText = dominant.Above ? "above" : "below";
        var acceptanceText = dominant.Above ? "above accepts" : "below accepts";
        var behavior = dominant.IsExpansion ? "continuation risk increases; avoid blind fades" : "failed-break / pin risk remains first";
        var confluence = BuildConfluenceLabel(dominant);
        var modifiers = BuildModifierConfirmationSentence(dominant);
        return sideText
            + " has "
            + confluence.ToLowerInvariant()
            + "; "
            + acceptanceText
            + ": "
            + behavior
            + modifiers;
    }

    private static TriggerStackRead ChooseDominantStackRead(TriggerStackRead above, TriggerStackRead below)
    {
        if (above == null || !above.HasGamma)
            return below;
        if (below == null || !below.HasGamma)
            return above;

        var aboveScore = GetStackPriority(above);
        var belowScore = GetStackPriority(below);
        if (Math.Abs(aboveScore - belowScore) > ScoreComparisonEpsilon)
            return aboveScore > belowScore ? above : below;

        if (below.IsExpansion && !above.IsExpansion)
            return below;
        if (above.IsExpansion && !below.IsExpansion)
            return above;

        return below;
    }

    private static double GetStackPriority(TriggerStackRead read)
    {
        if (read == null || !read.HasGamma)
            return 0.0;

        return (read.IsExpansion ? 2.0 : 1.0) + read.ContextCount;
    }

    private static string BuildConfluenceLabel(TriggerStackRead read)
    {
        if (read == null || !read.HasGamma)
            return "No trigger";

        var behavior = read.IsExpansion ? "Expansion" : "Pin Risk";
        if (read.HasVannaContext && read.HasCharmContext)
            return "High Confluence " + behavior;
        if (read.HasVannaContext)
            return "GEX + VEX " + behavior;
        if (read.HasCharmContext)
            return "GEX + CEX " + behavior;

        return "GEX-Led " + behavior;
    }

    private static string BuildModifierConfirmationSentence(TriggerStackRead read)
    {
        var text = string.Empty;
        if (read.HasVannaContext)
            text += "; IV repricing can add conditional pressure";
        if (read.HasCharmContext)
            text += "; CEX matters more late day";
        if (text.Length == 0)
            text = "; VEX/CEX context is not strong at this trigger";

        return text + ".";
    }

    private static string BuildOverallStackActionText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice)
    {
        if (!hasCurrentFuturesPrice || !HasUsableLevels(gammaLevels))
            return "Plan: wait for spot and mapped GEX/VEX/CEX trigger zones.";

        var maxAbsMetric = GetMaxAbsMetric(gammaLevels);
        var above = BuildTriggerStackRead("Above", true, FindNearestMajorLevel(gammaLevels, currentFuturesPrice, true, maxAbsMetric, true), vannaLevels, charmLevels);
        var below = BuildTriggerStackRead("Below", false, FindNearestMajorLevel(gammaLevels, currentFuturesPrice, true, maxAbsMetric, false), vannaLevels, charmLevels);
        var dominant = ChooseDominantStackRead(above, below);
        if (dominant == null || !dominant.HasGamma)
            return "Plan: mixed stack; wait for clearer acceptance/rejection.";

        var side = dominant.Above ? "above" : "below";
        return "Plan: "
            + BuildConfluenceLabel(dominant)
            + " "
            + side
            + ". If price accepts "
            + side
            + " the trigger, "
            + (dominant.IsExpansion ? "avoid blind fades; continuation risk increases" : "expect pin/fade risk unless price holds away")
            + BuildModifierConfirmationSentence(dominant).ToLowerInvariant();
    }

    private static List<ExposureCardLevelInsight> BuildOverallLevelInsights(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        int maxLevelCount)
    {
        var result = new List<ExposureCardLevelInsight>();
        var levelCandidates = FilterLevelsNearSpot(gammaLevels, currentFuturesPrice, hasCurrentFuturesPrice, NearSpotFilterDistanceRatio);
        var count = Math.Min(maxLevelCount <= 0 ? DefaultCardLevelCount : maxLevelCount, levelCandidates.Count);
        for (var index = 0; index < count; index++)
        {
            var level = levelCandidates[index];
            if (level == null)
                continue;

            var hasDistance = hasCurrentFuturesPrice;
            var distance = hasDistance ? level.Price - currentFuturesPrice : 0.0;
            var isAboveSpot = hasCurrentFuturesPrice && level.Price >= currentFuturesPrice;
            result.Add(new ExposureCardLevelInsight(
                level.Strike,
                level.Price,
                level.MetricValue,
                distance,
                hasDistance,
                FormatPrice(level.Strike),
                FormatPrice(level.Price),
                ExposureCardTextFormatter.FormatSignedCompactExposure(level.MetricValue),
                FormatDistance(distance, hasDistance),
                BuildOverallLevelLabel(level, isAboveSpot, vannaLevels, charmLevels),
                level.MetricValue >= 0.0,
                level.Intensity));
        }

        return result;
    }

    private static string BuildOverallLevelLabel(
        HistogramRenderLevel gammaLevel,
        bool isAboveSpot,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels)
    {
        if (gammaLevel == null)
            return string.Empty;

        var read = BuildTriggerStackRead("Strike", isAboveSpot, gammaLevel, vannaLevels, charmLevels);
        var role = gammaLevel.MetricValue < 0.0 ? "Expansion zone" : "Pin / failed-break zone";
        if (read.HasVannaContext && read.HasCharmContext)
            role = gammaLevel.MetricValue < 0.0 ? "Stacked expansion zone" : "Stacked pin zone";
        else if (read.HasVannaContext)
            role = gammaLevel.MetricValue < 0.0 ? "IV-sensitive expansion" : "Vol-reactive pin zone";
        else if (read.HasCharmContext)
            role = gammaLevel.MetricValue < 0.0 ? "Clock-sensitive expansion" : "Clock-sensitive pin zone";

        return role
            + "|"
            + (gammaLevel.MetricValue < 0.0 ? "-GEX" : "+GEX")
            + " / "
            + BuildCompactModifierText("VEX", read.VannaStrength, false)
            + " / "
            + BuildCompactModifierText("CEX", read.CharmStrength, true);
    }

    private static string BuildCompactModifierText(string label, ExposurePressureStrength strength, bool isCharm)
    {
        if (strength >= ExposurePressureStrength.Strong)
            return isCharm ? "strong " + label + " late" : "strong " + label;
        if (strength >= ExposurePressureStrength.Moderate)
            return isCharm ? label + " late" : label + " context";
        if (strength >= ExposurePressureStrength.Light)
            return "light " + label;

        return "light " + label;
    }

    private static HistogramRenderLevel FindMatchingPressureLevel(IList<HistogramRenderLevel> levels, HistogramRenderLevel target)
    {
        if (levels == null || target == null)
            return null;

        HistogramRenderLevel match = null;
        var bestDistance = double.MaxValue;
        var tolerance = Math.Max(PriceMatchMinimumTolerance, target.PriceBand <= 0.0 ? PriceMatchMinimumTolerance : target.PriceBand);
        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            if (level == null)
                continue;

            var distance = Math.Abs(level.Price - target.Price);
            if (distance > tolerance || distance >= bestDistance)
                continue;

            match = level;
            bestDistance = distance;
        }

        return match;
    }

}
