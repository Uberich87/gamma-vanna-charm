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
    private static UIElement BuildOverallTradingZonesBody(
        ExposureCardInsight insight,
        IList<ExposureCardSparklineModel> sparklines,
        OptionExposureStrikeTrend strikeTrend,
        IList<ExposureCardStrikeSparklineModel> strikeSparklines,
        Brush positiveBrush,
        Brush negativeBrush)
    {
        var panel = CreateSectionPanel();
        if (insight == null)
            return panel;

        panel.Children.Add(new TextBlock
        {
            Text = "KEY TRADING ZONES",
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 18.0,
            FontWeight = FontWeights.Bold,
            LineHeight = 23.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 2.0, 0.0, 10.0)
        });
        panel.Children.Add(CreateGexRegimeScoreBlock(insight, sparklines, positiveBrush, negativeBrush));
        panel.Children.Add(CreateMutedText("Zone cards show the top 5 nearby GEX zones within ±5% of spot."));

        if (insight.Levels == null || insight.Levels.Count == 0)
        {
            panel.Children.Add(CreateMutedText("No key trading zones within 5% of spot."));
            return panel;
        }

        for (var index = 0; index < insight.Levels.Count; index++)
            panel.Children.Add(CreateOverallTradingZoneRow(insight.Levels[index], GetStrikeSparklineValues(strikeSparklines, insight.Levels[index]), strikeTrend, positiveBrush, negativeBrush));

        return panel;
    }

    private static Border CreateGexRegimeScoreBlock(ExposureCardInsight insight, IList<ExposureCardSparklineModel> sparklines, Brush positiveBrush, Brush negativeBrush)
    {
        var aboveValue = ParseBalanceExposureValue(insight != null ? insight.AboveBalanceText : null);
        var belowValue = ParseBalanceExposureValue(insight != null ? insight.BelowBalanceText : null);
        var ratio = CalculateSkewRatio(aboveValue, belowValue);
        var dominantBelow = Math.Abs(belowValue) >= Math.Abs(aboveValue);

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(CreateSubsectionHeading("GEX REGIME SCORE", ExposureCardPalette.MutedBrush));

        var scoreGrid = new Grid { Margin = new Thickness(0.0, 2.0, 0.0, 8.0) };
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        scoreGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        scoreGrid.Children.Add(CreateRegimeScoreColumn("Regime", BuildGexRegimeLabel(aboveValue, belowValue), GetGexRegimeBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 0));
        scoreGrid.Children.Add(CreateRegimeScoreColumn("Skew", BuildGexSkewLabel(aboveValue, belowValue), GetGexSkewBrush(aboveValue, belowValue, positiveBrush, negativeBrush), 1));
        scoreGrid.Children.Add(CreateRegimeScoreColumn("Intensity", EstimateGexIntensityLabel(sparklines, Math.Abs(aboveValue) + Math.Abs(belowValue)), ExposureCardPalette.TextBrush, 2));
        panel.Children.Add(scoreGrid);

        var netGrid = new Grid();
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        netGrid.Children.Add(CreateRegimeNetCell(FormatNetGexLabel(insight != null ? insight.AboveBalanceText : null, "Above", aboveValue), 0, GetGexValueBrush(aboveValue, positiveBrush, negativeBrush)));
        netGrid.Children.Add(CreateRegimeNetCell(FormatNetGexLabel(insight != null ? insight.BelowBalanceText : null, "Below", belowValue), 1, GetGexValueBrush(belowValue, positiveBrush, negativeBrush)));
        netGrid.Children.Add(CreateRegimeNetCell(FormatSkewRatioText(ratio, dominantBelow), 2, ExposureCardPalette.BodyBrush));
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

    private static UIElement CreateRegimeScoreColumn(string label, string value, Brush valueBrush, int column)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(new TextBlock
        {
            Text = label ?? string.Empty,
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.0,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = value ?? string.Empty,
            Foreground = valueBrush ?? ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 16.0,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
        });

        Grid.SetColumn(panel, column);
        return panel;
    }

    private static UIElement CreateRegimeNetCell(string text, int column, Brush foreground)
    {
        var block = new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = foreground ?? ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 11.0,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
        };
        Grid.SetColumn(block, column);
        return block;
    }


    private static Border CreateOverallTradingZoneRow(ExposureCardLevelInsight level, IList<double> sparklineValues, OptionExposureStrikeTrend strikeTrend, Brush positiveBrush, Brush negativeBrush)
    {
        var isPositive = level != null && level.IsPositive;
        var accentBrush = isPositive ? positiveBrush ?? ExposureCardPalette.PositiveBrush : negativeBrush ?? ExposureCardPalette.NegativeBrush;
        var stateRead = ResolveZoneStateRead(level, sparklineValues, strikeTrend);
        var state = stateRead.StateText;
        var migration = stateRead.MigrationText;
        var roleStack = SplitRoleStack(level != null ? level.LabelText : null, isPositive);
        roleStack[0] = BuildLocationAwareRole(level, roleStack[0]);

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
            Text = BuildZoneTraderRead(level, state),
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
        metrics.Children.Add(CreateZoneMetricBlock("Δ Notional", FormatSparklineNotionalChange(sparklineValues), ExposureCardPalette.BrandAccentBrush, 1));
        metrics.Children.Add(CreateZoneMetricBlock("Migration", migration, ExposureCardPalette.BodyBrush, 2));
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

    private static Border CreateZonePill(string text, Brush accentBrush)
    {
        return new Border
        {
            BorderBrush = accentBrush ?? ExposureCardPalette.BrandAccentBrush,
            BorderThickness = new Thickness(1.0),
            CornerRadius = new CornerRadius(4.0),
            Padding = new Thickness(6.0, 3.0, 6.0, 3.0),
            Margin = new Thickness(5.0, 0.0, 0.0, 0.0),
            Child = new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = accentBrush ?? ExposureCardPalette.BrandAccentBrush,
                FontFamily = ExposureCardPalette.FontFamily,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    private static UIElement CreateZoneMetricBlock(string label, string value, Brush valueBrush, int column)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(new TextBlock
        {
            Text = label ?? string.Empty,
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = value ?? string.Empty,
            Foreground = valueBrush ?? ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 11.0,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(panel, column);
        return panel;
    }


    private static void AddTradingZoneBodyCell(Grid grid, string text, int column, Brush foreground, double fontSize, FontWeight fontWeight)
    {
        var block = new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = foreground ?? ExposureCardPalette.BodyBrush,
            FontFamily = column == 0 || column == 2 || column == 3 ? ExposureCardPalette.MonospaceFontFamily : ExposureCardPalette.FontFamily,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = 15.0,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8.0, 9.0, 7.0, 9.0)
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }


    private static UIElement CreateTradingZoneTrendRow(IList<double> values, Brush accentBrush)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76.0) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        AddTradingZoneBodyCell(grid, "Trend", 0, ExposureCardPalette.MutedBrush, 11.0, FontWeights.SemiBold);
        var canvas = new Canvas
        {
            Margin = new Thickness(8.0, 5.0, 8.0, 7.0)
        };
        canvas.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
        {
            ExposureCardSparklineRenderer.Draw(canvas, values, accentBrush, args.NewSize.Width, args.NewSize.Height, 1.25);
        };
        ExposureCardSparklineRenderer.Draw(canvas, values, accentBrush, canvas.ActualWidth, canvas.ActualHeight, 1.25);
        Grid.SetColumn(canvas, 1);
        grid.Children.Add(canvas);
        return grid;
    }

    private static string[] SplitRoleStack(string text, bool isPositive)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            var parts = text.Split(new[] { '|' }, 2);
            if (parts.Length == 2)
                return new[] { parts[0].Trim(), parts[1].Trim() };
        }

        return isPositive
            ? new[] { "Pin / failed-break zone", "+GEX / light VEX / light CEX" }
            : new[] { "Expansion zone", "-GEX / light VEX / light CEX" };
    }

    private static string BuildLocationAwareRole(ExposureCardLevelInsight level, string fallbackRole)
    {
        if (level == null)
            return fallbackRole ?? string.Empty;
        if (!level.IsPositive && level.DistanceFromSpot < 0.0)
            return (fallbackRole != null && fallbackRole.IndexOf("Stacked", StringComparison.OrdinalIgnoreCase) >= 0) ? "Stacked downside expansion" : "Active downside trigger";
        if (!level.IsPositive && level.DistanceFromSpot > 0.0)
            return (fallbackRole != null && fallbackRole.IndexOf("Stacked", StringComparison.OrdinalIgnoreCase) >= 0) ? "Stacked upside expansion" : "Active upside trigger";
        if (level.IsPositive)
            return fallbackRole ?? "Pin / failed-break zone";

        return fallbackRole ?? string.Empty;
    }

    private static ExposureZoneStateRead ResolveZoneStateRead(ExposureCardLevelInsight level, IList<double> values, OptionExposureStrikeTrend strikeTrend)
    {
        return ExposureZoneStateAnalyzer.Analyze(
            level,
            values,
            strikeTrend,
            HasStackContext(level, "VEX"),
            HasStackContext(level, "CEX"));
    }

    private static bool HasStackContext(ExposureCardLevelInsight level, string label)
    {
        if (level == null || string.IsNullOrWhiteSpace(level.LabelText) || string.IsNullOrWhiteSpace(label))
            return false;

        var text = level.LabelText;
        if (text.IndexOf("strong " + label, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (text.IndexOf(label + " context", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (text.IndexOf(label + " late", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private static string BuildZoneTraderRead(ExposureCardLevelInsight level, string state)
    {
        if (level == null)
            return "Wait for mapped zone.";
        if (string.Equals(state, "Conditional", StringComparison.OrdinalIgnoreCase))
            return "Relevant only if spot migrates toward this zone.";
        if (level.IsPositive)
            return "Needs clean acceptance; failed moves can fade.";
        if (level.DistanceFromSpot < 0.0)
            return "Accepted moves below may amplify.";

        return "Accepted moves above may amplify.";
    }

    private static string FormatSparklineNotionalChange(IList<double> values)
    {
        if (values == null || values.Count < 2)
            return "--";

        return ExposureCardTextFormatter.FormatSignedCompactExposure(Math.Abs(values[values.Count - 1]) - Math.Abs(values[0]));
    }

    private static double ParseBalanceExposureValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0.0;

        var value = text;
        var colonIndex = value.IndexOf(':');
        if (colonIndex >= 0)
            value = value.Substring(colonIndex + 1).Trim();
        var spaceIndex = value.IndexOf(' ');
        if (spaceIndex > 0)
            value = value.Substring(0, spaceIndex).Trim();

        var multiplier = 1.0;
        if (value.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1000000000.0;
            value = value.Substring(0, value.Length - 1);
        }
        else if (value.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1000000.0;
            value = value.Substring(0, value.Length - 1);
        }
        else if (value.EndsWith("K", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1000.0;
            value = value.Substring(0, value.Length - 1);
        }

        double parsed;
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) ? parsed * multiplier : 0.0;
    }

    private static string BuildGexRegimeLabel(double aboveValue, double belowValue)
    {
        if (aboveValue > 0.0 && belowValue > 0.0)
            return "Suppression";
        if (aboveValue < 0.0 && belowValue < 0.0)
            return "Expansion";
        if (aboveValue > 0.0 && belowValue < 0.0)
            return "Split: Pin Above / Expand Below";
        if (aboveValue < 0.0 && belowValue > 0.0)
            return "Mixed / Unstable";

        return "Waiting";
    }

    private static Brush GetGexRegimeBrush(double aboveValue, double belowValue, Brush positiveBrush, Brush negativeBrush)
    {
        if (aboveValue > 0.0 && belowValue > 0.0)
            return positiveBrush ?? ExposureCardPalette.PositiveBrush;
        if (aboveValue < 0.0 && belowValue < 0.0)
            return negativeBrush ?? ExposureCardPalette.NegativeBrush;

        var dominantValue = Math.Abs(belowValue) >= Math.Abs(aboveValue) ? belowValue : aboveValue;
        return GetGexValueBrush(dominantValue, positiveBrush, negativeBrush);
    }

    private static string BuildGexSkewLabel(double aboveValue, double belowValue)
    {
        var absAbove = Math.Abs(aboveValue);
        var absBelow = Math.Abs(belowValue);
        var ratio = CalculateSkewRatio(aboveValue, belowValue);
        if (ratio < 1.5)
            return "Balanced Response";

        var dominantBelow = absBelow >= absAbove;
        var dominantValue = dominantBelow ? belowValue : aboveValue;
        var behavior = dominantValue < 0.0 ? "Amplification" : "Suppression";
        return (dominantBelow ? "Below-Spot " : "Above-Spot ") + behavior;
    }

    private static Brush GetGexSkewBrush(double aboveValue, double belowValue, Brush positiveBrush, Brush negativeBrush)
    {
        var ratio = CalculateSkewRatio(aboveValue, belowValue);
        if (ratio < 1.5)
            return ExposureCardPalette.BodyBrush;

        var dominantValue = Math.Abs(belowValue) >= Math.Abs(aboveValue) ? belowValue : aboveValue;
        return GetGexValueBrush(dominantValue, positiveBrush, negativeBrush);
    }

    private static Brush GetGexValueBrush(double value, Brush positiveBrush, Brush negativeBrush)
    {
        if (value > 0.0)
            return positiveBrush ?? ExposureCardPalette.PositiveBrush;
        if (value < 0.0)
            return negativeBrush ?? ExposureCardPalette.NegativeBrush;

        return ExposureCardPalette.BodyBrush;
    }

    private static double CalculateSkewRatio(double aboveValue, double belowValue)
    {
        var absAbove = Math.Abs(aboveValue);
        var absBelow = Math.Abs(belowValue);
        var smaller = Math.Min(absAbove, absBelow);
        var larger = Math.Max(absAbove, absBelow);
        if (smaller <= 0.0)
            return larger > 0.0 ? 999.0 : 0.0;

        return larger / smaller;
    }

    private static string FormatSkewRatioText(double ratio, bool belowDominant)
    {
        if (ratio <= 0.0)
            return "Ratio: --";
        if (ratio >= 999.0)
            return belowDominant ? "Below/Above: >99x" : "Above/Below: >99x";

        return (belowDominant ? "Below/Above: " : "Above/Below: ") + ratio.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "x";
    }

    private static string EstimateGexIntensityLabel(IList<ExposureCardSparklineModel> sparklines, double currentTotalAbs)
    {
        var gamma = FindSparkline(sparklines, "GEX");
        if (gamma == null || gamma.Values == null || gamma.Values.Count < 5)
            return currentTotalAbs > 0.0 ? "Active" : "Waiting";

        var current = currentTotalAbs > 0.0 ? currentTotalAbs : Math.Abs(gamma.Values[gamma.Values.Count - 1]);
        var countBelow = 0;
        for (var index = 0; index < gamma.Values.Count; index++)
        {
            if (Math.Abs(gamma.Values[index]) <= current)
                countBelow++;
        }

        var percentile = (double)countBelow / gamma.Values.Count;
        if (percentile >= 0.90)
            return "Extreme";
        if (percentile >= 0.70)
            return "High";
        if (percentile >= 0.40)
            return "Moderate";

        return "Low";
    }

    private static ExposureCardSparklineModel FindSparkline(IList<ExposureCardSparklineModel> sparklines, string label)
    {
        if (sparklines == null)
            return null;

        for (var index = 0; index < sparklines.Count; index++)
        {
            var sparkline = sparklines[index];
            if (sparkline != null && string.Equals(sparkline.Label, label, StringComparison.OrdinalIgnoreCase))
                return sparkline;
        }

        return null;
    }

    private static string FormatNetGexLabel(string text, string fallbackSide, double value)
    {
        var gexSign = value > 0.0 ? "+GEX" : value < 0.0 ? "-GEX" : "GEX";
        if (string.IsNullOrWhiteSpace(text))
            return fallbackSide + " " + gexSign + ": --";

        var cleaned = text.Replace(" spot", string.Empty).Trim();
        var colonIndex = cleaned.IndexOf(':');
        var valueText = colonIndex >= 0 ? cleaned.Substring(colonIndex + 1).Trim() : cleaned;
        if (valueText.EndsWith(" GEX", StringComparison.OrdinalIgnoreCase))
            valueText = valueText.Substring(0, valueText.Length - 4).Trim();

        return fallbackSide + " " + gexSign + ": " + valueText;
    }

    private static IList<double> GetStrikeSparklineValues(IList<ExposureCardStrikeSparklineModel> strikeSparklines, ExposureCardLevelInsight level)
    {
        if (strikeSparklines == null || level == null)
            return new List<double>();

        for (var index = 0; index < strikeSparklines.Count; index++)
        {
            var sparkline = strikeSparklines[index];
            if (sparkline != null && Math.Abs(sparkline.Strike - level.Strike) < 0.0000001)
                return sparkline.Values;
        }

        return new List<double>();
    }
}
