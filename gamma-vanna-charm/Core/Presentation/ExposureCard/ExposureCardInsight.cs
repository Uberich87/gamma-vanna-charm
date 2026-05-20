using System;
using System.Collections.Generic;
using System.Globalization;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public enum ExposureCardThesisState
{
    Waiting,
    Positive,
    Negative,
    Mixed
}

public sealed class ExposureCardInsight
{
    public ExposureCardInsight(
        string regimeText,
        ExposureCardThesisState thesisState,
        string thesisText,
        string actionText,
        string watchText,
        string nearestAboveText,
        string nearestBelowText,
        string aboveBalanceText,
        string belowBalanceText,
        string balanceExplanationText,
        IList<ExposureCardLevelInsight> levels)
    {
        RegimeText = regimeText ?? string.Empty;
        ThesisState = thesisState;
        ThesisText = thesisText ?? string.Empty;
        ActionText = actionText ?? string.Empty;
        WatchText = watchText ?? string.Empty;
        NearestAboveText = nearestAboveText ?? string.Empty;
        NearestBelowText = nearestBelowText ?? string.Empty;
        AboveBalanceText = aboveBalanceText ?? string.Empty;
        BelowBalanceText = belowBalanceText ?? string.Empty;
        BalanceExplanationText = balanceExplanationText ?? string.Empty;
        Levels = new List<ExposureCardLevelInsight>(levels ?? new List<ExposureCardLevelInsight>()).AsReadOnly();
    }

    public string RegimeText { get; private set; }

    public ExposureCardThesisState ThesisState { get; private set; }

    public string ThesisText { get; private set; }

    public string ActionText { get; private set; }

    public string WatchText { get; private set; }

    public string NearestAboveText { get; private set; }

    public string NearestBelowText { get; private set; }

    public string AboveBalanceText { get; private set; }

    public string BelowBalanceText { get; private set; }

    public string BalanceExplanationText { get; private set; }

    public IReadOnlyList<ExposureCardLevelInsight> Levels { get; private set; }
}

public sealed class ExposureCardLevelInsight
{
    public ExposureCardLevelInsight(
        double strike,
        double price,
        double metricValue,
        double distanceFromSpot,
        bool hasDistanceFromSpot,
        string strikeText,
        string priceText,
        string exposureText,
        string distanceText,
        string labelText,
        bool isPositive,
        double intensity)
    {
        Strike = strike;
        Price = price;
        MetricValue = metricValue;
        DistanceFromSpot = distanceFromSpot;
        HasDistanceFromSpot = hasDistanceFromSpot;
        StrikeText = strikeText ?? string.Empty;
        PriceText = priceText ?? string.Empty;
        ExposureText = exposureText ?? string.Empty;
        DistanceText = distanceText ?? string.Empty;
        LabelText = labelText ?? string.Empty;
        IsPositive = isPositive;
        Intensity = intensity;
    }

    public double Strike { get; private set; }

    public double Price { get; private set; }

    public double MetricValue { get; private set; }

    public double DistanceFromSpot { get; private set; }

    public bool HasDistanceFromSpot { get; private set; }

    public string StrikeText { get; private set; }

    public string PriceText { get; private set; }

    public string ExposureText { get; private set; }

    public string DistanceText { get; private set; }

    public string LabelText { get; private set; }

    public bool IsPositive { get; private set; }

    public double Intensity { get; private set; }
}
