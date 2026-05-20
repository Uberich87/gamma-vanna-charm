#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal sealed class ExposureCardWpfModel
{
    private ExposureCardWpfModel()
    {
        ProxyButtons = new List<ExposureCardProxyButtonModel>();
    }

    public bool IsStatus { get; private set; }

    public string StatusBadgeText { get; private set; }

    public string StatusText { get; private set; }

    public string HeaderSymbolText { get; private set; }

    public OptionExposureMetric SelectedMetric { get; private set; }

    public string PositionModelText { get; private set; }

    public string DataScopeText { get; private set; }

    public string CheckedText { get; private set; }

    public string SnapshotText { get; private set; }

    public string CurrentVersionText { get; private set; }

    public string UpdateLinkText { get; private set; }

    public string UpdateLinkUrl { get; private set; }

    public IList<ExposureCardProxyButtonModel> ProxyButtons { get; private set; }

    public IList<ExposureCardSparklineModel> Sparklines { get; private set; }

    public string StrikeTrendText { get; private set; }

    public string StrikeBiasText { get; private set; }

    public OptionExposureStrikeTrend SelectedStrikeTrend { get; private set; }

    public string IvConditionText { get; private set; }

    public IList<ExposureCardStrikeSparklineModel> StrikeSparklines { get; private set; }

    public ExposureCardInsight Insight { get; private set; }

    public Brush SelectedPositiveBrush { get; private set; }

    public Brush SelectedNegativeBrush { get; private set; }

    public Brush GammaMetricBrush { get; private set; }

    public Brush VannaMetricBrush { get; private set; }

    public Brush CharmMetricBrush { get; private set; }

    public static ExposureCardWpfModel CreateStatus(string badgeText, string statusText, string checkedText, string snapshotText, string currentVersionText, string updateLinkText, string updateLinkUrl)
    {
        return new ExposureCardWpfModel
        {
            IsStatus = true,
            StatusBadgeText = badgeText ?? "INFO",
            StatusText = statusText ?? string.Empty,
            CheckedText = checkedText ?? string.Empty,
            SnapshotText = snapshotText ?? string.Empty,
            CurrentVersionText = currentVersionText ?? string.Empty,
            UpdateLinkText = updateLinkText ?? string.Empty,
            UpdateLinkUrl = updateLinkUrl ?? string.Empty
        };
    }

    public static ExposureCardWpfModel CreateExposure(
        string headerSymbolText,
        OptionExposureMetric selectedMetric,
        string positionModelText,
        string dataScopeText,
        string checkedText,
        string snapshotText,
        IList<ExposureCardProxyButtonModel> proxyButtons,
        IList<ExposureCardSparklineModel> sparklines,
        string strikeTrendText,
        string strikeBiasText,
        OptionExposureStrikeTrend selectedStrikeTrend,
        string ivConditionText,
        IList<ExposureCardStrikeSparklineModel> strikeSparklines,
        ExposureCardInsight insight,
        Brush selectedPositiveBrush,
        Brush selectedNegativeBrush,
        Brush gammaMetricBrush,
        Brush vannaMetricBrush,
        Brush charmMetricBrush,
        string currentVersionText,
        string updateLinkText,
        string updateLinkUrl)
    {
        return new ExposureCardWpfModel
        {
            IsStatus = false,
            HeaderSymbolText = headerSymbolText ?? string.Empty,
            SelectedMetric = selectedMetric,
            PositionModelText = positionModelText ?? string.Empty,
            DataScopeText = dataScopeText ?? string.Empty,
            CheckedText = checkedText ?? string.Empty,
            SnapshotText = snapshotText ?? string.Empty,
            ProxyButtons = proxyButtons ?? new List<ExposureCardProxyButtonModel>(),
            Sparklines = sparklines ?? new List<ExposureCardSparklineModel>(),
            StrikeTrendText = strikeTrendText ?? string.Empty,
            StrikeBiasText = strikeBiasText ?? string.Empty,
            SelectedStrikeTrend = selectedStrikeTrend,
            IvConditionText = ivConditionText ?? string.Empty,
            StrikeSparklines = strikeSparklines ?? new List<ExposureCardStrikeSparklineModel>(),
            Insight = insight,
            SelectedPositiveBrush = selectedPositiveBrush ?? ExposureCardPalette.PositiveBrush,
            SelectedNegativeBrush = selectedNegativeBrush ?? ExposureCardPalette.NegativeBrush,
            GammaMetricBrush = gammaMetricBrush ?? ExposureCardPalette.PositiveBrush,
            VannaMetricBrush = vannaMetricBrush ?? ExposureCardPalette.PositiveBrush,
            CharmMetricBrush = charmMetricBrush ?? ExposureCardPalette.PositiveBrush,
            CurrentVersionText = currentVersionText ?? string.Empty,
            UpdateLinkText = updateLinkText ?? string.Empty,
            UpdateLinkUrl = updateLinkUrl ?? string.Empty
        };
    }
}

internal sealed class ExposureCardStrikeSparklineModel
{
    public ExposureCardStrikeSparklineModel(double strike, IList<double> values)
    {
        Strike = strike;
        Values = values ?? new List<double>();
    }

    public double Strike { get; private set; }

    public IList<double> Values { get; private set; }
}

internal sealed class ExposureCardSparklineModel
{
    public ExposureCardSparklineModel(string label, string changeText, IList<double> values, Brush brush)
    {
        Label = label ?? string.Empty;
        ChangeText = changeText ?? string.Empty;
        Values = values ?? new List<double>();
        Brush = brush ?? ExposureCardPalette.BrandAccentBrush;
    }

    public string Label { get; private set; }

    public string ChangeText { get; private set; }

    public IList<double> Values { get; private set; }

    public Brush Brush { get; private set; }
}

internal sealed class ExposureCardProxyButtonModel
{
    public ExposureCardProxyButtonModel(SpotProxySymbol proxySymbol, string text, bool isSelected)
    {
        ProxySymbol = proxySymbol;
        Text = text ?? string.Empty;
        IsSelected = isSelected;
    }

    public SpotProxySymbol ProxySymbol { get; private set; }

    public string Text { get; private set; }

    public bool IsSelected { get; private set; }
}

internal sealed class ExposureMetricSelectedEventArgs : EventArgs
{
    public ExposureMetricSelectedEventArgs(OptionExposureMetric metric)
    {
        Metric = metric;
    }

    public OptionExposureMetric Metric { get; private set; }
}

internal sealed class ExposureProxySelectedEventArgs : EventArgs
{
    public ExposureProxySelectedEventArgs(SpotProxySymbol proxySymbol)
    {
        ProxySymbol = proxySymbol;
    }

    public SpotProxySymbol ProxySymbol { get; private set; }
}
