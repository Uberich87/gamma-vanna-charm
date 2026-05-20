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

internal sealed class ExposureCardWpfView : Border
{
    private static readonly Brush CardBackground = CreateFrozenBrush(Color.FromArgb(248, 7, 12, 15));
    private static readonly Brush CardBorder = CreateFrozenBrush(Color.FromArgb(92, 73, 86, 101));
    private readonly Grid _root;
    private readonly Grid _layout;
    private readonly Button _collapsedButton;
    private readonly ExposureCardHeaderView _header;
    private readonly ExposureCardContentView _content;
    private readonly ScrollViewer _contentScroller;
    private readonly ExposureCardFooterView _footer;
    private bool _isCollapsed;

    public ExposureCardWpfView()
    {
        CornerRadius = new CornerRadius(10.0);
        Background = CardBackground;
        BorderBrush = CardBorder;
        BorderThickness = new Thickness(1.0);
        Padding = new Thickness(12.0, 12.0, 12.0, 12.0);
        Cursor = Cursors.Arrow;
        SnapsToDevicePixels = true;
        Effect = new DropShadowEffect
        {
            BlurRadius = 12.0,
            ShadowDepth = 3.0,
            Opacity = 0.18,
            Color = Colors.Black
        };
        MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args) { args.Handled = true; };

        _root = new Grid();
        Child = _root;

        _layout = new Grid();
        _layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
        _layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _collapsedButton = CreateCollapsedButton();

        _header = new ExposureCardHeaderView();
        _content = new ExposureCardContentView();
        _contentScroller = new ScrollViewer
        {
            Content = _content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            Focusable = false
        };
        _footer = new ExposureCardFooterView();

        _header.MetricSelected += delegate(object sender, ExposureMetricSelectedEventArgs args) { RaiseMetricSelected(args.Metric); };
        _header.CollapseRequested += delegate(object sender, EventArgs args) { SetCollapsed(true); };
        _footer.ProxySelected += delegate(object sender, ExposureProxySelectedEventArgs args) { RaiseProxySelected(args.ProxySymbol); };
        _footer.DataScopeToggled += delegate(object sender, EventArgs args) { RaiseDataScopeToggled(); };

        Grid.SetRow(_header, 0);
        Grid.SetRow(_contentScroller, 1);
        Grid.SetRow(_footer, 2);
        _layout.Children.Add(_header);
        _layout.Children.Add(_contentScroller);
        _layout.Children.Add(_footer);
        _root.Children.Add(_layout);
        _root.Children.Add(_collapsedButton);
        ApplyCollapsedState();
    }

    public event EventHandler<ExposureMetricSelectedEventArgs> MetricSelected;

    public event EventHandler<ExposureProxySelectedEventArgs> ProxySelected;

    public event EventHandler DataScopeToggled;

    public event EventHandler CollapseToggled;

    public bool IsCollapsed
    {
        get { return _isCollapsed; }
    }

    public void Update(ExposureCardWpfModel model)
    {
        if (model == null)
            return;

        _header.Update(model);
        _content.Update(model);
        _footer.Update(model);
    }

    private void RaiseMetricSelected(OptionExposureMetric metric)
    {
        var handler = MetricSelected;
        if (handler != null)
            handler(this, new ExposureMetricSelectedEventArgs(metric));
    }

    private void RaiseProxySelected(SpotProxySymbol proxySymbol)
    {
        var handler = ProxySelected;
        if (handler != null)
            handler(this, new ExposureProxySelectedEventArgs(proxySymbol));
    }

    private Button CreateCollapsedButton()
    {
        var text = new TextBlock
        {
            Text = "GVC",
            Foreground = ExposureCardPalette.BrandAccentBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0.0),
            Padding = new Thickness(0.0),
            MinWidth = 0.0,
            MinHeight = 0.0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = CreateFlatCollapsedButtonTemplate(),
            Cursor = Cursors.Hand,
            Focusable = true,
            IsTabStop = true
        };
        button.Click += delegate(object sender, RoutedEventArgs args)
        {
            SetCollapsed(false);
        };
        System.Windows.Automation.AutomationProperties.SetName(button, "Expand Gamma Vanna Charm Exposure Card");
        System.Windows.Automation.AutomationProperties.SetHelpText(button, "Click to expand the exposure card");

        return button;
    }

    private static ControlTemplate CreateFlatCollapsedButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        template.VisualTree = contentPresenter;
        return template;
    }

    private void SetCollapsed(bool collapsed)
    {
        if (_isCollapsed == collapsed)
            return;

        _isCollapsed = collapsed;
        ApplyCollapsedState();
        RaiseCollapseToggled();
    }

    private void ApplyCollapsedState()
    {
        if (_layout == null || _collapsedButton == null)
            return;

        _layout.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        _collapsedButton.Visibility = _isCollapsed ? Visibility.Visible : Visibility.Collapsed;
        Padding = _isCollapsed ? new Thickness(0.0) : new Thickness(12.0, 12.0, 12.0, 12.0);
        CornerRadius = new CornerRadius(_isCollapsed ? 8.0 : 10.0);
    }

    private void RaiseDataScopeToggled()
    {
        var handler = DataScopeToggled;
        if (handler != null)
            handler(this, EventArgs.Empty);
    }

    private void RaiseCollapseToggled()
    {
        var handler = CollapseToggled;
        if (handler != null)
            handler(this, EventArgs.Empty);
    }

    internal static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
            brush.Freeze();
        return brush;
    }
}
