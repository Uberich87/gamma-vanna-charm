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

internal sealed partial class ExposureCardContentView : Grid
{
    private readonly TextBlock _statusText;
    private readonly StackPanel _sectionsPanel;
    private readonly ExposureAccordionSection _thesisSection;
    private bool _initializedExposureSections;

    public ExposureCardContentView()
    {
        _statusText = new TextBlock
        {
            Foreground = ExposureCardPalette.TextBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 12.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 8.0, 0.0, 10.0),
            MinHeight = 32.0
        };
        Children.Add(_statusText);

        _sectionsPanel = new StackPanel { Orientation = Orientation.Vertical };
        _thesisSection = new ExposureAccordionSection("DEALER FLOW READ");
        _sectionsPanel.Children.Add(_thesisSection);
        Children.Add(_sectionsPanel);
    }

    public void Update(ExposureCardWpfModel model)
    {
        if (model == null)
            return;

        if (model.IsStatus)
        {
            _sectionsPanel.Visibility = Visibility.Collapsed;
            _statusText.Visibility = Visibility.Visible;
            _statusText.Text = model.StatusText;
            return;
        }

        _statusText.Visibility = Visibility.Collapsed;
        _sectionsPanel.Visibility = Visibility.Visible;

        var insight = model.Insight;
        if (model.SelectedMetric == OptionExposureMetric.Overall)
        {
            _thesisSection.SetTitle(BuildThesisSectionTitle(model.PositionModelText));
            _thesisSection.SetHelpTooltip(GetOverallTradingZonesHelpTooltip());
            _thesisSection.SetBody(BuildOverallTradingZonesBody(insight, model.Sparklines, model.SelectedStrikeTrend, model.StrikeSparklines, model.SelectedPositiveBrush, model.SelectedNegativeBrush));
        }
        else
        {
            _thesisSection.SetTitle(BuildThesisSectionTitle(model.PositionModelText));
            _thesisSection.SetHelpTooltip(GetMetricLensHelpTooltip(model.SelectedMetric));
            _thesisSection.SetBody(BuildMetricLensZonesBody(insight, model.SelectedMetric, model.Sparklines, model.SelectedStrikeTrend, model.IvConditionText, model.StrikeSparklines, model.SelectedPositiveBrush, model.SelectedNegativeBrush));
        }

        if (!_initializedExposureSections)
        {
            _thesisSection.SetExpanded(true, false);
            _initializedExposureSections = true;
        }
    }

    private static string BuildThesisSectionTitle(string positionModelText)
    {
        if (string.IsNullOrWhiteSpace(positionModelText))
            return "DEALER FLOW READ";

        return "DEALER FLOW READ \u00B7 " + positionModelText.Trim().ToUpperInvariant();
    }
}
