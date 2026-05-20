#region Using declarations
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

internal sealed class ExposureCardFooterView : Grid
{
    private readonly Grid _timestampsPanel;
    private readonly TextBlock _checkedText;
    private readonly TextBlock _snapshotText;
    private readonly Grid _controlRow;
    private readonly TextBlock _currentVersionText;
    private readonly TextBlock _versionUpdateLink;
    private readonly StackPanel _buttonsPanel;
    private readonly ExposurePill _dataScopePill;
    private readonly List<ExposurePill> _proxyPills;

    public ExposureCardFooterView()
    {
        Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _timestampsPanel = new Grid();
        _timestampsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _timestampsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        _timestampsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _timestampsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _checkedText = AddTimestampRow("Checked UTC", 0);
        _snapshotText = AddTimestampRow("Updated UTC", 1);

        _controlRow = new Grid { Margin = new Thickness(0.0, 7.0, 0.0, 0.0) };
        _controlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        _controlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _currentVersionText = CreateCurrentVersionText();
        _buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _dataScopePill = new ExposurePill("0DTE") { Height = 18.0, MinWidth = 46.0 };
        _dataScopePill.Clicked += delegate(object sender, EventArgs args)
        {
            var handler = DataScopeToggled;
            if (handler != null)
                handler(this, EventArgs.Empty);
        };
        _proxyPills = new List<ExposurePill>();
        _versionUpdateLink = CreateVersionUpdateLink();

        Grid.SetColumn(_currentVersionText, 0);
        Grid.SetColumn(_buttonsPanel, 1);
        _controlRow.Children.Add(_currentVersionText);
        _controlRow.Children.Add(_buttonsPanel);

        Grid.SetRow(_timestampsPanel, 0);
        Grid.SetRow(_controlRow, 1);
        Grid.SetRow(_versionUpdateLink, 2);
        Children.Add(_timestampsPanel);
        Children.Add(_controlRow);
        Children.Add(_versionUpdateLink);
    }

    public event EventHandler<ExposureProxySelectedEventArgs> ProxySelected;

    public event EventHandler DataScopeToggled;

    public void Update(ExposureCardWpfModel model)
    {
        if (model == null)
            return;

        _checkedText.Text = model.CheckedText;
        _snapshotText.Text = model.SnapshotText;
        _currentVersionText.Text = model.CurrentVersionText;
        UpdateVersionLink(model.UpdateLinkText, model.UpdateLinkUrl);

        if (model.IsStatus)
        {
            _controlRow.Visibility = Visibility.Visible;
            _buttonsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _controlRow.Visibility = Visibility.Visible;
        _buttonsPanel.Visibility = Visibility.Visible;
        RebuildButtons(model);
    }

    private void RebuildButtons(ExposureCardWpfModel model)
    {
        _buttonsPanel.Children.Clear();
        _proxyPills.Clear();

        if (model.ProxyButtons != null)
        {
            for (var index = 0; index < model.ProxyButtons.Count; index++)
            {
                var proxyModel = model.ProxyButtons[index];
                var pill = new ExposurePill(proxyModel.Text)
                {
                    Height = 18.0,
                    MinWidth = 38.0,
                    Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
                    IsHitTestVisible = model.ProxyButtons.Count > 1
                };
                pill.SetSelected(proxyModel.IsSelected);
                var capturedProxy = proxyModel.ProxySymbol;
                pill.Clicked += delegate(object sender, EventArgs args)
                {
                    var handler = ProxySelected;
                    if (handler != null)
                        handler(this, new ExposureProxySelectedEventArgs(capturedProxy));
                };
                _proxyPills.Add(pill);
                _buttonsPanel.Children.Add(pill);
            }
        }

        _dataScopePill.Text = model.DataScopeText;
        _dataScopePill.SetSelected(true);
        _dataScopePill.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
        _buttonsPanel.Children.Add(_dataScopePill);
    }

    private void UpdateVersionLink(string text, string url)
    {
        _versionUpdateLink.Inlines.Clear();
        _versionUpdateLink.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(url))
            return;

        var trimmedUrl = url.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var navigateUri))
            return;

        // Only allow http and https schemes for security
        if (!string.Equals(navigateUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(navigateUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return;

        var link = new Hyperlink(new Run(text.Trim()))
        {
            NavigateUri = navigateUri,
            Foreground = ExposureCardPalette.BrandAccentBrush,
            TextDecorations = TextDecorations.Underline
        };
        link.Click += delegate(object sender, RoutedEventArgs args)
        {
            OpenVersionUpdateLink(trimmedUrl);
            args.Handled = true;
        };

        _versionUpdateLink.Inlines.Add(link);
        _versionUpdateLink.Visibility = Visibility.Visible;
    }

    private static TextBlock CreateCurrentVersionText()
    {
        return new TextBlock
        {
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static TextBlock CreateVersionUpdateLink()
    {
        return new TextBlock
        {
            Foreground = ExposureCardPalette.BrandAccentBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 10.5,
            Margin = new Thickness(0.0, 7.0, 0.0, 0.0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed
        };
    }

    private static void OpenVersionUpdateLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url.Trim(),
                UseShellExecute = true
            });
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private TextBlock AddTimestampRow(string label, int row)
    {
        var labelText = CreateFooterLabelText(label);
        var valueText = CreateFooterValueText();

        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, 0);
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        _timestampsPanel.Children.Add(labelText);
        _timestampsPanel.Children.Add(valueText);
        return valueText;
    }

    private static TextBlock CreateFooterLabelText(string text)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 10.5,
            Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static TextBlock CreateFooterValueText()
    {
        return new TextBlock
        {
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.5,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }
}
