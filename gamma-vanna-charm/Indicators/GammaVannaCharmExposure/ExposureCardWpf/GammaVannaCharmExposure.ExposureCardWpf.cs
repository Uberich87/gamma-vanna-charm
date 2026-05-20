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

public partial class GammaVannaCharmExposure
{
    private ExposureCardWpfView _exposureCardOverlay;
    private long _lastExposureCardOverlaySignature;
    private bool _hasLastExposureCardOverlaySignature;

    private void UpdateExposureCardOverlay(IList<HistogramRenderLevel> renderLevels)
    {
        if (renderLevels == null || renderLevels.Count == 0)
        {
            UpdateExposureStatusOverlay(LastExposureStatusText);
            return;
        }

        var persistenceStatus = GetExposurePersistenceStatus();
        if (!string.IsNullOrWhiteSpace(persistenceStatus))
        {
            UpdateExposureStatusOverlay(persistenceStatus);
            return;
        }

        if (_isTerminated)
            return;

        if (!HasExposureCardOverlayAccess())
        {
            var renderLevelsSnapshot = new List<HistogramRenderLevel>(renderLevels);
            BeginExposureCardOverlayAction(delegate { UpdateExposureCardOverlay(renderLevelsSnapshot); });
            return;
        }

        if (!EnsureExposureCardOverlay())
            return;

        var signature = BuildExposureCardOverlaySignature(renderLevels);
        if (!_hasLastExposureCardOverlaySignature || _lastExposureCardOverlaySignature != signature)
        {
            var model = BuildExposureCardOverlayModel(renderLevels);
            _exposureCardOverlay.Update(model);
            _lastExposureCardOverlaySignature = signature;
            _hasLastExposureCardOverlaySignature = true;
        }

        PositionExposureCardOverlay();
    }

    private void UpdateExposureStatusOverlay(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            RemoveExposureCardOverlay();
            return;
        }

        if (_isTerminated)
            return;

        if (!HasExposureCardOverlayAccess())
        {
            var statusTextSnapshot = statusText;
            BeginExposureCardOverlayAction(delegate { UpdateExposureStatusOverlay(statusTextSnapshot); });
            return;
        }

        if (!EnsureExposureCardOverlay())
            return;

        var signature = BuildExposureStatusOverlaySignature(statusText);
        if (!_hasLastExposureCardOverlaySignature || _lastExposureCardOverlaySignature != signature)
        {
            var model = ExposureCardWpfModel.CreateStatus(
                ExposureCardTextFormatter.GetStatusBadgeText(statusText),
                ExposureCardTextFormatter.CleanStatusText(statusText),
                ExposureCardTextFormatter.FormatUtcDateTime(GetOptionsLastCheckedUtc()),
                ExposureCardTextFormatter.FormatUtcDateTime(GetOptionsLastUpdatedUtc()),
                GetCurrentVersionText(),
                GetVersionUpdateLinkText(),
                GetVersionUpdateLinkUrl());

            _exposureCardOverlay.Update(model);
            _lastExposureCardOverlaySignature = signature;
            _hasLastExposureCardOverlaySignature = true;
        }

        PositionExposureCardOverlay();
    }

    private ExposureCardWpfModel BuildExposureCardOverlayModel(IList<HistogramRenderLevel> renderLevels)
    {
        var gammaLevels = ExposureMetric == OptionExposureMetric.Gamma || ExposureMetric == OptionExposureMetric.Overall ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Gamma);
        var vannaLevels = ExposureMetric == OptionExposureMetric.Vanna ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Vanna);
        var charmLevels = ExposureMetric == OptionExposureMetric.Charm ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Charm);
        var insight = ExposureMetric == OptionExposureMetric.Overall
            ? ExposureCardInsightBuilder.BuildOverall(
                gammaLevels,
                vannaLevels,
                charmLevels,
                _latestFuturesPrice,
                _hasLatestFuturesPrice,
                5,
                ActivePositionModel == OptionPositionModel.FlowImpliedDealer)
            : ExposureCardInsightBuilder.Build(
                renderLevels,
                ExposureMetric,
                _latestFuturesPrice,
                _hasLatestFuturesPrice,
                5);

        var proxyButtons = new List<ExposureCardProxyButtonModel>();
        if (HasExposureProxyButtons())
        {
            for (var index = 0; index < GetExposureProxyButtonCount(); index++)
            {
                var proxy = GetExposureProxyByButtonIndex(index);
                proxyButtons.Add(new ExposureCardProxyButtonModel(proxy, GetExposureProxyButtonText(index), proxy == _resolvedExposureProxySymbol));
            }
        }
        else if (_resolvedExposureProxySymbol == SpotProxySymbol.QQQ)
        {
            proxyButtons.Add(new ExposureCardProxyButtonModel(SpotProxySymbol.QQQ, "QQQ", true));
        }

        var selectedMetric = ExposureMetricPresentation.GetPrimaryMetric(ExposureMetric);
        return ExposureCardWpfModel.CreateExposure(
            GetExposureCardHeaderSymbolText(),
            ExposureMetric,
            ActivePositionModel == OptionPositionModel.FlowImpliedDealer ? "FLOW" : "NAIVE",
            UseZeroDteOptions ? "0DTE" : "ALL",
            ExposureCardTextFormatter.FormatUtcDateTime(GetOptionsLastCheckedUtc()),
            ExposureCardTextFormatter.FormatUtcDateTime(GetOptionsLastUpdatedUtc()),
            proxyButtons,
            BuildExposureCardSparklines(),
            BuildExposureCardStrikeTrendText(),
            BuildExposureCardStrikeBiasText(gammaLevels, vannaLevels, charmLevels),
            GetExposureStrikeTrendSnapshot(selectedMetric),
            BuildExposureCardIvConditionText(),
            BuildExposureCardStrikeSparklines(insight),
            insight,
            CreateExposureCardBrush(GetPositiveExposureBrush(), Colors.DodgerBlue),
            CreateExposureCardBrush(GetNegativeExposureBrush(), Colors.Firebrick),
            CreateExposureCardBrush(GammaPositiveExposureBrush, Colors.DodgerBlue),
            CreateExposureCardBrush(VannaPositiveExposureBrush, Colors.MediumPurple),
            CreateExposureCardBrush(CharmPositiveExposureBrush, Colors.MediumSeaGreen),
            GetCurrentVersionText(),
            GetVersionUpdateLinkText(),
            GetVersionUpdateLinkUrl());
    }

    private string BuildExposureCardStrikeBiasText(
        IList<HistogramRenderLevel> gammaLevels,
        IList<HistogramRenderLevel> vannaLevels,
        IList<HistogramRenderLevel> charmLevels)
    {
        var snapshotTimestamp = GetOptionsLastUpdatedUtc();
        return ExposureCardStrikeBiasBuilder.Build(
            gammaLevels,
            vannaLevels,
            charmLevels,
            _latestFuturesPrice,
            _hasLatestFuturesPrice,
            snapshotTimestamp != DateTime.MinValue ? snapshotTimestamp : DateTime.UtcNow);
    }

    private IList<ExposureCardStrikeSparklineModel> BuildExposureCardStrikeSparklines(ExposureCardInsight insight)
    {
        var result = new List<ExposureCardStrikeSparklineModel>();
        if (insight == null || insight.Levels == null || insight.Levels.Count == 0)
            return result;

        var metric = ExposureMetricPresentation.GetPrimaryMetric(ExposureMetric);
        for (var index = 0; index < insight.Levels.Count; index++)
        {
            var level = insight.Levels[index];
            if (level == null)
                continue;

            var values = GetStrikeExposureHistorySnapshot(metric, level.Strike);
            if (values.Count >= 2)
                result.Add(new ExposureCardStrikeSparklineModel(level.Strike, values));
        }

        return result;
    }

    private string BuildExposureCardStrikeTrendText()
    {
        var metric = ExposureMetricPresentation.GetPrimaryMetric(ExposureMetric);
        var trend = GetExposureStrikeTrendSnapshot(metric);
        var metricText = ExposureMetricPresentation.GetAbbreviation(metric);
        if (trend == null)
            return metricText + " strike trend waiting for 2 persisted level snapshots.";

        var centerChange = trend.WeightedStrikeChange;
        var centerDirection = Math.Abs(centerChange) < 0.0000001 ? "center flat" : centerChange > 0.0 ? "center migrating higher" : "center migrating lower";
        var centerText = centerDirection;
        if (Math.Abs(centerChange) >= 0.0000001)
            centerText += " " + Math.Abs(centerChange).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " pts";

        if (trend.HasRotation)
        {
            return metricText + " top strike rotating "
                + trend.FirstTopStrike.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + " → "
                + trend.LatestTopStrike.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + "; " + centerText + ".";
        }

        return metricText + " top strike holding "
            + trend.LatestTopStrike.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "; " + centerText + ".";
    }

    private string BuildExposureCardIvConditionText()
    {
        double latest;
        double previous;
        bool hasLatest;
        bool hasPrevious;
        lock (_optionDataProcessingSyncRoot)
        {
            latest = _latestWeightedImpliedVolatility;
            previous = _previousWeightedImpliedVolatility;
            hasLatest = _hasLatestWeightedImpliedVolatility;
            hasPrevious = _hasPreviousWeightedImpliedVolatility;
        }

        return ExposureCardIvConditionAnalyzer.Analyze(latest, previous, hasLatest, hasPrevious);
    }

    private IList<ExposureCardSparklineModel> BuildExposureCardSparklines()
    {
        var historyPoints = GetRecentExposureHistoryPointsSnapshot();
        if (historyPoints.Count < 2)
            return new List<ExposureCardSparklineModel>();

        var gammaValues = new List<double>();
        var vannaValues = new List<double>();
        var charmValues = new List<double>();
        for (var index = 0; index < historyPoints.Count; index++)
        {
            var point = historyPoints[index];
            gammaValues.Add(point.AbsoluteGammaExposure > 0.0 ? point.AbsoluteGammaExposure : Math.Abs(point.GammaExposure));
            vannaValues.Add(point.AbsoluteVannaExposure > 0.0 ? point.AbsoluteVannaExposure : Math.Abs(point.VannaExposure));
            charmValues.Add(point.AbsoluteCharmExposure > 0.0 ? point.AbsoluteCharmExposure : Math.Abs(point.CharmExposure));
        }

        var sparklines = new List<ExposureCardSparklineModel>();
        if (ExposureMetric == OptionExposureMetric.Overall || ExposureMetric == OptionExposureMetric.Gamma)
            sparklines.Add(CreateSparklineModel(ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Gamma), gammaValues, GammaPositiveExposureBrush, GammaNegativeExposureBrush, Colors.DodgerBlue, Colors.Firebrick));
        if (ExposureMetric == OptionExposureMetric.Overall || ExposureMetric == OptionExposureMetric.Vanna)
            sparklines.Add(CreateSparklineModel(ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Vanna), vannaValues, VannaPositiveExposureBrush, VannaNegativeExposureBrush, Colors.MediumPurple, Colors.Goldenrod));
        if (ExposureMetric == OptionExposureMetric.Overall || ExposureMetric == OptionExposureMetric.Charm)
            sparklines.Add(CreateSparklineModel(ExposureMetricPresentation.GetAbbreviation(OptionExposureMetric.Charm), charmValues, CharmPositiveExposureBrush, CharmNegativeExposureBrush, Colors.MediumSeaGreen, Colors.DeepPink));

        return sparklines;
    }

    private static ExposureCardSparklineModel CreateSparklineModel(
        string label,
        IList<double> values,
        Brush positiveBrush,
        Brush negativeBrush,
        Color positiveFallbackColor,
        Color negativeFallbackColor)
    {
        var pressureChange = CalculateExposurePressureChange(values);
        var brush = pressureChange < 0.0
            ? CreateExposureCardBrush(negativeBrush, negativeFallbackColor)
            : CreateExposureCardBrush(positiveBrush, positiveFallbackColor);
        return new ExposureCardSparklineModel(label, FormatExposureTrendChange(values), values, brush);
    }

    private static string FormatExposureTrendChange(IList<double> values)
    {
        if (values == null || values.Count < 2)
            return string.Empty;

        return ExposureCardTextFormatter.FormatSignedCompactExposure(CalculateExposurePressureChange(values));
    }

    private static double CalculateExposurePressureChange(IList<double> values)
    {
        if (values == null || values.Count < 2)
            return 0.0;

        return Math.Abs(values[values.Count - 1]) - Math.Abs(values[0]);
    }

    private List<HistogramRenderLevel> BuildExposureCardMetricRenderLevels(OptionExposureMetric metric)
    {
        List<OptionStrikeExposure> strikeExposures;
        FuturesFamily futuresFamily;
        SpotProxySymbol proxySymbol;
        SpotProxyConversionMultipliers proxyConversionMultipliers;
        double underlyingPrice;
        double futuresAnchorPrice;
        double tickSize;
        double heatmapBandTicks;
        int heatmapMaxOpacity;

        lock (_optionDataProcessingSyncRoot)
        {
            if (_optionStrikeExposures == null || _optionStrikeExposures.Count == 0 || _underlyingPrice <= 0.0 || _futuresAnchorPrice <= 0.0)
                return new List<HistogramRenderLevel>();

            strikeExposures = new List<OptionStrikeExposure>(_optionStrikeExposures);
            futuresFamily = _exposureFuturesFamily;
            proxySymbol = _resolvedExposureProxySymbol;
            proxyConversionMultipliers = CloneExposureCardProxyConversionMultipliers(_proxyConversionMultipliers);
            underlyingPrice = _underlyingPrice;
            futuresAnchorPrice = _futuresAnchorPrice;
            tickSize = TickSize;
            heatmapBandTicks = HeatmapBandTicks;
            heatmapMaxOpacity = HeatmapMaxOpacity;
        }

        string status;
        return ExposureHistogramBuilder.Build(
            strikeExposures,
            futuresFamily,
            proxySymbol,
            metric,
            underlyingPrice,
            0.0,
            futuresAnchorPrice,
            proxyConversionMultipliers,
            tickSize,
            heatmapBandTicks,
            heatmapMaxOpacity,
            out status);
    }

    private static SpotProxyConversionMultipliers CloneExposureCardProxyConversionMultipliers(SpotProxyConversionMultipliers source)
    {
        if (source == null)
            return null;

        return new SpotProxyConversionMultipliers
        {
            SpyToEs = source.SpyToEs,
            SpxToEs = source.SpxToEs,
            QqqToNq = source.QqqToNq
        };
    }

    private static Brush CreateExposureCardBrush(Brush sourceBrush, Color fallbackColor)
    {
        var solidBrush = sourceBrush as SolidColorBrush;
        var color = solidBrush != null ? solidBrush.Color : fallbackColor;
        return ExposureCardWpfView.CreateFrozenBrush(color);
    }

    private bool HasExposureCardOverlayAccess()
    {
        var dispatcher = GetExposureCardOverlayDispatcher();
        if (dispatcher == null)
            return true;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return false;

        return dispatcher.CheckAccess();
    }

    private Dispatcher GetExposureCardOverlayDispatcher()
    {
        var overlay = _exposureCardOverlay;
        if (overlay != null && overlay.Dispatcher != null)
            return overlay.Dispatcher;

        var chartControl = ChartControl;
        if (chartControl != null && chartControl.Dispatcher != null)
            return chartControl.Dispatcher;

        return _chartDispatcher;
    }

    private void BeginExposureCardOverlayAction(Action action)
    {
        if (action == null)
            return;

        var dispatcher = GetExposureCardOverlayDispatcher();
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return;

        try
        {
            dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private long BuildExposureCardOverlaySignature(IList<HistogramRenderLevel> renderLevels)
    {
        unchecked
        {
            var signature = 29L;
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Build(renderLevels);
            var gammaLevels = ExposureMetric == OptionExposureMetric.Gamma || ExposureMetric == OptionExposureMetric.Overall ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Gamma);
            var vannaLevels = ExposureMetric == OptionExposureMetric.Vanna ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Vanna);
            var charmLevels = ExposureMetric == OptionExposureMetric.Charm ? renderLevels : BuildExposureCardMetricRenderLevels(OptionExposureMetric.Charm);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Build(gammaLevels);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Build(vannaLevels);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Build(charmLevels);
            signature = (signature * 31L) + 1L;
            signature = (signature * 31L) + (int)ExposureMetric;
            signature = (signature * 31L) + (int)ActivePositionModel;
            signature = (signature * 31L) + (UseZeroDteOptions ? 1L : 0L);
            signature = (signature * 31L) + (int)_resolvedExposureProxySymbol;
            signature = (signature * 31L) + (_hasLatestFuturesPrice ? 1L : 0L);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(_latestFuturesPrice, 100.0);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(_underlyingPrice, 100.0);
            signature = (signature * 31L) + GetOptionsLastCheckedUtc().Ticks;
            signature = (signature * 31L) + GetOptionsLastUpdatedUtc().Ticks;
            signature = (signature * 31L) + BuildExposureHistorySignature();
            var selectedMetric = ExposureMetricPresentation.GetPrimaryMetric(ExposureMetric);
            signature = (signature * 31L) + BuildExposureStrikeTrendSignature(GetExposureStrikeTrendSnapshot(selectedMetric));
            signature = (signature * 31L) + BuildExposureCardStringSignature(BuildExposureCardStrikeBiasText(gammaLevels, vannaLevels, charmLevels));
            signature = (signature * 31L) + BuildExposureCardStringSignature(BuildExposureCardIvConditionText());
            signature = (signature * 31L) + BuildExposureCardStringSignature(GetExposureCardHeaderSymbolText());
            signature = (signature * 31L) + BuildExposureCardBrushSignature(GetPositiveExposureBrush(), Colors.DodgerBlue);
            signature = (signature * 31L) + BuildExposureCardBrushSignature(GetNegativeExposureBrush(), Colors.Firebrick);
            signature = (signature * 31L) + BuildExposureCardBrushSignature(GammaPositiveExposureBrush, Colors.DodgerBlue);
            signature = (signature * 31L) + BuildExposureCardBrushSignature(VannaPositiveExposureBrush, Colors.MediumPurple);
            signature = (signature * 31L) + BuildExposureCardBrushSignature(CharmPositiveExposureBrush, Colors.MediumSeaGreen);
            signature = (signature * 31L) + BuildExposureCardStringSignature(GetCurrentVersionText());
            signature = (signature * 31L) + BuildVersionUpdateSignature();
            return signature;
        }
    }

    private long BuildExposureHistorySignature()
    {
        unchecked
        {
            var historyPoints = GetRecentExposureHistoryPointsSnapshot();
            var signature = 23L;
            for (var index = 0; index < historyPoints.Count; index++)
            {
                var point = historyPoints[index];
                signature = (signature * 31L) + point.TimestampUtc.Ticks;
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.GammaExposure, 100.0);
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.VannaExposure, 100.0);
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.CharmExposure, 100.0);
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.AbsoluteGammaExposure, 100.0);
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.AbsoluteVannaExposure, 100.0);
                signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(point.AbsoluteCharmExposure, 100.0);
            }

            return signature;
        }
    }

    private long BuildExposureStatusOverlaySignature(string statusText)
    {
        unchecked
        {
            var signature = 37L;
            signature = (signature * 31L) + 2L;
            signature = (signature * 31L) + BuildExposureCardStringSignature(statusText);
            signature = (signature * 31L) + GetOptionsLastCheckedUtc().Ticks;
            signature = (signature * 31L) + GetOptionsLastUpdatedUtc().Ticks;
            signature = (signature * 31L) + BuildExposureCardStringSignature(GetCurrentVersionText());
            signature = (signature * 31L) + BuildVersionUpdateSignature();
            return signature;
        }
    }

    private static long BuildExposureStrikeTrendSignature(OptionExposureStrikeTrend trend)
    {
        if (trend == null)
            return 0L;

        unchecked
        {
            var signature = 19L;
            signature = (signature * 31L) + (int)trend.Metric;
            signature = (signature * 31L) + trend.SnapshotCount;
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(trend.FirstTopStrike, 100.0);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(trend.LatestTopStrike, 100.0);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(trend.FirstWeightedStrike, 100.0);
            signature = (signature * 31L) + ExposureHistogramRenderSignature.Quantize(trend.LatestWeightedStrike, 100.0);
            signature = (signature * 31L) + (trend.HasRotation ? 1L : 0L);
            return signature;
        }
    }

    private static long BuildExposureCardStringSignature(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0L;

        unchecked
        {
            var signature = 17L;
            for (var index = 0; index < text.Length; index++)
                signature = (signature * 31L) + text[index];
            return signature;
        }
    }

    private static long BuildExposureCardBrushSignature(Brush sourceBrush, Color fallbackColor)
    {
        var solidBrush = sourceBrush as SolidColorBrush;
        var color = solidBrush != null ? solidBrush.Color : fallbackColor;
        return ((long)color.A << 24) | ((long)color.R << 16) | ((long)color.G << 8) | color.B;
    }

    private void ResetExposureCardOverlaySignature()
    {
        _lastExposureCardOverlaySignature = 0L;
        _hasLastExposureCardOverlaySignature = false;
    }

    private void DetachExposureCardOverlayEvents(ExposureCardWpfView overlay)
    {
        if (overlay == null)
            return;

        overlay.MetricSelected -= OnExposureCardWpfMetricSelected;
        overlay.ProxySelected -= OnExposureCardWpfProxySelected;
        overlay.DataScopeToggled -= OnExposureCardWpfDataScopeToggled;
        overlay.CollapseToggled -= OnExposureCardWpfCollapseToggled;
        overlay.SizeChanged -= OnExposureCardWpfSizeChanged;
    }

    private bool EnsureExposureCardOverlay()
    {
        if (_isTerminated)
            return false;
        if (_exposureCardOverlay != null)
            return true;

        var chartControl = ChartControl;
        if (chartControl == null || chartControl.Dispatcher == null)
            return false;

        _chartDispatcher = chartControl.Dispatcher;
        if (_chartDispatcher.HasShutdownStarted || _chartDispatcher.HasShutdownFinished)
            return false;
        if (!_chartDispatcher.CheckAccess())
        {
            BeginExposureCardOverlayAction(delegate { EnsureExposureCardOverlay(); });
            return false;
        }

        _exposureCardOverlay = new ExposureCardWpfView();
        _exposureCardOverlay.Width = ExposureCardWidth;
        _exposureCardOverlay.HorizontalAlignment = HorizontalAlignment.Left;
        _exposureCardOverlay.VerticalAlignment = VerticalAlignment.Top;
        _exposureCardOverlay.MetricSelected += OnExposureCardWpfMetricSelected;
        _exposureCardOverlay.ProxySelected += OnExposureCardWpfProxySelected;
        _exposureCardOverlay.DataScopeToggled += OnExposureCardWpfDataScopeToggled;
        _exposureCardOverlay.CollapseToggled += OnExposureCardWpfCollapseToggled;
        _exposureCardOverlay.SizeChanged += OnExposureCardWpfSizeChanged;
        System.Windows.Controls.Panel.SetZIndex(_exposureCardOverlay, int.MaxValue);

        try
        {
            if (!UserControlCollection.Contains(_exposureCardOverlay))
                UserControlCollection.Add(_exposureCardOverlay);
        }
        catch (InvalidOperationException)
        {
            DetachExposureCardOverlayEvents(_exposureCardOverlay);
            _exposureCardOverlay = null;
            ResetExposureCardOverlaySignature();
            return false;
        }

        ResetExposureCardOverlaySignature();
        return true;
    }

    private void RemoveExposureCardOverlay()
    {
        var overlay = _exposureCardOverlay;
        if (overlay == null)
        {
            ResetExposureCardOverlaySignature();
            return;
        }

        var dispatcher = overlay.Dispatcher ?? GetExposureCardOverlayDispatcher();
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                BeginExposureCardOverlayAction(RemoveExposureCardOverlay);
            else
            {
                DetachExposureCardOverlayEvents(overlay);
                _exposureCardOverlay = null;
                ResetExposureCardOverlaySignature();
            }
            return;
        }

        DetachExposureCardOverlayEvents(overlay);

        try
        {
            if (UserControlCollection.Contains(overlay))
                UserControlCollection.Remove(overlay);
        }
        catch (InvalidOperationException)
        {
        }

        _exposureCardOverlay = null;
        ResetExposureCardOverlaySignature();
    }

    private void PositionExposureCardOverlay()
    {
        if (_exposureCardOverlay == null || ChartPanel == null)
            return;

        var isCollapsed = _exposureCardOverlay.IsCollapsed;
        var width = isCollapsed ? ExposureCardCollapsedSize : ExposureCardWidth;
        var maxHeight = isCollapsed ? ExposureCardCollapsedSize : GetExposureCardOverlayMaxHeight();
        if (Math.Abs(_exposureCardOverlay.MaxHeight - maxHeight) > 0.5)
            _exposureCardOverlay.MaxHeight = maxHeight;

        double height;
        if (isCollapsed)
        {
            height = ExposureCardCollapsedSize;
            _exposureCardOverlay.Height = height;
        }
        else
        {
            _exposureCardOverlay.Height = double.NaN;
            height = _exposureCardOverlay.ActualHeight;
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 1.0)
                height = _exposureCardOverlay.DesiredSize.Height > 1.0 ? _exposureCardOverlay.DesiredSize.Height : ExposureCardHeight;
            height = Math.Min(height, maxHeight);
        }

        var cardRect = GetExposureCardRectangle(width, (float)height);
        var top = ClampExposureCardTop(cardRect.Top, height);
        _exposureCardOverlay.Width = width;
        _exposureCardOverlay.Margin = new Thickness(cardRect.Left, top, 0.0, 0.0);
    }

    private double GetExposureCardOverlayMaxHeight()
    {
        if (ChartPanel == null)
            return ExposureCardHeight;

        const double panelMargin = 12.0;
        return Math.Max(96.0, ChartPanel.H - (panelMargin * 2.0));
    }

    private double ClampExposureCardTop(float requestedTop, double height)
    {
        if (ChartPanel == null)
            return requestedTop;

        const double panelMargin = 4.0;
        var minTop = ChartPanel.Y + panelMargin;
        var maxTop = ChartPanel.Y + ChartPanel.H - height - panelMargin;
        if (maxTop < minTop)
            return minTop;

        return Math.Max(minTop, Math.Min(requestedTop, maxTop));
    }

    private void OnExposureCardWpfSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        PositionExposureCardOverlay();
    }

    private void OnExposureCardWpfCollapseToggled(object sender, EventArgs eventArgs)
    {
        PositionExposureCardOverlay();
    }

    private void OnExposureCardWpfMetricSelected(object sender, ExposureMetricSelectedEventArgs eventArgs)
    {
        SelectExposureMetricFromCard(eventArgs.Metric);
    }

    private void OnExposureCardWpfProxySelected(object sender, ExposureProxySelectedEventArgs eventArgs)
    {
        SelectExposureProxyFromCard(eventArgs.ProxySymbol);
    }

    private void OnExposureCardWpfDataScopeToggled(object sender, EventArgs eventArgs)
    {
        ToggleExposureDataScopeFromCard();
    }
}
