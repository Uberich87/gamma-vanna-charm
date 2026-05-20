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

internal sealed class ExposureCardHeaderView : Grid
{
    private readonly Grid _metricButtonsPanel;
    private readonly Grid _titleRow;
    private readonly Border _heroPanel;
    private TextBlock _symbolText;
    private TextBlock _priceText;
    private Border _regimeBadge;
    private TextBlock _regimeTitleText;
    private TextBlock _regimeDetailText;
    private readonly TextBlock _statusTitle;
    private readonly ExposurePill _statusBadge;
    private readonly ExposurePill _collapseButton;
    private readonly Dictionary<OptionExposureMetric, ExposurePill> _metricButtons;

    public ExposureCardHeaderView()
    {
        Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _metricButtons = new Dictionary<OptionExposureMetric, ExposurePill>();

        _titleRow = new Grid { Margin = new Thickness(0.0, 0.0, 0.0, 9.0) };
        _titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        _titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = ExposureMetricPresentation.CardTitle,
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        };

        _collapseButton = new ExposurePill("−")
        {
            Height = 22.0,
            MinWidth = 28.0,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        _collapseButton.Clicked += delegate(object sender, EventArgs args)
        {
            var handler = CollapseRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        };

        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(_collapseButton, 1);
        _titleRow.Children.Add(titleText);
        _titleRow.Children.Add(_collapseButton);

        _heroPanel = CreateHeroPanel();

        _metricButtonsPanel = new Grid { Margin = new Thickness(0.0, 8.0, 0.0, 0.0) };
        for (var index = 0; index < ExposureMetricPresentation.CardMetricCount; index++)
        {
            _metricButtonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            var metric = ExposureMetricPresentation.GetCardMetric(index);
            AddMetricButton(metric, ExposureMetricPresentation.GetDisplayName(metric), index);
        }

        _statusTitle = new TextBlock
        {
            Text = "OPTIONS DATA",
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusBadge = new ExposurePill("INFO") { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Right };

        Grid.SetRow(_titleRow, 0);
        Grid.SetRow(_heroPanel, 1);
        Grid.SetRow(_metricButtonsPanel, 2);
        Grid.SetRow(_statusTitle, 3);
        Grid.SetRow(_statusBadge, 3);
        Children.Add(_titleRow);
        Children.Add(_heroPanel);
        Children.Add(_metricButtonsPanel);
        Children.Add(_statusTitle);
        Children.Add(_statusBadge);
    }

    public event EventHandler<ExposureMetricSelectedEventArgs> MetricSelected;

    public event EventHandler CollapseRequested;

    public void Update(ExposureCardWpfModel model)
    {
        if (model == null)
            return;

        var isStatus = model.IsStatus;
        _heroPanel.Visibility = isStatus ? Visibility.Collapsed : Visibility.Visible;
        _metricButtonsPanel.Visibility = isStatus ? Visibility.Collapsed : Visibility.Visible;
        _statusTitle.Visibility = isStatus ? Visibility.Visible : Visibility.Collapsed;
        _statusBadge.Visibility = isStatus ? Visibility.Visible : Visibility.Collapsed;
        _collapseButton.Visibility = isStatus ? Visibility.Collapsed : Visibility.Visible;

        if (isStatus)
        {
            _statusBadge.Text = model.StatusBadgeText;
            _statusBadge.SetSelected(true);
            return;
        }

        var symbolParts = SplitHeaderSymbol(model.HeaderSymbolText);
        _symbolText.Text = symbolParts[0];
        _priceText.Text = symbolParts[1];
        var regimeTitle = ResolveRegimeBadgeTitle(model.Insight != null ? model.Insight.RegimeText : null);
        _regimeTitleText.Text = regimeTitle;
        _regimeTitleText.Foreground = ExposureCardPalette.BrandAccentBrush;
        _regimeBadge.Background = ExposureCardPalette.RangePanelBrush;
        _regimeDetailText.Text = ResolveRegimeBadgeDetail(model.Insight != null ? model.Insight.RegimeText : null, model.PositionModelText);

        foreach (var pair in _metricButtons)
            pair.Value.SetSelected(pair.Key == model.SelectedMetric, GetMetricBrush());
    }

    private Border CreateHeroPanel()
    {
        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var symbolStack = new StackPanel { Orientation = Orientation.Vertical };
        _symbolText = new TextBlock
        {
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 22.0,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0.0, 1.0, 0.0, 0.0)
        };
        _priceText = new TextBlock
        {
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        symbolStack.Children.Add(_symbolText);
        symbolStack.Children.Add(_priceText);

        _regimeBadge = new Border
        {
            CornerRadius = new CornerRadius(7.0),
            BorderThickness = new Thickness(1.0),
            BorderBrush = ExposureCardPalette.BorderBrush,
            Background = ExposureCardPalette.RangePanelBrush,
            Padding = new Thickness(10.0, 7.0, 10.0, 7.0),
            MinWidth = 140.0,
            MaxWidth = 220.0,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeStack = new StackPanel { Orientation = Orientation.Vertical };
        _regimeTitleText = new TextBlock
        {
            Foreground = ExposureCardPalette.BrandAccentBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.0,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap
        };
        _regimeDetailText = new TextBlock
        {
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 14.0,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
        };
        badgeStack.Children.Add(_regimeTitleText);
        badgeStack.Children.Add(_regimeDetailText);
        _regimeBadge.Child = badgeStack;

        Grid.SetColumn(symbolStack, 0);
        Grid.SetColumn(_regimeBadge, 1);
        heroGrid.Children.Add(symbolStack);
        heroGrid.Children.Add(_regimeBadge);

        return new Border
        {
            BorderBrush = ExposureCardPalette.BorderBrush,
            BorderThickness = new Thickness(1.0),
            Background = ExposureCardPalette.HeroBrush,
            Padding = new Thickness(14.0, 12.0, 14.0, 12.0),
            Child = heroGrid
        };
    }

    private static Brush GetMetricBrush()
    {
        return ExposureCardPalette.BrandAccentBrush;
    }

    private static string[] SplitHeaderSymbol(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new[] { "--", string.Empty };

        var trimmed = text.Trim();
        var splitIndex = trimmed.LastIndexOf(' ');
        if (splitIndex <= 0 || splitIndex >= trimmed.Length - 1)
            return new[] { trimmed, string.Empty };

        return new[] { trimmed.Substring(0, splitIndex), trimmed.Substring(splitIndex + 1) };
    }

    private static string ResolveRegimeBadgeTitle(string regimeText)
    {
        if (string.IsNullOrWhiteSpace(regimeText))
            return "SCENARIO READ";

        var upperText = regimeText.ToUpperInvariant();
        if (upperText.IndexOf("EXPANSION", StringComparison.Ordinal) >= 0)
            return "EXPANSION RISK";
        if (upperText.IndexOf("REJECTION", StringComparison.Ordinal) >= 0)
            return "REJECTION ZONE";
        if (upperText.IndexOf("PIN", StringComparison.Ordinal) >= 0 || upperText.IndexOf("RANGE", StringComparison.Ordinal) >= 0 || upperText.IndexOf("DAMP", StringComparison.Ordinal) >= 0)
            return "PIN / RANGE";
        if (upperText.IndexOf("MIX", StringComparison.Ordinal) >= 0)
            return "MIXED READ";

        return "SCENARIO READ";
    }

    private static string ResolveRegimeBadgeDetail(string regimeText, string positionModelText)
    {
        if (!string.IsNullOrWhiteSpace(positionModelText))
            return positionModelText.Trim();
        if (string.IsNullOrWhiteSpace(regimeText))
            return ExposureMetricPresentation.CardTitle;

        return regimeText.Trim();
    }

    private void AddMetricButton(OptionExposureMetric metric, string text, int column)
    {
        var lastColumn = ExposureMetricPresentation.CardMetricCount - 1;
        var rightMargin = column < lastColumn ? 2.0 : 0.0;
        var leftMargin = column > 0 ? 2.0 : 0.0;
        var pill = new ExposurePill(text)
        {
            Height = 30.0,
            Margin = new Thickness(leftMargin, 0.0, rightMargin, 0.0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        pill.Clicked += delegate(object sender, EventArgs args)
        {
            var handler = MetricSelected;
            if (handler != null)
                handler(this, new ExposureMetricSelectedEventArgs(metric));
        };
        _metricButtons[metric] = pill;
        Grid.SetColumn(pill, column);
        _metricButtonsPanel.Children.Add(pill);
    }
}
