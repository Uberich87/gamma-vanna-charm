#region Using declarations
using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    private void UpdateOptionLevelDrawings()
    {
        if (_optionStrikeExposures == null || _optionStrikeExposures.Count == 0)
        {
            ClearOptionLevelDrawings();
            return;
        }
        if (!ShouldRebuildOptionLevelDrawings())
            return;

        if (_underlyingPrice <= 0.0)
        {
            _optionDataStatus = "Options data loaded; waiting for underlying price.";
            return;
        }

        if (_futuresAnchorPrice <= 0.0)
        {
            _optionDataStatus = "Options data loaded; waiting for chart price to map exposure levels.";
            return;
        }

        var lastCheckedUtc = GetOptionsLastCheckedUtc();
        var lastUpdatedUtc = GetOptionsLastUpdatedUtc();
        DrawOptionExposureHistogram(_underlyingPrice, 0.0, _futuresAnchorPrice, lastCheckedUtc, lastUpdatedUtc);

        MarkOptionLevelDrawingsCurrent(lastCheckedUtc, lastUpdatedUtc);
    }

    private bool ShouldRebuildOptionLevelDrawings()
    {
        return _lastOptionsTimestampUtc != _lastExposureTimestampUtc
            || _lastRenderedUseZeroDteOptions != UseZeroDteOptions
            || _lastRenderedActivePositionModel != ActivePositionModel
            || _lastRenderedExposureMetric != ExposureMetric
            || _lastRenderedHeatmapMaxOpacity != HeatmapMaxOpacity
            || _lastRenderedHeatmapMaxWidthPercent != HeatmapMaxWidthPercent
            || Math.Abs(_lastRenderedHeatmapBandTicks - HeatmapBandTicks) > 0.0000001
            || _lastRenderedOptionsLastCheckedUtc != GetOptionsLastCheckedUtc()
            || _lastRenderedOptionsLastUpdatedUtc != GetOptionsLastUpdatedUtc();
    }

    private void MarkOptionLevelDrawingsCurrent(DateTime lastCheckedUtc, DateTime lastUpdatedUtc)
    {
        _lastOptionsTimestampUtc = _lastExposureTimestampUtc;
        _lastRenderedUseZeroDteOptions = UseZeroDteOptions;
        _lastRenderedActivePositionModel = ActivePositionModel;
        _lastRenderedExposureMetric = ExposureMetric;
        _lastRenderedHeatmapMaxOpacity = HeatmapMaxOpacity;
        _lastRenderedHeatmapMaxWidthPercent = HeatmapMaxWidthPercent;
        _lastRenderedHeatmapBandTicks = HeatmapBandTicks;
        _lastRenderedOptionsLastCheckedUtc = lastCheckedUtc;
        _lastRenderedOptionsLastUpdatedUtc = lastUpdatedUtc;
    }

    private void DrawOptionExposureHistogram(double currentProxyPrice, double currentIndexPrice, double currentFuturesPrice, DateTime lastCheckedUtc, DateTime lastUpdatedUtc)
    {
        if (_optionStrikeExposures == null || _optionLevelTags == null)
            return;

        string status;
        var renderLevels = ExposureHistogramBuilder.Build(
            _optionStrikeExposures,
            _exposureFuturesFamily,
            _resolvedExposureProxySymbol,
            SelectedExposureMetric,
            currentProxyPrice,
            currentIndexPrice,
            currentFuturesPrice,
            _proxyConversionMultipliers,
            TickSize,
            HeatmapBandTicks,
            HeatmapMaxOpacity,
            out status);
        _optionDataStatus = status;

        var persistenceStatus = GetExposurePersistenceStatus();
        if (!string.IsNullOrWhiteSpace(persistenceStatus))
            _optionDataStatus = persistenceStatus;

        if (renderLevels.Count == 0)
        {
            ClearHistogramRenderLevels();
            RemoveDrawObject(ExposureProfileLegendTag);
            RemoveInactiveOptionLevelDrawings(new HashSet<string>());
            return;
        }

        var renderSignature = BuildHistogramRenderSignature(renderLevels, ExposureMetric, lastCheckedUtc, lastUpdatedUtc);
        if (!_hasLastHistogramRenderSignature || _lastHistogramRenderSignature != renderSignature)
        {
            _lastHistogramRenderSignature = renderSignature;
            _hasLastHistogramRenderSignature = true;
            SetHistogramRenderLevels(renderLevels);
        }

        RemoveDrawObject(ExposureProfileLegendTag);
        RemoveInactiveOptionLevelDrawings(new HashSet<string>());
    }

    private void DrawOptionStatusText()
    {
        RemoveDrawObject(ExposureStatusTag);

        var statusText = BuildOptionStatusText();
        var lastCheckedUtc = GetOptionsLastCheckedUtc();
        var lastUpdatedUtc = GetOptionsLastUpdatedUtc();
        if (string.Equals(_lastOptionStatusText, statusText, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(statusText)
                || (_lastOptionStatusLastCheckedUtc == lastCheckedUtc
                    && _lastOptionStatusLastUpdatedUtc == lastUpdatedUtc)))
        {
            return;
        }

        _lastOptionStatusText = statusText;
        _lastOptionStatusLastCheckedUtc = lastCheckedUtc;
        _lastOptionStatusLastUpdatedUtc = lastUpdatedUtc;
        RequestChartRenderRefresh();
    }

    private string BuildOptionStatusText()
    {
        var persistenceStatus = GetExposurePersistenceStatus();
        var optionDataStatus = string.IsNullOrWhiteSpace(persistenceStatus) ? _optionDataStatus : persistenceStatus;
        return OptionsStatusTextBuilder.Build(
            _spotPriceStatus,
            _optionExposureDataSource != null ? _optionExposureDataSource.LastError : null,
            UseZeroDteOptions,
            optionDataStatus,
            GetHistogramRenderLevelsSnapshot().Count > 0);
    }

    private static long BuildHistogramRenderSignature(List<HistogramRenderLevel> renderLevels, OptionExposureMetric exposureMetric, DateTime lastCheckedUtc, DateTime lastUpdatedUtc)
    {
        unchecked
        {
            var signature = ExposureHistogramRenderSignature.Build(renderLevels);
            signature = (signature * 31L) + (int)exposureMetric;
            signature = (signature * 31L) + lastCheckedUtc.Ticks;
            signature = (signature * 31L) + lastUpdatedUtc.Ticks;
            return signature;
        }
    }

    private void SetHistogramRenderLevels(List<HistogramRenderLevel> renderLevels)
    {
        lock (_renderSyncRoot)
            _histogramRenderLevels = CreateHistogramRenderSnapshot(renderLevels);

        RequestChartRenderRefresh();
    }

    private HistogramRenderLevel[] CreateHistogramRenderSnapshot(List<HistogramRenderLevel> renderLevels)
    {
        if (renderLevels == null || renderLevels.Count == 0)
            return EmptyHistogramRenderLevels;

        var snapshot = renderLevels.ToArray();
        Array.Sort(snapshot, CompareHistogramRenderLevelsByPrice);
        return snapshot;
    }

    private int CompareHistogramRenderLevelsByPrice(HistogramRenderLevel left, HistogramRenderLevel right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return -1;
        if (right == null)
            return 1;

        return GetHistogramRowLowerPrice(left).CompareTo(GetHistogramRowLowerPrice(right));
    }

    private void ClearHistogramRenderLevels()
    {
        lock (_renderSyncRoot)
            _histogramRenderLevels = EmptyHistogramRenderLevels;

        _lastHistogramRenderSignature = 0L;
        _hasLastHistogramRenderSignature = false;
        RequestChartRenderRefresh();
    }

    private void RequestChartRenderRefresh()
    {
        var chartControl = ChartControl;
        if (chartControl == null || chartControl.Dispatcher == null)
            return;

        if (chartControl.Dispatcher.CheckAccess())
            chartControl.InvalidateVisual();
        else
            chartControl.Dispatcher.BeginInvoke(new Action(chartControl.InvalidateVisual));
    }

    private IList<HistogramRenderLevel> GetHistogramRenderLevelsSnapshot()
    {
        lock (_renderSyncRoot)
            return _histogramRenderLevels;
    }

    private void RemoveInactiveOptionLevelDrawings(HashSet<string> activeTags)
    {
        if (_optionLevelTags == null)
            return;

        foreach (var tag in _optionLevelTags)
        {
            if (!activeTags.Contains(tag))
                RemoveDrawObject(tag);
        }

        _optionLevelTags = activeTags;
    }

    private void ClearOptionLevelDrawings()
    {
        if (_optionLevelTags == null)
            return;

        foreach (var tag in _optionLevelTags)
            RemoveDrawObject(tag);
        _optionLevelTags.Clear();
        RemoveDrawObject(ExposureProfileLegendTag);
        RemoveDrawObject(ExposureStatusTag);
        ClearHistogramRenderLevels();
    }

    private void DisposeOptionsPoller()
    {
        if (_optionExposureDataSource != null)
        {
            _optionExposureDataSource.PollCompleted -= OnOptionsPollCompleted;
            _optionExposureDataSource.Stop();
        }

        DisposeBackgroundOptionsPollers();
    }

    private void DisposeBackgroundOptionsPollers()
    {
        if (_backgroundOptionExposureDataSources == null)
            return;

        for (var index = 0; index < _backgroundOptionExposureDataSources.Count; index++)
        {
            var dataSource = _backgroundOptionExposureDataSources[index];
            if (dataSource == null)
                continue;

            dataSource.PollCompleted -= OnBackgroundOptionsPollCompleted;
            dataSource.Stop();
            dataSource.Dispose();
        }

        _backgroundOptionExposureDataSources.Clear();
    }
}
