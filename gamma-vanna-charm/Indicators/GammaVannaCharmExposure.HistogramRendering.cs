#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows.Media;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
using NinjaTrader.Gui.Chart;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    private Brush GetPositiveExposureBrush()
    {
        if (SelectedExposureMetric == OptionExposureMetric.Vanna)
            return VannaPositiveExposureBrush ?? Brushes.MediumPurple;
        if (SelectedExposureMetric == OptionExposureMetric.Charm)
            return CharmPositiveExposureBrush ?? Brushes.MediumSeaGreen;

        return GammaPositiveExposureBrush ?? Brushes.DodgerBlue;
    }

    private Brush GetNegativeExposureBrush()
    {
        if (SelectedExposureMetric == OptionExposureMetric.Vanna)
            return VannaNegativeExposureBrush ?? Brushes.Goldenrod;
        if (SelectedExposureMetric == OptionExposureMetric.Charm)
            return CharmNegativeExposureBrush ?? Brushes.DeepPink;

        return GammaNegativeExposureBrush ?? Brushes.Firebrick;
    }

    private SharpDX.Color4 GetBrushColor(Brush sourceBrush, Color fallbackColor)
    {
        var brush = sourceBrush as SolidColorBrush;
        var color = brush != null ? brush.Color : fallbackColor;
        return new SharpDX.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
    }

    private double GetHistogramRowLowerPrice(HistogramRenderLevel level)
    {
        return ExposureHistogramGeometry.GetRowLowerPrice(level, ExposureFuturesFamily, ResolvedExposureProxySymbol);
    }

    private double GetHistogramRowUpperPrice(HistogramRenderLevel level)
    {
        return ExposureHistogramGeometry.GetRowUpperPrice(level, ExposureFuturesFamily, ResolvedExposureProxySymbol);
    }

    private float GetProfileMaxWidth()
    {
        return ChartPanel.W * Math.Max(1, Math.Min(100, ExposureHistogramMaxWidthPercent)) / 100.0f;
    }

    protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
    {
        base.OnRender(chartControl, chartScale);

        if (RenderTarget == null || ChartPanel == null)
            return;

        var renderLevels = GetHistogramRenderLevelsSnapshot();
        if (renderLevels.Count == 0)
        {
            UpdateExposureStatusOverlay(LastExposureStatusText);
            return;
        }

        var maxWidth = GetProfileMaxWidth();
        SharpDX.Direct2D1.SolidColorBrush positiveBrush;
        SharpDX.Direct2D1.SolidColorBrush negativeBrush;
        if (maxWidth > 1.0f && TryEnsureHistogramRenderBrushes(out positiveBrush, out negativeBrush))
        {
            var right = (float)(ChartPanel.X + ChartPanel.W);
            var panelTop = (float)ChartPanel.Y;
            var panelBottom = (float)(ChartPanel.Y + ChartPanel.H);
            for (var index = 0; index < renderLevels.Count; index++)
            {
                var level = renderLevels[index];
                var lowerPrice = GetHistogramRowLowerPrice(level);
                var upperPrice = GetHistogramRowUpperPrice(level);

                var upperY = chartScale.GetYByValue(upperPrice);
                var lowerY = chartScale.GetYByValue(lowerPrice);
                if (!IsRenderableCoordinate(upperY) || !IsRenderableCoordinate(lowerY))
                    continue;

                var topY = (float)Math.Min(upperY, lowerY);
                var bottomY = (float)Math.Max(upperY, lowerY);
                if (bottomY < panelTop || topY > panelBottom)
                    continue;

                topY = Math.Max(panelTop, topY);
                bottomY = Math.Min(panelBottom, bottomY);
                var height = Math.Max(2.0f, bottomY - topY - 2.0f);
                var width = Math.Max(2.0f, maxWidth * (float)Math.Min(1.0, Math.Max(0.0, level.Intensity)));
                var x = right - width;
                var y = topY + 1.0f;
                var brush = level.MetricValue >= 0.0 ? positiveBrush : negativeBrush;
                brush.Opacity = (float)Math.Min(1.0, Math.Max(0.08, level.Opacity));
                RenderTarget.FillRectangle(new SharpDX.RectangleF(x, y, width, height), brush);
            }
        }

        UpdateExposureCardOverlay(renderLevels);
    }

    private bool TryEnsureHistogramRenderBrushes(out SharpDX.Direct2D1.SolidColorBrush positiveBrush, out SharpDX.Direct2D1.SolidColorBrush negativeBrush)
    {
        positiveBrush = null;
        negativeBrush = null;
        if (RenderTarget == null)
            return false;

        var positiveColor = GetBrushColor(GetPositiveExposureBrush(), Colors.DodgerBlue);
        var negativeColor = GetBrushColor(GetNegativeExposureBrush(), Colors.Firebrick);
        if (_positiveHistogramRenderBrush == null
            || _negativeHistogramRenderBrush == null
            || !ReferenceEquals(_histogramRenderBrushTarget, RenderTarget)
            || !_hasHistogramRenderBrushColors
            || !positiveColor.Equals(_positiveHistogramRenderBrushColor)
            || !negativeColor.Equals(_negativeHistogramRenderBrushColor))
        {
            DisposeHistogramRenderResources();
            _histogramRenderBrushTarget = RenderTarget;
            _positiveHistogramRenderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, positiveColor);
            _negativeHistogramRenderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, negativeColor);
            _positiveHistogramRenderBrushColor = positiveColor;
            _negativeHistogramRenderBrushColor = negativeColor;
            _hasHistogramRenderBrushColors = true;
        }

        positiveBrush = _positiveHistogramRenderBrush;
        negativeBrush = _negativeHistogramRenderBrush;
        return positiveBrush != null && negativeBrush != null;
    }

    private void DisposeHistogramRenderResources()
    {
        if (_positiveHistogramRenderBrush != null)
        {
            _positiveHistogramRenderBrush.Dispose();
            _positiveHistogramRenderBrush = null;
        }

        if (_negativeHistogramRenderBrush != null)
        {
            _negativeHistogramRenderBrush.Dispose();
            _negativeHistogramRenderBrush = null;
        }

        _histogramRenderBrushTarget = null;
        _hasHistogramRenderBrushColors = false;
    }

    private static bool IsRenderableCoordinate(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
