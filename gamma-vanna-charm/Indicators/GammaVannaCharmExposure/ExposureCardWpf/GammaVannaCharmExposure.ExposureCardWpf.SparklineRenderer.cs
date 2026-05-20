#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal static class ExposureCardSparklineRenderer
{
    public static void Draw(Canvas canvas, IList<double> values, Brush brush, double fallbackWidth, double fallbackHeight, double strokeThickness)
    {
        if (canvas == null)
            return;

        canvas.Children.Clear();
        if (values == null || values.Count < 2)
            return;

        var validValues = new List<double>(values.Count);
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;

            validValues.Add(value);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (validValues.Count < 2)
            return;
        if (Math.Abs(max - min) < 0.0000001)
        {
            min -= 1.0;
            max += 1.0;
        }

        var width = canvas.ActualWidth > 1.0 ? canvas.ActualWidth : fallbackWidth;
        var height = canvas.ActualHeight > 1.0 ? canvas.ActualHeight : fallbackHeight;
        var points = new PointCollection();
        for (var index = 0; index < validValues.Count; index++)
        {
            var x = validValues.Count == 1 ? 0.0 : width * index / (validValues.Count - 1);
            var normalized = (validValues[index] - min) / (max - min);
            var y = height - (normalized * height);
            points.Add(new Point(x, Math.Max(1.0, Math.Min(height - 1.0, y))));
        }

        canvas.Children.Add(new System.Windows.Shapes.Polyline
        {
            Points = points,
            Stroke = brush ?? ExposureCardPalette.BrandAccentBrush,
            StrokeThickness = strokeThickness,
            SnapsToDevicePixels = true
        });
    }
}
