#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal sealed class ExposureAccordionSection : Border
{
    private static readonly Geometry CollapsedChevronGeometry = CreateChevronGeometry("M 3 1 L 7 5 L 3 9");
    private static readonly Geometry ExpandedChevronGeometry = CreateChevronGeometry("M 1 3 L 5 7 L 9 3");
    private readonly System.Windows.Shapes.Path _chevronIcon;
    private readonly TextBlock _titleText;
    private readonly HelpIcon _helpIcon;
    private readonly Border _contentClip;
    private readonly ContentPresenter _contentPresenter;
    private bool _isExpanded;
    private bool _isLoaded;

    private static Geometry CreateChevronGeometry(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        if (geometry.CanFreeze)
            geometry.Freeze();

        return geometry;
    }

    public ExposureAccordionSection(string title)
    {
        CornerRadius = new CornerRadius(7.0);
        BorderBrush = ExposureCardPalette.BorderBrush;
        BorderThickness = new Thickness(1.0);
        Background = ExposureCardPalette.SectionBrush;
        Margin = new Thickness(0.0, 0.0, 0.0, 6.0);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Child = layout;

        var header = new Grid
        {
            Cursor = Cursors.Hand,
            Margin = new Thickness(12.0, 8.0, 12.0, 8.0)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
        {
            args.Handled = true;
        };
        header.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs args)
        {
            args.Handled = true;
            Toggle();
        };

        _titleText = new TextBlock
        {
            Text = title ?? string.Empty,
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.0,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _helpIcon = new HelpIcon(string.Empty)
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _chevronIcon = new System.Windows.Shapes.Path
        {
            Data = CollapsedChevronGeometry,
            Stroke = ExposureCardPalette.MutedBrush,
            StrokeThickness = 1.4,
            Width = 10.0,
            Height = 10.0,
            Stretch = Stretch.None,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_titleText, 0);
        Grid.SetColumn(_helpIcon, 1);
        Grid.SetColumn(_chevronIcon, 2);
        header.Children.Add(_titleText);
        header.Children.Add(_helpIcon);
        header.Children.Add(_chevronIcon);

        _contentPresenter = new ContentPresenter
        {
            Margin = new Thickness(10.0, 0.0, 10.0, 8.0)
        };
        _contentClip = new Border
        {
            Height = 0.0,
            Opacity = 0.0,
            ClipToBounds = true,
            Child = _contentPresenter
        };

        var separator = new Border
        {
            Height = 1.0,
            Background = ExposureCardPalette.BorderBrush,
            Opacity = 0.55
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(separator, 1);
        Grid.SetRow(_contentClip, 2);
        layout.Children.Add(header);
        layout.Children.Add(separator);
        layout.Children.Add(_contentClip);

        Loaded += delegate(object sender, RoutedEventArgs args)
        {
            _isLoaded = true;
            RefreshExpandedHeight(false);
            QueueRefreshExpandedHeight();
        };
        SizeChanged += delegate(object sender, SizeChangedEventArgs args)
        {
            if (args.WidthChanged)
                RefreshExpandedHeight(false);
        };
    }

    public void SetBody(UIElement body)
    {
        _contentPresenter.Content = body;
        RefreshExpandedHeight(false);
        QueueRefreshExpandedHeight();
    }

    public void SetTitle(string title)
    {
        _titleText.Text = title ?? string.Empty;
    }

    public void SetHelpTooltip(string tooltip)
    {
        if (string.IsNullOrWhiteSpace(tooltip))
        {
            _helpIcon.Visibility = Visibility.Collapsed;
            _helpIcon.SetTooltip(string.Empty);
            _helpIcon.ClosePopup();
            return;
        }

        _helpIcon.SetTooltip(tooltip);
        _helpIcon.Visibility = Visibility.Visible;
    }

    public void SetExpanded(bool expanded, bool animate)
    {
        _isExpanded = expanded;
        _chevronIcon.Data = expanded ? ExpandedChevronGeometry : CollapsedChevronGeometry;
        RefreshExpandedHeight(animate);
    }

    private void Toggle()
    {
        SetExpanded(!_isExpanded, true);
    }

    private void QueueRefreshExpandedHeight()
    {
        var dispatcher = Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return;

        dispatcher.BeginInvoke(new Action(delegate { RefreshExpandedHeight(false); }), DispatcherPriority.Loaded);
    }

    private void RefreshExpandedHeight(bool animate)
    {
        if (!_isLoaded && ActualWidth <= 0.0)
            return;

        var targetHeight = _isExpanded ? MeasureContentHeight() : 0.0;
        var targetOpacity = _isExpanded ? 1.0 : 0.0;

        if (!animate)
        {
            _contentClip.BeginAnimation(HeightProperty, null);
            _contentClip.BeginAnimation(OpacityProperty, null);
            _contentClip.Height = targetHeight;
            _contentClip.Opacity = targetOpacity;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(180.0));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        _contentClip.BeginAnimation(HeightProperty, new DoubleAnimation(targetHeight, duration) { EasingFunction = easing });
        _contentClip.BeginAnimation(OpacityProperty, new DoubleAnimation(targetOpacity, duration) { EasingFunction = easing });
    }

    private double MeasureContentHeight()
    {
        var width = ActualWidth;
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 1.0)
            width = 272.0;

        _contentPresenter.Measure(new Size(Math.Max(1.0, width - 20.0), double.PositiveInfinity));
        return Math.Max(0.0, _contentPresenter.DesiredSize.Height);
    }
}

internal sealed class HelpIcon : Border
{
    private readonly System.Windows.Controls.Primitives.Popup _popup;
    private readonly TextBlock _tooltipText;
    private readonly DispatcherTimer _closeTimer;
    private bool _isIconHovering;
    private bool _isPopupHovering;

    public HelpIcon(string tooltip)
    {
        Width = 16.0;
        Height = 16.0;
        CornerRadius = new CornerRadius(8.0);
        BorderBrush = ExposureCardPalette.MutedBrush;
        BorderThickness = new Thickness(1.0);
        Background = Brushes.Transparent;
        Cursor = Cursors.Help;
        Child = new TextBlock
        {
            Text = "?",
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.0,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        _tooltipText = CreateTooltipText(tooltip);
        var hoverCard = CreateHoverCard(_tooltipText);
        hoverCard.MouseEnter += delegate(object sender, MouseEventArgs args)
        {
            _isPopupHovering = true;
            _closeTimer.Stop();
        };
        hoverCard.MouseLeave += delegate(object sender, MouseEventArgs args)
        {
            _isPopupHovering = false;
            ScheduleClose();
        };

        _popup = new System.Windows.Controls.Primitives.Popup
        {
            AllowsTransparency = true,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            PlacementTarget = this,
            StaysOpen = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
            Child = hoverCard
        };

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160.0) };
        _closeTimer.Tick += delegate(object sender, EventArgs args)
        {
            _closeTimer.Stop();
            if (!_isIconHovering && !_isPopupHovering)
                _popup.IsOpen = false;
        };

        MouseEnter += delegate(object sender, MouseEventArgs args)
        {
            _isIconHovering = true;
            _closeTimer.Stop();
            _popup.IsOpen = true;
        };
        MouseLeave += delegate(object sender, MouseEventArgs args)
        {
            _isIconHovering = false;
            ScheduleClose();
        };
        MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args) { args.Handled = true; };
        MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs args) { args.Handled = true; };
        Unloaded += delegate(object sender, RoutedEventArgs args)
        {
            _closeTimer.Stop();
            _popup.IsOpen = false;
        };
    }

    public void SetTooltip(string tooltip)
    {
        ApplyTooltipText(_tooltipText, tooltip);
    }

    public void ClosePopup()
    {
        _closeTimer.Stop();
        if (_popup != null)
            _popup.IsOpen = false;
    }

    private void ScheduleClose()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    private static Border CreateHoverCard(UIElement content)
    {
        return new Border
        {
            Background = ExposureCardPalette.SectionBrush,
            BorderBrush = ExposureCardPalette.BorderBrush,
            BorderThickness = new Thickness(1.0),
            CornerRadius = new CornerRadius(6.0),
            Padding = new Thickness(9.0, 7.0, 9.0, 7.0),
            Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
            Effect = new DropShadowEffect
            {
                BlurRadius = 8.0,
                ShadowDepth = 2.0,
                Opacity = 0.22,
                Color = Colors.Black
            },
            Child = content
        };
    }

    private static TextBlock CreateTooltipText(string text)
    {
        var textBlock = new TextBlock
        {
            Foreground = ExposureCardPalette.BodyBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.5,
            LineHeight = 16.5,
            MaxWidth = 340.0,
            TextWrapping = TextWrapping.Wrap
        };
        ApplyTooltipText(textBlock, text);
        return textBlock;
    }

    private static void ApplyTooltipText(TextBlock textBlock, string text)
    {
        if (textBlock == null)
            return;

        textBlock.Inlines.Clear();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
        var previousBlank = true;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index] ?? string.Empty;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
                previousBlank = true;
                continue;
            }

            if (IsTooltipHeading(trimmed, previousBlank))
            {
                textBlock.Inlines.Add(new System.Windows.Documents.Run(trimmed)
                {
                    Foreground = ExposureCardPalette.TextBrush,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12.8
                });
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal))
            {
                textBlock.Inlines.Add(new System.Windows.Documents.Run(trimmed)
                {
                    Foreground = ExposureCardPalette.MutedBrush,
                    FontSize = 11.0
                });
            }
            else
            {
                AddTooltipDescriptionLine(textBlock, trimmed);
            }

            if (index < lines.Length - 1)
                textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
            previousBlank = false;
        }
    }

    private static bool IsTooltipHeading(string line, bool previousBlank)
    {
        if (!previousBlank || line.IndexOf(':') >= 0 || line.StartsWith("•", StringComparison.Ordinal))
            return false;

        return line.Length <= 34;
    }

    private static void AddTooltipDescriptionLine(TextBlock textBlock, string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0 && colonIndex <= 18)
        {
            textBlock.Inlines.Add(new System.Windows.Documents.Run(line.Substring(0, colonIndex + 1))
            {
                Foreground = ExposureCardPalette.BrandAccentBrush,
                FontWeight = FontWeights.SemiBold
            });
            textBlock.Inlines.Add(new System.Windows.Documents.Run(" " + line.Substring(colonIndex + 1).Trim())
            {
                Foreground = ExposureCardPalette.BodyBrush
            });
            return;
        }

        textBlock.Inlines.Add(new System.Windows.Documents.Run(line)
        {
            Foreground = ExposureCardPalette.BodyBrush
        });
    }
}

internal sealed class ExposurePill : Border
{
    private readonly TextBlock _textBlock;
    private Brush _accentBrush;
    private Brush _accentBackgroundBrush;
    private bool _isSelected;
    private bool _isHovering;

    public ExposurePill(string text)
    {
        CornerRadius = new CornerRadius(6.0);
        BorderThickness = new Thickness(1.0);
        Padding = new Thickness(7.0, 2.0, 7.0, 2.0);
        Cursor = Cursors.Hand;
        Background = ExposureCardPalette.PillBrush;
        BorderBrush = Brushes.Transparent;
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, true);

        _textBlock = new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        Child = _textBlock;

        MouseEnter += delegate(object sender, MouseEventArgs args)
        {
            _isHovering = true;
            ApplyState();
        };
        MouseLeave += delegate(object sender, MouseEventArgs args)
        {
            _isHovering = false;
            ApplyState();
        };
        MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
        {
            args.Handled = true;
        };
        MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs args)
        {
            args.Handled = true;
            var handler = Clicked;
            if (handler != null)
                handler(this, EventArgs.Empty);
        };
        KeyDown += delegate(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter || args.Key == Key.Space)
            {
                args.Handled = true;
                _isHovering = true;
                ApplyState();
            }
        };
        KeyUp += delegate(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter || args.Key == Key.Space)
            {
                args.Handled = true;
                _isHovering = false;
                ApplyState();
                var handler = Clicked;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler Clicked;

    public string Text
    {
        get { return _textBlock.Text; }
        set { _textBlock.Text = value ?? string.Empty; }
    }

    public void SetSelected(bool selected)
    {
        SetSelected(selected, null);
    }

    public void SetSelected(bool selected, Brush accentBrush)
    {
        _isSelected = selected;
        _accentBrush = accentBrush;
        _accentBackgroundBrush = CreateAccentBackgroundBrush(accentBrush);
        ApplyState();
    }

    private void ApplyState()
    {
        if (_isSelected)
        {
            Background = _accentBackgroundBrush ?? ExposureCardPalette.PillSelectedBrush;
            BorderBrush = _accentBrush ?? ExposureCardPalette.BorderBrush;
            Opacity = 1.0;
            _textBlock.Foreground = _accentBrush ?? ExposureCardPalette.TextBrush;
            _textBlock.Opacity = 1.0;
            return;
        }

        Background = _isHovering ? ExposureCardPalette.PillHoverBrush : ExposureCardPalette.PillBrush;
        BorderBrush = ExposureCardPalette.BorderBrush;
        Opacity = _isHovering ? 1.0 : 0.88;
        _textBlock.Foreground = _accentBrush ?? (_isHovering ? ExposureCardPalette.TextBrush : ExposureCardPalette.MutedBrush);
        _textBlock.Opacity = _accentBrush == null ? 1.0 : (_isHovering ? 0.85 : 0.62);
    }

    private static Brush CreateAccentBackgroundBrush(Brush accentBrush)
    {
        var solidBrush = accentBrush as SolidColorBrush;
        if (solidBrush == null)
            return null;

        var color = solidBrush.Color;
        return ExposureCardWpfView.CreateFrozenBrush(Color.FromArgb(44, color.R, color.G, color.B));
    }
}

internal static class ExposureCardPalette
{
    public static readonly FontFamily FontFamily = new FontFamily("Segoe UI");
    public static readonly FontFamily MonospaceFontFamily = new FontFamily("Segoe UI");
    public static readonly Brush TextBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(248, 250, 252));
    public static readonly Brush BrandAccentBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(125, 211, 252));
    public static readonly Brush BodyBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(196, 208, 221));
    public static readonly Brush MutedBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(148, 163, 184));
    public static readonly Brush NeutralBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(148, 163, 184));
    public static readonly Brush PositiveBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(56, 189, 248));
    public static readonly Brush NegativeBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(248, 113, 113));
    public static readonly Brush BorderBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromArgb(78, 73, 86, 101));
    public static readonly Brush SectionBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(12, 18, 23));
    public static readonly Brush HeroBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(8, 12, 16));
    public static readonly Brush RangePanelBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(8, 25, 51));
    public static readonly Brush BalanceBoxBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(10, 15, 20));
    public static readonly Brush PillBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(13, 21, 28));
    public static readonly Brush PillHoverBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(18, 30, 39));
    public static readonly Brush PillSelectedBrush = ExposureCardWpfView.CreateFrozenBrush(Color.FromRgb(22, 33, 43));
}
