#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal sealed partial class ExposureCardContentView
{
    private static readonly TimeZoneInfo CharmMarketTimeZone = FindCharmMarketTimeZone();
    private static UIElement BuildMetricLensZonesBody(
        ExposureCardInsight insight,
        OptionExposureMetric metric,
        IList<ExposureCardSparklineModel> sparklines,
        OptionExposureStrikeTrend strikeTrend,
        string ivConditionText,
        IList<ExposureCardStrikeSparklineModel> strikeSparklines,
        Brush positiveBrush,
        Brush negativeBrush)
    {
        var panel = CreateSectionPanel();
        if (insight == null)
            return panel;

        panel.Children.Add(new TextBlock
        {
            Text = GetMetricLensTitle(metric),
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 18.0,
            FontWeight = FontWeights.Bold,
            LineHeight = 23.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 2.0, 0.0, 5.0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = GetMetricLensPurpose(metric),
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.5,
            LineHeight = 15.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
        });
        panel.Children.Add(CreateMetricLensScoreBlock(insight, metric, sparklines, ivConditionText, positiveBrush, negativeBrush));
        panel.Children.Add(CreateMutedText("Zone cards show the top 5 nearby " + ExposureMetricPresentation.GetAbbreviation(metric) + " zones within ±5% of spot."));

        if (insight.Levels == null || insight.Levels.Count == 0)
        {
            panel.Children.Add(CreateMutedText("No " + ExposureMetricPresentation.GetAbbreviation(metric) + " zones within 5% of spot."));
            return panel;
        }

        for (var index = 0; index < insight.Levels.Count; index++)
            panel.Children.Add(CreateMetricLensZoneRow(insight.Levels[index], metric, GetStrikeSparklineValues(strikeSparklines, insight.Levels[index]), strikeTrend, ivConditionText, positiveBrush, negativeBrush));

        return panel;
    }

    private static Border CreateMetricLensScoreBlock(ExposureCardInsight insight, OptionExposureMetric metric, IList<ExposureCardSparklineModel> sparklines, string ivConditionText, Brush positiveBrush, Brush negativeBrush)
    {
        var aboveValue = ParseBalanceExposureValue(insight != null ? insight.AboveBalanceText : null);
        var belowValue = ParseBalanceExposureValue(insight != null ? insight.BelowBalanceText : null);
        var dominantBelow = Math.Abs(belowValue) >= Math.Abs(aboveValue);
        var abbreviation = ExposureMetricPresentation.GetAbbreviation(metric);

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(CreateSubsectionHeading(GetMetricLensScoreHeading(metric), ExposureCardPalette.MutedBrush));

        var scoreGrid = new Grid { Margin = new Thickness(0.0, 2.0, 0.0, 8.0) };
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        if (metric == OptionExposureMetric.Gamma)
        {
            scoreGrid.Children.Add(CreateRegimeScoreColumn("Regime", BuildGexRegimeLabel(aboveValue, belowValue), GetGexRegimeBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 0));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("Skew", BuildGexSkewLabel(aboveValue, belowValue), GetGexSkewBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 1));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("Intensity", EstimateGexIntensityLabel(sparklines, Math.Abs(aboveValue) + Math.Abs(belowValue)), ExposureCardPalette.TextBrush, 2));
        }
        else if (metric == OptionExposureMetric.Vanna)
        {
            scoreGrid.Children.Add(CreateRegimeScoreColumn("VEX Skew", BuildMetricSkewLabel("VEX", aboveValue, belowValue), GetGexSkewBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 0));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("IV Sensitivity", EstimateMetricIntensityLabel(sparklines, "VEX", Math.Abs(aboveValue) + Math.Abs(belowValue)), ExposureCardPalette.TextBrush, 1));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("IV Condition", NormalizeIvConditionText(ivConditionText), GetIvConditionBrush(ivConditionText, positiveBrush, negativeBrush), 2));
        }
        else
        {
            scoreGrid.Children.Add(CreateRegimeScoreColumn("CEX Skew", BuildMetricSkewLabel("CEX", aboveValue, belowValue), GetGexSkewBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 0));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("Time Weight", GetCharmTimeWeightLabel(DateTime.UtcNow), ExposureCardPalette.BodyBrush, 1));
            scoreGrid.Children.Add(CreateRegimeScoreColumn("Clock Sens.", EstimateMetricIntensityLabel(sparklines, "CEX", Math.Abs(aboveValue) + Math.Abs(belowValue)), ExposureCardPalette.TextBrush, 2));
        }

        panel.Children.Add(scoreGrid);

        var netGrid = new Grid();
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.Children.Add(CreateRegimeNetCell(FormatNetMetricLabel(insight != null ? insight.AboveBalanceText : null, "Above", aboveValue, abbreviation), 0, GetGexValueBrush(aboveValue, positiveBrush, negativeBrush)));
        netGrid.Children.Add(CreateRegimeNetCell(FormatNetMetricLabel(insight != null ? insight.BelowBalanceText : null, "Below", belowValue, abbreviation), 1, GetGexValueBrush(belowValue, positiveBrush, negativeBrush)));
        netGrid.Children.Add(CreateRegimeNetCell(FormatSkewRatioText(CalculateSkewRatio(aboveValue, belowValue), dominantBelow), 2, ExposureCardPalette.BodyBrush));
        panel.Children.Add(netGrid);

        return new Border
        {
            BorderBrush = ExposureCardPalette.BorderBrush,
            BorderThickness = new Thickness(1.0),
            Background = ExposureCardPalette.SectionBrush,
            CornerRadius = new CornerRadius(7.0),
            Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
            Padding = new Thickness(10.0, 9.0, 10.0, 9.0),
            Child = panel
        };
    }

    private static Border CreateMetricLensZoneRow(ExposureCardLevelInsight level, OptionExposureMetric metric, IList<double> sparklineValues, OptionExposureStrikeTrend strikeTrend, string ivConditionText, Brush positiveBrush, Brush negativeBrush)
    {
        var isPositive = level != null && level.IsPositive;
        var accentBrush = isPositive ? positiveBrush ?? ExposureCardPalette.PositiveBrush : negativeBrush ?? ExposureCardPalette.NegativeBrush;
        var stateRead = ResolveZoneStateRead(level, sparklineValues, strikeTrend);
        var state = stateRead.StateText;
        var detailLabel = GetMetricLensDetailLabel(metric);
        var detailValue = GetMetricLensDetailValue(metric, level, state, stateRead.MigrationText, ivConditionText);
        var roleStack = BuildMetricLensRoleStack(level, metric);

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10.0, 9.0, 10.0, 8.0)
        };

        var header = new Grid { Margin = new Thickness(0.0, 0.0, 0.0, 7.0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var zoneStack = new StackPanel { Orientation = Orientation.Vertical };
        zoneStack.Children.Add(new TextBlock
        {
            Text = "ZONE",
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        });
        zoneStack.Children.Add(new TextBlock
        {
            Text = level != null ? level.PriceText : "--",
            Foreground = accentBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 19.0,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(zoneStack, 0);
        header.Children.Add(zoneStack);

        var pillStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        pillStack.Children.Add(CreateZonePill(state, accentBrush));
        Grid.SetColumn(pillStack, 1);
        header.Children.Add(pillStack);
        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = roleStack[0],
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 16.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = roleStack[1],
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.0,
            LineHeight = 15.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 0.0, 0.0, 5.0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = BuildMetricLensTraderRead(level, metric, state, ivConditionText),
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 15.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
        });

        var metrics = new Grid { Margin = new Thickness(0.0, 0.0, 0.0, 8.0) };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        metrics.Children.Add(CreateZoneMetricBlock("Notional", level != null ? level.ExposureText : "--", accentBrush, 0));
        metrics.Children.Add(CreateZoneMetricBlock("Δ " + ExposureMetricPresentation.GetAbbreviation(metric), FormatSparklineNotionalChange(sparklineValues), ExposureCardPalette.BrandAccentBrush, 1));
        metrics.Children.Add(CreateZoneMetricBlock(detailLabel, detailValue, ExposureCardPalette.BodyBrush, 2));
        panel.Children.Add(metrics);
        panel.Children.Add(CreateTradingZoneTrendRow(sparklineValues, accentBrush));

        return new Border
        {
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(1.0),
            Background = ExposureCardPalette.BalanceBoxBrush,
            CornerRadius = new CornerRadius(6.0),
            Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
            Child = panel
        };
    }

    private static string GetMetricLensTitle(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
            return "VANNA IV REPRICING";
        if (metric == OptionExposureMetric.Charm)
            return "CHARM CLOCK PRESSURE";

        return "GAMMA PRICE RESPONSE";
    }

    private static string GetMetricLensPurpose(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
            return "Vanna measures delta sensitivity to IV changes. Use this tab to see where IV repricing may add hedge pressure near a zone.";
        if (metric == OptionExposureMetric.Charm)
            return "Charm measures delta sensitivity to time passing. Use this tab to see where clock pressure may matter, especially later in the session.";

        return "Gamma measures delta sensitivity to price movement. Use this tab to see where dealer hedging may suppress or amplify accepted moves.";
    }

    private static string GetMetricLensScoreHeading(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
            return "VEX CONTEXT";
        if (metric == OptionExposureMetric.Charm)
            return "CEX CONTEXT";

        return "GEX REGIME";
    }

    private static string[] BuildMetricLensRoleStack(ExposureCardLevelInsight level, OptionExposureMetric metric)
    {
        var sign = level != null && level.IsPositive ? "+" : "-";
        var abbreviation = ExposureMetricPresentation.GetAbbreviation(metric);
        var stack = sign + abbreviation;
        if (level == null)
            return new[] { "Waiting for zone", stack };

        var far = ExposureZoneStateAnalyzer.IsConditionalDistance(level);
        if (metric == OptionExposureMetric.Vanna)
        {
            var strength = BuildLevelStrengthText(level);
            var role = far ? "Conditional IV Zone" : strength + " IV-Sensitive Zone";
            return new[] { role, stack + " / " + strength + " IV sensitivity" };
        }

        if (metric == OptionExposureMetric.Charm)
        {
            var strength = BuildLevelStrengthText(level);
            var role = far ? "Conditional Late-Day Zone" : strength + " Clock-Pressure Zone";
            return new[] { role, stack + " / " + strength + " CEX / " + GetCharmTimeWeightLabel(DateTime.UtcNow) };
        }

        if (far)
            return new[] { "Conditional Gamma Cluster", stack };
        if (level.IsPositive)
            return new[] { "Pin / Suppression Zone", stack };

        return new[] { "Expansion Trigger", stack };
    }

    private static string BuildMetricLensTraderRead(ExposureCardLevelInsight level, OptionExposureMetric metric, string state, string ivConditionText)
    {
        if (level == null)
            return "Wait for mapped zone.";
        if (string.Equals(state, "Conditional", StringComparison.OrdinalIgnoreCase))
        {
            if (metric == OptionExposureMetric.Vanna)
                return "Relevant if price approaches and IV reprices near this zone.";
            if (metric == OptionExposureMetric.Charm)
                return "Relevant if spot remains near this zone later in the session.";

            return "Relevant if spot migrates toward this gamma zone.";
        }

        if (metric == OptionExposureMetric.Vanna)
        {
            if (string.Equals(state, "Unwinding", StringComparison.OrdinalIgnoreCase))
                return "IV layer is fading; move may be more price-led unless IV reprices again.";
            if (IsIvCondition(ivConditionText, "Firming"))
                return "IV is firming; VEX may add hedge pressure if price accepts near this zone.";
            if (IsIvCondition(ivConditionText, "Fading"))
                return "IV is fading; keep VEX conditional unless volatility reprices again.";

            return "IV changes may add hedge pressure if volatility reprices near this zone.";
        }

        if (metric == OptionExposureMetric.Charm)
        {
            if (string.Equals(state, "Unwinding", StringComparison.OrdinalIgnoreCase))
                return "Clock-pressure effect is fading at this zone.";

            return "Time-decay hedging may become more active here, especially later in the session.";
        }

        if (level.IsPositive)
            return "Dealer response may suppress movement; failed breaks can fade.";

        return "Accepted moves through this zone may amplify; avoid blind fades.";
    }

    private static string GetMetricLensDetailLabel(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
            return "IV Condition";
        if (metric == OptionExposureMetric.Charm)
            return "Time Weight";

        return "Migration";
    }

    private static string GetMetricLensDetailValue(OptionExposureMetric metric, ExposureCardLevelInsight level, string state, string migrationText, string ivConditionText)
    {
        if (metric == OptionExposureMetric.Vanna)
        {
            if (string.Equals(state, "Unwinding", StringComparison.OrdinalIgnoreCase) && IsIvCondition(ivConditionText, "Fading"))
                return "Needs IV Rebuild";

            return NormalizeIvConditionText(ivConditionText);
        }
        if (metric == OptionExposureMetric.Charm)
            return GetCharmTimeWeightLabel(DateTime.UtcNow);

        return string.IsNullOrWhiteSpace(migrationText) ? "Flat" : migrationText;
    }

    private static string BuildLevelStrengthText(ExposureCardLevelInsight level)
    {
        if (level == null)
            return "Light";

        if (level.Intensity >= 0.60)
            return "Strong";
        if (level.Intensity >= 0.25)
            return "Moderate";

        return "Light";
    }

    private static Brush GetIvConditionBrush(string ivConditionText, Brush positiveBrush, Brush negativeBrush)
    {
        if (IsIvCondition(ivConditionText, "Firming"))
            return positiveBrush ?? ExposureCardPalette.PositiveBrush;
        if (IsIvCondition(ivConditionText, "Fading"))
            return negativeBrush ?? ExposureCardPalette.NegativeBrush;

        return ExposureCardPalette.BodyBrush;
    }

    private static string NormalizeIvConditionText(string ivConditionText)
    {
        if (IsIvCondition(ivConditionText, "Firming"))
            return "Firming";
        if (IsIvCondition(ivConditionText, "Fading"))
            return "Fading";
        if (IsIvCondition(ivConditionText, "Not Measured"))
            return "Not Measured";

        return "Neutral";
    }

    private static bool IsIvCondition(string ivConditionText, string expected)
    {
        return !string.IsNullOrWhiteSpace(ivConditionText)
            && string.Equals(ivConditionText.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildMetricSkewLabel(string label, double aboveValue, double belowValue)
    {
        var ratio = CalculateSkewRatio(aboveValue, belowValue);
        if (ratio < 1.5)
            return "Balanced " + label;

        return Math.Abs(belowValue) >= Math.Abs(aboveValue) ? "Below " + label : "Above " + label;
    }

    private static string EstimateMetricIntensityLabel(IList<ExposureCardSparklineModel> sparklines, string label, double currentTotalAbs)
    {
        var sparkline = FindSparkline(sparklines, label);
        if (sparkline == null || sparkline.Values == null || sparkline.Values.Count < 5)
            return currentTotalAbs > 0.0 ? "Active" : "Waiting";

        var current = currentTotalAbs > 0.0 ? currentTotalAbs : Math.Abs(sparkline.Values[sparkline.Values.Count - 1]);
        var countBelow = 0;
        for (var index = 0; index < sparkline.Values.Count; index++)
        {
            if (Math.Abs(sparkline.Values[index]) <= current)
                countBelow++;
        }

        var percentile = (double)countBelow / sparkline.Values.Count;
        if (percentile >= 0.90)
            return "Extreme";
        if (percentile >= 0.70)
            return "High";
        if (percentile >= 0.40)
            return "Moderate";

        return "Low";
    }

    private static string GetCharmTimeWeightLabel(DateTime timestampUtc)
    {
        return GetCharmTimeWeightLabel(timestampUtc, CharmMarketTimeZone, () => DateTime.UtcNow);
    }

    private static string GetCharmTimeWeightLabel(DateTime timestampUtc, TimeZoneInfo marketTimeZone, Func<DateTime> utcNowProvider)
    {
        var utc = timestampUtc == DateTime.MinValue ? utcNowProvider() : timestampUtc;
        if (utc.Kind == DateTimeKind.Unspecified)
        {
            var parameterName = timestampUtc == DateTime.MinValue ? nameof(utcNowProvider) : nameof(timestampUtc);
            throw new ArgumentException("Charm time weight timestamps must be Local or Utc.", parameterName);
        }

        utc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
        var marketTime = TimeZoneInfo.ConvertTimeFromUtc(utc, marketTimeZone ?? TimeZoneInfo.Local);
        var minutes = (marketTime.Hour * 60) + marketTime.Minute;
        if (minutes >= (15 * 60) + 30)
            return "Highest";
        if (minutes >= (14 * 60) + 30)
            return "High";
        if (minutes >= 12 * 60)
            return "Moderate";

        return "Light Now";
    }

    private static TimeZoneInfo FindCharmMarketTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.Local;
    }

    private static string FormatNetMetricLabel(string text, string fallbackSide, double value, string abbreviation)
    {
        var signLabel = value > 0.0 ? "+" + abbreviation : value < 0.0 ? "-" + abbreviation : abbreviation;
        if (string.IsNullOrWhiteSpace(text))
            return fallbackSide + " " + signLabel + ": --";

        var cleaned = text.Replace(" spot", string.Empty).Trim();
        var colonIndex = cleaned.IndexOf(':');
        var valueText = colonIndex >= 0 ? cleaned.Substring(colonIndex + 1).Trim() : cleaned;
        if (valueText.EndsWith(" " + abbreviation, StringComparison.OrdinalIgnoreCase))
            valueText = valueText.Substring(0, valueText.Length - abbreviation.Length - 1).Trim();

        return fallbackSide + " " + signLabel + ": " + valueText;
    }
}
