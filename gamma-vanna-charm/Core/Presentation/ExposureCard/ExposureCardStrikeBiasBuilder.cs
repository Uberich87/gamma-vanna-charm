using System;
using System.Collections.Generic;
using System.Globalization;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureCardStrikeBiasBuilder
{
    private const double StrongBiasScoreThreshold = 1.25;
    private const double LevelMatchMinimumTolerance = 5.0;
    private const double HeavyCharmWeightThreshold = 0.55;
    private const double MediumCharmWeightThreshold = 0.30;
    private const double FinalHourCharmWeight = 0.75;
    private const double MidAfternoonCharmWeight = 0.55;
    private const double MiddayCharmWeight = 0.35;
    private const double EarlySessionCharmWeight = 0.18;
    private const int FinalHourStartMinutes = (15 * 60) + 30;
    private const int MidAfternoonStartMinutes = (14 * 60) + 30;
    private const int MiddayStartMinutes = 12 * 60;

    public static string Build(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double currentFuturesPrice,
        bool hasCurrentFuturesPrice,
        DateTime timestampUtc)
    {
        if (!hasCurrentFuturesPrice || currentFuturesPrice <= 0.0 || gammaLevels == null || gammaLevels.Count == 0)
            return "Dealer response waiting for spot and mapped GEX/VEX/CEX levels.";

        var above = FindNearestLevel(gammaLevels, currentFuturesPrice, true);
        var below = FindNearestLevel(gammaLevels, currentFuturesPrice, false);
        if (above == null && below == null)
            return "Dealer response waiting for levels around spot.";

        var cexWeight = GetCharmWeight(timestampUtc);
        var parts = new List<string>();
        if (above != null)
            parts.Add(BuildScenario("Above", above, true, vannaLevels, charmLevels, cexWeight));
        if (below != null)
            parts.Add(BuildScenario("Below", below, false, vannaLevels, charmLevels, cexWeight));

        return string.Join("\n", parts.ToArray());
    }

    private static string BuildScenario(
        string side,
        HistogramRenderLevel gammaLevel,
        bool above,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels,
        double cexWeight)
    {
        var vannaLevel = FindMatchingLevel(vannaLevels, gammaLevel);
        var charmLevel = FindMatchingLevel(charmLevels, gammaLevel);
        var vannaStrength = ExposurePressureAnalyzer.GetPressureStrength(vannaLevel, vannaLevels);
        var charmStrength = ExposurePressureAnalyzer.GetPressureStrength(charmLevel, charmLevels);
        var score = GetGammaScore(gammaLevel.MetricValue, above);

        return side
            + " "
            + FormatPrice(gammaLevel.Price)
            + ": "
            + GetConfluenceLabel(gammaLevel.MetricValue, vannaStrength, charmStrength, above)
            + " — "
            + "GEX: "
            + GetGammaPhrase(gammaLevel.MetricValue, above)
            + "; "
            + "VEX: "
            + GetPressurePhrase(vannaStrength, gammaLevel.MetricValue)
            + "; "
            + "CEX: "
            + GetCharmPhrase(charmStrength, cexWeight)
            + "; "
            + "Trader read: "
            + GetTraderRead(score, vannaStrength, charmStrength, above)
            + ".";
    }

    private static double GetGammaScore(double value, bool above)
    {
        if (value >= 0.0)
            return above ? -2.0 : 2.0;

        return above ? 2.0 : -2.0;
    }

    private static string GetBiasLabel(double score, bool above)
    {
        if (above)
        {
            if (score >= StrongBiasScoreThreshold)
                return "dealers may amplify moves above acceptance";
            if (score <= -StrongBiasScoreThreshold)
                return "pin/mean-reversion risk first";

            return "mixed exposure; cleaner confirmation needed";
        }

        if (score >= StrongBiasScoreThreshold)
            return "pin/mean-reversion risk first";
        if (score <= -StrongBiasScoreThreshold)
            return "dealers may amplify moves below acceptance";

        return "mixed exposure; cleaner confirmation needed";
    }

    private static string GetGammaPhrase(double value, bool above)
    {
        if (value >= 0.0)
            return above ? "+GEX suppress/pin above" : "+GEX suppress/pin below";

        return above ? "-GEX may amplify upside" : "-GEX may amplify downside";
    }

    private static string GetConfluenceLabel(double gammaValue, ExposurePressureStrength vannaStrength, ExposurePressureStrength charmStrength, bool above)
    {
        var behavior = gammaValue < 0.0 ? (above ? "Expansion Above" : "Expansion Below") : "Pin Risk";
        var hasVanna = vannaStrength >= ExposurePressureStrength.Moderate;
        var hasCharm = charmStrength >= ExposurePressureStrength.Moderate;
        if (hasVanna && hasCharm)
            return "High Confluence " + behavior;
        if (hasVanna)
            return "GEX + VEX " + behavior;
        if (hasCharm)
            return "GEX + CEX " + behavior;

        return "GEX-Led " + behavior;
    }

    private static string GetTraderRead(double score, ExposurePressureStrength vannaStrength, ExposurePressureStrength charmStrength, bool above)
    {
        var baseRead = GetBiasLabel(score, above);
        var hasVanna = vannaStrength >= ExposurePressureStrength.Moderate;
        var hasCharm = charmStrength >= ExposurePressureStrength.Moderate;
        if (hasVanna && hasCharm)
            return baseRead + "; same-strike VEX/CEX increase reaction risk";
        if (hasVanna)
            return baseRead + "; IV repricing can add conditional pressure";
        if (hasCharm)
            return baseRead + "; late-day clock pressure can add conditional pressure";

        return baseRead + "; cleaner confirmation needed because VEX/CEX context is weak";
    }

    private static string GetPressurePhrase(ExposurePressureStrength strength, double gammaValue)
    {
        if (strength >= ExposurePressureStrength.Moderate)
        {
            if (gammaValue < 0.0)
                return "watch IV: if IV reprices on the break, VEX can add conditional hedge pressure";

            return "watch IV: IV repricing can make this pin zone more reactive";
        }

        if (strength == ExposurePressureStrength.Light)
            return "light same-strike IV context; move is mostly price-led unless IV confirms";

        return "weak same-strike VEX context; move is mostly price-led";
    }

    private static string GetCharmPhrase(ExposurePressureStrength strength, double cexWeight)
    {
        var weightText = cexWeight >= HeavyCharmWeightThreshold ? "heavy late-day" : cexWeight >= MediumCharmWeightThreshold ? "medium" : "light early";
        if (strength >= ExposurePressureStrength.Moderate)
            return "watch time: pressure rises if spot holds near this strike into the close (" + weightText + ")";
        if (strength == ExposurePressureStrength.Light)
            return "light clock context; more useful later (" + weightText + ")";

        return "weak CEX context (" + weightText + ")";
    }

    private static HistogramRenderLevel FindNearestLevel(IList<HistogramRenderLevel> levels, double currentFuturesPrice, bool above)
    {
        HistogramRenderLevel nearest = null;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            if (level == null)
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

    private static HistogramRenderLevel FindMatchingLevel(IList<HistogramRenderLevel> levels, HistogramRenderLevel target)
    {
        if (levels == null || target == null)
            return null;

        HistogramRenderLevel match = null;
        var bestDistance = double.MaxValue;
        var tolerance = Math.Max(LevelMatchMinimumTolerance, target.PriceBand <= 0.0 ? LevelMatchMinimumTolerance : target.PriceBand);
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

    private static double GetCharmWeight(DateTime timestampUtc)
    {
        var easternTime = ToUsEastern(timestampUtc == DateTime.MinValue ? DateTime.UtcNow : timestampUtc);
        var minutes = (easternTime.Hour * 60) + easternTime.Minute;
        if (minutes >= FinalHourStartMinutes)
            return FinalHourCharmWeight;
        if (minutes >= MidAfternoonStartMinutes)
            return MidAfternoonCharmWeight;
        if (minutes >= MiddayStartMinutes)
            return MiddayCharmWeight;

        return EarlySessionCharmWeight;
    }

    private static DateTime ToUsEastern(DateTime timestampUtc)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Local
            ? timestampUtc.ToUniversalTime()
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);
        var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(utc, easternZone);
    }

    private static string FormatPrice(double price)
    {
        return price.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
