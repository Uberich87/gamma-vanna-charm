#region Using declarations
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal sealed partial class ExposureCardContentView
{
    private static StackPanel CreateSectionPanel()
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0.0, 3.0, 0.0, 2.0)
        };
    }

    private static TextBlock CreateMutedText(string text)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.FontFamily,
            FontSize = 11.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
    }

    private static TextBlock CreateSubsectionHeading(string text)
    {
        return CreateSubsectionHeading(text, ExposureCardPalette.MutedBrush);
    }

    private static TextBlock CreateSubsectionHeading(string text, Brush foreground)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = foreground ?? ExposureCardPalette.MutedBrush,
            FontFamily = ExposureCardPalette.MonospaceFontFamily,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0.0, 5.0, 0.0, 5.0)
        };
    }

}
