#region Using declarations
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.MarketData.Cboe;
using GammaVannaCharm.Core.Options;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    private void ConfigureSpotPriceProvider()
    {
        ClearOptionLevelDrawings();
        _chartDispatcher = ChartControl != null ? ChartControl.Dispatcher : null;
        Interlocked.Exchange(ref _optionsProcessingQueued, 0);
        _spotPriceStatus = null;
        _optionDataStatus = null;
        _lastOptionsTimestampUtc = DateTime.MinValue;
        _lastExposureTimestampUtc = DateTime.MinValue;
        _lastRenderedUseZeroDteOptions = UseZeroDteOptions;
        _lastObservedUseZeroDteOptions = UseZeroDteOptions;
        _lastResolvedActivePositionModel = ActivePositionModel;
        _lastRenderedOptionsLastCheckedUtc = DateTime.MinValue;
        _lastRenderedOptionsLastUpdatedUtc = DateTime.MinValue;
        _lastOptionStatusLastCheckedUtc = DateTime.MinValue;
        _lastOptionStatusLastUpdatedUtc = DateTime.MinValue;
        ResetExposurePersistenceState();
        _optionExposureSummary = null;
        ResetWeightedImpliedVolatilityRead();
        _underlyingPrice = 0.0;
        _anchorUnderlyingPrice = 0.0;
        _futuresAnchorPrice = 0.0;
        _nextPriceDrivenRefreshUtc = DateTime.MinValue;
        if (_optionStrikeExposures != null)
            _optionStrikeExposures.Clear();
        _resolvedExposureProxySymbol = SpotProxySymbol.Auto;
        _exposureFuturesFamily = FuturesFamily.Unknown;

        _exposureFuturesFamily = FuturesFamilyResolver.Resolve(GetInstrumentName());

        SpotProxySymbol resolvedSymbol;
        if (!SpotProxySymbolResolver.TryResolve(_exposureFuturesFamily, ExposureProxySymbol, out resolvedSymbol))
        {
            DisposeOptionsPoller();
            _spotPriceStatus = "Selected proxy is not valid for the chart futures instrument.";
            return;
        }

        _resolvedExposureProxySymbol = resolvedSymbol;

        ConfigureOptionsPoller(resolvedSymbol);
        ConfigureBackgroundOptionsPollers(resolvedSymbol);
    }

    private void RefreshRuntimeSettingsIfChanged()
    {
        if (_lastObservedUseZeroDteOptions == UseZeroDteOptions)
            return;

        _lastObservedUseZeroDteOptions = UseZeroDteOptions;
        _lastExposureTimestampUtc = DateTime.MinValue;
        _lastOptionsTimestampUtc = DateTime.MinValue;
        _optionExposureSummary = null;
        ResetWeightedImpliedVolatilityRead();
        if (_optionStrikeExposures != null)
            _optionStrikeExposures.Clear();
        ClearHistogramRenderLevels();
        RemoveDrawObject(ExposureProfileLegendTag);
        RemoveDrawObject(ExposureStatusTag);
        _optionDataStatus = "Options filter changed; waiting for the latest options response.";
        if (_resolvedExposureProxySymbol != SpotProxySymbol.Auto)
        {
            ConfigureOptionsPoller(_resolvedExposureProxySymbol);
            ConfigureBackgroundOptionsPollers(_resolvedExposureProxySymbol);
        }
    }

    private void ConfigureOptionsPoller(SpotProxySymbol resolvedSymbol)
    {
        var optionsSymbol = GetOptionsSymbol(resolvedSymbol);
        if (string.IsNullOrWhiteSpace(optionsSymbol))
        {
            DisposeOptionsPoller();
            _spotPriceStatus = "Options symbol is required.";
            return;
        }

        var promotedDataSource = TakeBackgroundOptionsDataSource(optionsSymbol, UseZeroDteOptions);
        DisposeOptionsPoller();
        if (promotedDataSource != null)
        {
            _optionExposureDataSource = promotedDataSource;
            _optionExposureDataSource.PollCompleted += OnOptionsPollCompleted;
            QueueExposureDatabaseInitialization(optionsSymbol, ActivePositionModel, UseZeroDteOptions);
            return;
        }

        if (_optionExposureDataSource == null)
            _optionExposureDataSource = new OptionExposureDataSource();

        var optionChainProvider = CreateOptionChainProvider();
        _optionExposureDataSource.PollCompleted += OnOptionsPollCompleted;
        _optionExposureDataSource.Start(
            optionsSymbol,
            TimeSpan.FromSeconds(Math.Max(15, OptionsPollSeconds)),
            optionChainProvider,
            UseZeroDteOptions);
        QueueExposureDatabaseInitialization(optionsSymbol, ActivePositionModel, UseZeroDteOptions);
    }

    private OptionExposureDataSource TakeBackgroundOptionsDataSource(string symbol, bool useZeroDteOptions)
    {
        if (_backgroundOptionExposureDataSources == null || string.IsNullOrWhiteSpace(symbol))
            return null;

        for (var index = 0; index < _backgroundOptionExposureDataSources.Count; index++)
        {
            var dataSource = _backgroundOptionExposureDataSources[index];
            if (dataSource == null
                || !string.Equals(dataSource.Symbol, symbol, StringComparison.Ordinal)
                || dataSource.UseZeroDteOptions != useZeroDteOptions)
            {
                continue;
            }

            _backgroundOptionExposureDataSources.RemoveAt(index);
            dataSource.PollCompleted -= OnBackgroundOptionsPollCompleted;
            return dataSource;
        }

        return null;
    }

    protected virtual IOptionChainProvider CreateOptionChainProvider()
    {
        return new CboeOptionChainProvider(new CboeOptionsClient(OptionsEndpointBase), UseZeroDteOptions);
    }

    private void ConfigureBackgroundOptionsPollers(SpotProxySymbol activeSymbol)
    {
        DisposeBackgroundOptionsPollers();
        if (_exposureFuturesFamily == FuturesFamily.SP500)
        {
            StartBackgroundOptionsPollersForProxy(SpotProxySymbol.SPX, activeSymbol);
            StartBackgroundOptionsPollersForProxy(SpotProxySymbol.SPY, activeSymbol);
            return;
        }

        StartBackgroundOptionsPollersForProxy(activeSymbol, activeSymbol);
    }

    private void StartBackgroundOptionsPollersForProxy(SpotProxySymbol proxySymbol, SpotProxySymbol activeSymbol)
    {
        StartBackgroundOptionsPoller(proxySymbol, activeSymbol, true);
        StartBackgroundOptionsPoller(proxySymbol, activeSymbol, false);
    }

    private void StartBackgroundOptionsPoller(SpotProxySymbol proxySymbol, SpotProxySymbol activeSymbol, bool useZeroDteOptions)
    {
        if (proxySymbol == SpotProxySymbol.Auto || (proxySymbol == activeSymbol && useZeroDteOptions == UseZeroDteOptions))
            return;

        var optionsSymbol = GetOptionsSymbol(proxySymbol);
        if (string.IsNullOrWhiteSpace(optionsSymbol))
            return;

        var dataSource = new OptionExposureDataSource();
        try
        {
            dataSource.PollCompleted += OnBackgroundOptionsPollCompleted;
            dataSource.Start(
                optionsSymbol,
                TimeSpan.FromSeconds(Math.Max(15, OptionsPollSeconds)),
                new CboeOptionChainProvider(new CboeOptionsClient(OptionsEndpointBase), useZeroDteOptions),
                useZeroDteOptions);
            _backgroundOptionExposureDataSources.Add(dataSource);
            QueueExposureDatabaseInitialization(optionsSymbol, ActivePositionModel, useZeroDteOptions);
        }
        catch
        {
            dataSource.PollCompleted -= OnBackgroundOptionsPollCompleted;
            dataSource.Dispose();
            throw;
        }
    }

    private SpotProxyConversionMultipliers CreateProxyConversionMultipliers()
    {
        return new SpotProxyConversionMultipliers
        {
            SpyToEs = SpyToEsMultiplier,
            SpxToEs = SpxToEsMultiplier,
            QqqToNq = QqqToNqMultiplier
        };
    }

    protected virtual string GetOptionsSymbol(SpotProxySymbol symbol)
    {
        return CboeOptionsSymbolResolver.GetOptionsSymbol(symbol, SpyOptionsSymbol, SpxOptionsSymbol, QqqOptionsSymbol);
    }

    private string GetInstrumentName()
    {
        if (Instrument == null || Instrument.MasterInstrument == null)
            return null;

        return Instrument.MasterInstrument.Name;
    }

    private bool TryResolveOptionExposureSummary(out OptionExposureSummary summary)
    {
        Debug.Assert(Monitor.IsEntered(_optionDataProcessingSyncRoot), "Option exposure state must be resolved while holding the option data processing lock.");
        summary = null;

        if (_optionExposureDataSource == null)
            return false;
        if (!_optionExposureDataSource.IsRunning)
        {
            _optionDataStatus = null;
            return false;
        }

        OptionExposureCalculation calculation;
        string status;
        if (!_optionExposureDataSource.TryGetCalculation(DateTime.UtcNow, out calculation, out status))
        {
            _optionDataStatus = status;
            return false;
        }

        if (_lastExposureTimestampUtc != _optionExposureDataSource.LastUpdatedUtc || _lastResolvedActivePositionModel != ActivePositionModel)
        {
            var positionModelChanged = _lastResolvedActivePositionModel != ActivePositionModel;
            _underlyingPrice = calculation.UnderlyingPrice;
            UpdateExposureMappingAnchor(calculation.UnderlyingPrice);
            _optionExposureSummary = calculation.GetSummary(ActivePositionModel);
            _optionStrikeExposures = calculation.GetStrikeExposures(ActivePositionModel);
            UpdateWeightedImpliedVolatilityRead(_optionExposureSummary, positionModelChanged);
            _lastExposureTimestampUtc = _optionExposureDataSource.LastUpdatedUtc;
            _lastResolvedActivePositionModel = ActivePositionModel;
            QueueExposureCalculationPersistence(calculation);
            _optionDataStatus = BuildResolvedOptionDataStatus(calculation);
        }

        var currentPersistenceStatus = GetExposurePersistenceStatus();
        if (!string.IsNullOrWhiteSpace(currentPersistenceStatus))
            _optionDataStatus = currentPersistenceStatus;
        else if (IsExposurePersistenceStatus(_optionDataStatus))
            _optionDataStatus = BuildResolvedOptionDataStatus(calculation);

        summary = _optionExposureSummary;
        return summary != null && summary.ContractCount > 0;
    }

    private string BuildResolvedOptionDataStatus(OptionExposureCalculation calculation)
    {
        var persistenceStatus = GetExposurePersistenceStatus();
        if (!string.IsNullOrWhiteSpace(persistenceStatus))
            return persistenceStatus;

        var selectedSummary = calculation != null ? calculation.GetSummary(ActivePositionModel) : null;
        if (selectedSummary == null || selectedSummary.ContractCount <= 0)
        {
            return UseZeroDteOptions
                ? "0DTE options have expired or no active 0DTE contracts remain; switch to ALL for later expirations."
                : "Options data loaded, but no active contracts remain for the selected filters.";
        }

        return ActivePositionModel == OptionPositionModel.FlowImpliedDealer && calculation != null && !calculation.HasFlowImpliedDealerExposure
            ? "FLOW model selected; waiting for seeded or classifiable option flow."
            : "Options data loaded; building exposure histogram.";
    }

    private void UpdateWeightedImpliedVolatilityRead(OptionExposureSummary summary, bool resetPrevious)
    {
        var current = summary != null ? summary.WeightedImpliedVolatility : 0.0;
        if (double.IsNaN(current) || double.IsInfinity(current) || current <= 0.0)
        {
            ResetWeightedImpliedVolatilityRead();
            return;
        }

        if (!_hasLatestWeightedImpliedVolatility || resetPrevious)
        {
            _latestWeightedImpliedVolatility = current;
            _previousWeightedImpliedVolatility = 0.0;
            _hasLatestWeightedImpliedVolatility = true;
            _hasPreviousWeightedImpliedVolatility = false;
            return;
        }

        _previousWeightedImpliedVolatility = _latestWeightedImpliedVolatility;
        _hasPreviousWeightedImpliedVolatility = true;
        _latestWeightedImpliedVolatility = current;
    }

    private void ResetWeightedImpliedVolatilityRead()
    {
        _latestWeightedImpliedVolatility = 0.0;
        _previousWeightedImpliedVolatility = 0.0;
        _hasLatestWeightedImpliedVolatility = false;
        _hasPreviousWeightedImpliedVolatility = false;
    }

    private static bool IsExposurePersistenceStatus(string status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && (status.StartsWith("Could not initialize exposure history:", StringComparison.Ordinal)
                || status.StartsWith("Could not persist exposure history:", StringComparison.Ordinal)
                || status.StartsWith("Could not queue exposure history persistence:", StringComparison.Ordinal));
    }

    private void QueueExposureDatabaseInitialization(string symbol, OptionPositionModel positionModel, bool useZeroDteOptions)
    {
        QueueExposurePersistenceRequest(new ExposurePersistenceRequest(ImpliedDataDirectory, symbol, positionModel, useZeroDteOptions));
    }

    private void QueueExposureCalculationPersistence(OptionExposureCalculation calculation)
    {
        QueueExposureCalculationPersistence(_optionExposureDataSource, calculation);
    }

    private void QueueExposureCalculationPersistence(OptionExposureDataSource dataSource, OptionExposureCalculation calculation)
    {
        if (calculation == null || dataSource == null)
            return;

        var timestampUtc = GetEffectiveExposureTimestampUtc(dataSource, calculation);
        if (timestampUtc == DateTime.MinValue)
            return;

        QueueExposurePersistenceRequest(new ExposurePersistenceRequest(
            ImpliedDataDirectory,
            calculation,
            timestampUtc,
            dataSource.LastImpliedDealerSnapshot,
            dataSource.LastFlowIntervals,
            ActivePositionModel,
            dataSource.UseZeroDteOptions));
    }

    private void PersistBackgroundOptionsData(OptionExposureDataSource dataSource)
    {
        if (dataSource == null || _isTerminated)
            return;

        OptionExposureCalculation calculation;
        string status;
        if (!dataSource.TryGetCalculation(DateTime.UtcNow, out calculation, out status))
            return;

        QueueExposureCalculationPersistence(dataSource, calculation);
    }

    private void QueueExposurePersistenceRequest(ExposurePersistenceRequest request)
    {
        if (request == null)
            return;

        CancellationToken cancellationToken;
        lock (_exposurePersistenceSyncRoot)
        {
            if (_isTerminated || IsExposurePersistenceDuplicate(request))
                return;

            _pendingExposurePersistenceQueue.Enqueue(request);
            if (_exposurePersistenceWorkerRunning)
                return;

            if (_exposurePersistenceCancellationSource == null || _exposurePersistenceCancellationSource.IsCancellationRequested)
            {
                _exposurePersistenceCancellationSource?.Dispose();
                _exposurePersistenceCancellationSource = new CancellationTokenSource();
            }
            cancellationToken = _exposurePersistenceCancellationSource.Token;
            _exposurePersistenceWorkerRunning = true;
        }

        try
        {
            var workerTask = Task.Run(delegate { RunExposurePersistenceWorker(cancellationToken); });
            lock (_exposurePersistenceSyncRoot)
                _exposurePersistenceTask = workerTask;
            workerTask.ContinueWith(ObserveExposurePersistenceWorkerCompletion, TaskScheduler.Default);
        }
        catch (Exception exception)
        {
            lock (_exposurePersistenceSyncRoot)
            {
                _exposurePersistenceWorkerRunning = false;
                _exposurePersistenceTask = null;
                _exposurePersistenceStatus = "Could not queue exposure history persistence: " + FormatExposurePersistenceException(exception);
            }
        }
    }

    private void RunExposurePersistenceWorker(CancellationToken cancellationToken)
    {
        while (true)
        {
            ExposurePersistenceRequest request;
            lock (_exposurePersistenceSyncRoot)
            {
                if (cancellationToken.IsCancellationRequested || _pendingExposurePersistenceQueue.Count == 0)
                {
                    _exposurePersistenceWorkerRunning = false;
                    return;
                }

                request = _pendingExposurePersistenceQueue.Dequeue();
            }

            if (IsExposurePersistenceDuplicateLocked(request))
                continue;

            try
            {
                if (request.IsInitialization)
                {
                    OptionExposureHistorySqliteStore.EnsureDatabase(request.Directory);
                    HydrateImpliedDealerState(request);
                    RefreshRecentExposureHistory(request);
                }
                else
                {
                    OptionExposureHistorySqliteStore.Save(
                        request.Directory,
                        request.Calculation,
                        request.EffectiveTimestampUtc,
                        request.ImpliedDealerSnapshot,
                        request.FlowIntervals,
                        request.ActivePositionModel,
                        request.UseZeroDteOptions);
                    RefreshRecentExposureHistory(request);
                }
                MarkExposurePersistenceSucceeded(request);
            }
            catch (Exception exception)
            {
                MarkExposurePersistenceFailed(request, exception);
            }

            QueueOptionsProcessing();
        }
    }

    private void HydrateImpliedDealerState(ExposurePersistenceRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol) || request.ActivePositionModel != OptionPositionModel.FlowImpliedDealer)
            return;

        var activeSymbol = GetActiveOptionsSymbol();
        if (string.IsNullOrWhiteSpace(activeSymbol)
            || !string.Equals(request.Symbol, activeSymbol, StringComparison.Ordinal)
            || request.UseZeroDteOptions != UseZeroDteOptions)
        {
            return;
        }

        OptionImpliedDealerSnapshot snapshot;
        if (!OptionExposureHistorySqliteStore.TryLoadLatestImpliedDealerSnapshot(
            request.Directory,
            request.Symbol,
            request.ActivePositionModel,
            request.UseZeroDteOptions,
            DateTime.UtcNow,
            out snapshot))
        {
            return;
        }

        var dataSource = _optionExposureDataSource;
        if (dataSource != null)
            dataSource.HydrateImpliedDealerSnapshot(snapshot);
    }

    private void RefreshRecentExposureHistory(ExposurePersistenceRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
            return;

        var activeSymbol = GetActiveOptionsSymbol();
        if (string.IsNullOrWhiteSpace(activeSymbol)
            || !string.Equals(request.Symbol, activeSymbol, StringComparison.Ordinal)
            || request.UseZeroDteOptions != UseZeroDteOptions)
        {
            return;
        }

        var trendSinceUtc = DateTime.UtcNow.AddHours(-OptionExposureHistoryDefaults.TrendWindowHours);
        var intensitySinceUtc = trendSinceUtc;
        var points = OptionExposureHistorySqliteStore.LoadRecentSnapshots(
            request.Directory,
            request.Symbol,
            request.ActivePositionModel,
            request.UseZeroDteOptions,
            intensitySinceUtc,
            OptionExposureHistoryDefaults.RecentSnapshotLimit);
        if (!HasTrendWindow(points))
        {
            points = OptionExposureHistorySqliteStore.LoadRecentSnapshots(
                request.Directory,
                request.Symbol,
                request.ActivePositionModel,
                intensitySinceUtc,
                OptionExposureHistoryDefaults.RecentSnapshotLimit);
        }

        OptionExposureStrikeTrend gammaTrend;
        OptionExposureStrikeTrend vannaTrend;
        OptionExposureStrikeTrend charmTrend;
        if (!OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, request.UseZeroDteOptions, OptionExposureMetric.Gamma, trendSinceUtc, out gammaTrend))
            OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, OptionExposureMetric.Gamma, trendSinceUtc, out gammaTrend);
        if (!OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, request.UseZeroDteOptions, OptionExposureMetric.Vanna, trendSinceUtc, out vannaTrend))
            OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, OptionExposureMetric.Vanna, trendSinceUtc, out vannaTrend);
        if (!OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, request.UseZeroDteOptions, OptionExposureMetric.Charm, trendSinceUtc, out charmTrend))
            OptionExposureHistorySqliteStore.TryLoadStrikeTrend(request.Directory, request.Symbol, request.ActivePositionModel, OptionExposureMetric.Charm, trendSinceUtc, out charmTrend);

        var gammaStrikeSeries = LoadScopedStrikeSeriesWithLegacyFallback(request, OptionExposureMetric.Gamma, trendSinceUtc);
        var vannaStrikeSeries = LoadScopedStrikeSeriesWithLegacyFallback(request, OptionExposureMetric.Vanna, trendSinceUtc);
        var charmStrikeSeries = LoadScopedStrikeSeriesWithLegacyFallback(request, OptionExposureMetric.Charm, trendSinceUtc);

        lock (_exposurePersistenceSyncRoot)
        {
            _recentExposureHistoryPoints = new List<OptionExposureHistoryPoint>(points);
            _recentGammaStrikeHistoryByStrike = BuildStrikeHistoryDictionary(gammaStrikeSeries);
            _recentVannaStrikeHistoryByStrike = BuildStrikeHistoryDictionary(vannaStrikeSeries);
            _recentCharmStrikeHistoryByStrike = BuildStrikeHistoryDictionary(charmStrikeSeries);
            _gammaStrikeTrend = gammaTrend;
            _vannaStrikeTrend = vannaTrend;
            _charmStrikeTrend = charmTrend;
        }
    }

    private static bool HasTrendWindow(IList<OptionExposureHistoryPoint> points)
    {
        return points != null && points.Count >= 2;
    }

    private IList<OptionExposureStrikeHistorySeries> LoadScopedStrikeSeriesWithLegacyFallback(
        ExposurePersistenceRequest request,
        OptionExposureMetric metric,
        DateTime sinceUtc)
    {
        var series = OptionExposureHistorySqliteStore.LoadRecentStrikeSeries(
            request.Directory,
            request.Symbol,
            request.ActivePositionModel,
            request.UseZeroDteOptions,
            metric,
            sinceUtc,
            OptionExposureHistoryDefaults.RecentSnapshotLimit);
        return HasTrendSeries(series)
            ? series
            : OptionExposureHistorySqliteStore.LoadRecentStrikeSeries(request.Directory, request.Symbol, request.ActivePositionModel, metric, sinceUtc, OptionExposureHistoryDefaults.RecentSnapshotLimit);
    }

    private static bool HasTrendSeries(IList<OptionExposureStrikeHistorySeries> series)
    {
        if (series == null)
            return false;

        for (var index = 0; index < series.Count; index++)
        {
            var item = series[index];
            if (item != null && item.Values != null && item.Values.Count >= 2)
                return true;
        }

        return false;
    }

    private List<OptionExposureHistoryPoint> GetRecentExposureHistoryPointsSnapshot()
    {
        lock (_exposurePersistenceSyncRoot)
            return new List<OptionExposureHistoryPoint>(_recentExposureHistoryPoints);
    }

    private OptionExposureStrikeTrend GetExposureStrikeTrendSnapshot(OptionExposureMetric metric)
    {
        lock (_exposurePersistenceSyncRoot)
        {
            if (metric == OptionExposureMetric.Vanna)
                return _vannaStrikeTrend;
            if (metric == OptionExposureMetric.Charm)
                return _charmStrikeTrend;

            return _gammaStrikeTrend;
        }
    }

    private List<double> GetStrikeExposureHistorySnapshot(OptionExposureMetric metric, double strike)
    {
        lock (_exposurePersistenceSyncRoot)
        {
            Dictionary<double, List<double>> source;
            if (metric == OptionExposureMetric.Vanna)
                source = _recentVannaStrikeHistoryByStrike;
            else if (metric == OptionExposureMetric.Charm)
                source = _recentCharmStrikeHistoryByStrike;
            else
                source = _recentGammaStrikeHistoryByStrike;

            if (source == null)
                return new List<double>();

            List<double> values;
            if (source.TryGetValue(strike, out values))
                return new List<double>(values);

            return new List<double>();
        }
    }

    private static Dictionary<double, List<double>> BuildStrikeHistoryDictionary(IList<OptionExposureStrikeHistorySeries> series)
    {
        var result = new Dictionary<double, List<double>>();
        if (series == null)
            return result;

        for (var index = 0; index < series.Count; index++)
        {
            var item = series[index];
            if (item != null && item.Values != null)
                result[item.Strike] = new List<double>(item.Values);
        }

        return result;
    }

    private void ObserveExposurePersistenceWorkerCompletion(Task task)
    {
        if (task != null && task.IsFaulted)
        {
            var ignored = task.Exception;
        }

        CancellationTokenSource cancellationSource = null;
        lock (_exposurePersistenceSyncRoot)
        {
            if (_exposurePersistenceTask == task)
            {
                _exposurePersistenceTask = null;
                if (_exposurePersistenceCancellationSource != null && _exposurePersistenceCancellationSource.IsCancellationRequested)
                {
                    cancellationSource = _exposurePersistenceCancellationSource;
                    _exposurePersistenceCancellationSource = null;
                }
            }
        }

        if (cancellationSource != null)
            cancellationSource.Dispose();
    }

    private bool IsExposurePersistenceDuplicateLocked(ExposurePersistenceRequest request)
    {
        lock (_exposurePersistenceSyncRoot)
            return IsExposurePersistenceDuplicate(request);
    }

    private bool IsExposurePersistenceDuplicate(ExposurePersistenceRequest request)
    {
        if (request == null)
            return false;
        if (request.IsInitialization)
            return string.Equals(request.Directory, _lastInitializedExposureDatabaseDirectory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Symbol, _lastInitializedExposureDatabaseSymbol, StringComparison.Ordinal)
                && request.ActivePositionModel == _lastInitializedExposureDatabaseModel
                && request.UseZeroDteOptions == _lastInitializedExposureDatabaseUseZeroDteOptions;

        return request.EffectiveTimestampUtc == _lastPersistedExposureTimestampUtc
            && request.ActivePositionModel == _lastPersistedActivePositionModel
            && request.UseZeroDteOptions == _lastPersistedUseZeroDteOptions
            && string.Equals(request.Symbol, _lastPersistedExposureSymbol, StringComparison.Ordinal);
    }

    private void MarkExposurePersistenceSucceeded(ExposurePersistenceRequest request)
    {
        lock (_exposurePersistenceSyncRoot)
        {
            if (request.IsInitialization)
            {
                _lastInitializedExposureDatabaseDirectory = request.Directory;
                _lastInitializedExposureDatabaseSymbol = request.Symbol;
                _lastInitializedExposureDatabaseModel = request.ActivePositionModel;
                _lastInitializedExposureDatabaseUseZeroDteOptions = request.UseZeroDteOptions;
            }
            else
            {
                _lastPersistedExposureTimestampUtc = request.EffectiveTimestampUtc;
                _lastPersistedActivePositionModel = request.ActivePositionModel;
                _lastPersistedUseZeroDteOptions = request.UseZeroDteOptions;
                _lastPersistedExposureSymbol = request.Symbol;
            }
            _exposurePersistenceStatus = null;
        }
    }

    private void MarkExposurePersistenceFailed(ExposurePersistenceRequest request, Exception exception)
    {
        lock (_exposurePersistenceSyncRoot)
        {
            var prefix = request != null && request.IsInitialization
                ? "Could not initialize exposure history: "
                : "Could not persist exposure history: ";
            _exposurePersistenceStatus = prefix + FormatExposurePersistenceException(exception);
        }
    }

    private static string FormatExposurePersistenceException(Exception exception)
    {
        if (exception == null)
            return "unknown error";

        var current = exception;
        while ((current is TargetInvocationException || current is TypeInitializationException) && current.InnerException != null)
            current = current.InnerException;

        return string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
    }

    private string GetExposurePersistenceStatus()
    {
        lock (_exposurePersistenceSyncRoot)
            return _exposurePersistenceStatus;
    }

    private DateTime GetEffectiveExposureTimestampUtc(OptionExposureDataSource dataSource, OptionExposureCalculation calculation)
    {
        if (calculation == null)
            return DateTime.MinValue;

        var timestampUtc = NormalizeExposurePersistenceTimestamp(calculation.SnapshotTimestampUtc);
        if (timestampUtc == DateTime.MinValue && dataSource != null)
            timestampUtc = NormalizeExposurePersistenceTimestamp(dataSource.LastUpdatedUtc);

        return timestampUtc;
    }

    private static DateTime NormalizeExposurePersistenceTimestamp(DateTime timestampUtc)
    {
        if (timestampUtc == DateTime.MinValue)
            return timestampUtc;
        if (timestampUtc.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);
        if (timestampUtc.Kind == DateTimeKind.Local)
            return timestampUtc.ToUniversalTime();

        return timestampUtc;
    }

    private void ResetExposurePersistenceState()
    {
        lock (_exposurePersistenceSyncRoot)
        {
            _lastPersistedExposureTimestampUtc = DateTime.MinValue;
            _lastPersistedActivePositionModel = ActivePositionModel;
            _lastPersistedUseZeroDteOptions = UseZeroDteOptions;
            _lastPersistedExposureSymbol = null;
            _lastInitializedExposureDatabaseDirectory = null;
            _lastInitializedExposureDatabaseSymbol = null;
            _lastInitializedExposureDatabaseModel = ActivePositionModel;
            _lastInitializedExposureDatabaseUseZeroDteOptions = UseZeroDteOptions;
            _exposurePersistenceStatus = null;
            _pendingExposurePersistenceQueue.Clear();
            _recentExposureHistoryPoints = new List<OptionExposureHistoryPoint>();
            _recentGammaStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _recentVannaStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _recentCharmStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _gammaStrikeTrend = null;
            _vannaStrikeTrend = null;
            _charmStrikeTrend = null;
        }
    }

    private void StopExposurePersistenceWorker()
    {
        CancellationTokenSource cancellationSource;
        Task workerTask;
        lock (_exposurePersistenceSyncRoot)
        {
            cancellationSource = _exposurePersistenceCancellationSource;
            workerTask = _exposurePersistenceTask;
            _pendingExposurePersistenceQueue.Clear();
            _exposurePersistenceStatus = null;
            _recentExposureHistoryPoints = new List<OptionExposureHistoryPoint>();
            _recentGammaStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _recentVannaStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _recentCharmStrikeHistoryByStrike = new Dictionary<double, List<double>>();
            _gammaStrikeTrend = null;
            _vannaStrikeTrend = null;
            _charmStrikeTrend = null;
        }

        if (cancellationSource == null)
            return;

        cancellationSource.Cancel();
        if (workerTask == null || workerTask.IsCompleted)
        {
            lock (_exposurePersistenceSyncRoot)
            {
                if (_exposurePersistenceCancellationSource == cancellationSource)
                    _exposurePersistenceCancellationSource = null;
            }
            cancellationSource.Dispose();
        }
    }

    private sealed class ExposurePersistenceRequest
    {
        public ExposurePersistenceRequest(string directory, string symbol, OptionPositionModel positionModel, bool useZeroDteOptions)
        {
            Directory = directory ?? string.Empty;
            Symbol = symbol ?? string.Empty;
            FlowIntervals = new List<OptionFlowInterval>().AsReadOnly();
            ActivePositionModel = positionModel;
            UseZeroDteOptions = useZeroDteOptions;
            IsInitialization = true;
        }

        public ExposurePersistenceRequest(
            string directory,
            OptionExposureCalculation calculation,
            DateTime effectiveTimestampUtc,
            OptionImpliedDealerSnapshot impliedDealerSnapshot,
            IReadOnlyList<OptionFlowInterval> flowIntervals,
            OptionPositionModel positionModel,
            bool useZeroDteOptions)
        {
            Directory = directory ?? string.Empty;
            Symbol = calculation != null ? calculation.UnderlyingSymbol : string.Empty;
            Calculation = calculation;
            EffectiveTimestampUtc = effectiveTimestampUtc;
            ImpliedDealerSnapshot = impliedDealerSnapshot;
            FlowIntervals = flowIntervals ?? new List<OptionFlowInterval>().AsReadOnly();
            ActivePositionModel = positionModel;
            UseZeroDteOptions = useZeroDteOptions;
        }

        public string Directory { get; private set; }

        public bool IsInitialization { get; private set; }

        public string Symbol { get; private set; }

        public OptionExposureCalculation Calculation { get; private set; }

        public DateTime EffectiveTimestampUtc { get; private set; }

        public OptionImpliedDealerSnapshot ImpliedDealerSnapshot { get; private set; }

        public IReadOnlyList<OptionFlowInterval> FlowIntervals { get; private set; }

        public OptionPositionModel ActivePositionModel { get; private set; }

        public bool UseZeroDteOptions { get; private set; }
    }

    private void UpdateExposureMappingAnchor(double underlyingPrice)
    {
        if (underlyingPrice <= 0.0 || !_hasLatestFuturesPrice || _latestFuturesPrice <= 0.0)
            return;

        if (_futuresAnchorPrice > 0.0 && Math.Abs(_anchorUnderlyingPrice - underlyingPrice) <= 0.0000001)
            return;

        _anchorUnderlyingPrice = underlyingPrice;
        _futuresAnchorPrice = _latestFuturesPrice;
    }

    private DateTime GetOptionsLastCheckedUtc()
    {
        return _optionExposureDataSource != null ? _optionExposureDataSource.LastCheckedUtc : DateTime.MinValue;
    }

    private DateTime GetOptionsLastUpdatedUtc()
    {
        return _optionExposureDataSource != null ? _optionExposureDataSource.LastUpdatedUtc : DateTime.MinValue;
    }

    private string GetActiveOptionsSymbol()
    {
        var dataSource = _optionExposureDataSource;
        return dataSource != null ? dataSource.Symbol : null;
    }
}
