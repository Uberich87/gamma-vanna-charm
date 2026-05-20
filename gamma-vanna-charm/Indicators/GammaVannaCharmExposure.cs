#region Using declarations
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.MarketData.Cboe;
using GammaVannaCharm.Core.Options;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure : Indicator
{
    public override string DisplayName
    {
        get { return State == State.SetDefaults || ChartControl == null ? Name : string.Empty; }
    }

    protected override void OnStateChange()
    {
        if (State == State.SetDefaults)
        {
            Description = "Renders gamma, vanna, and charm option-chain exposure as a price-panel histogram.";
            Name = "_Gamma Vanna Charm Exposures";
            Calculate = Calculate.OnPriceChange;
            IsOverlay = true;
            DisplayInDataBox = false;
            DrawHorizontalGridLines = true;
            DrawOnPricePanel = true;
            DrawVerticalGridLines = true;
            IsAutoScale = false;
            IsSuspendedWhileInactive = true;
            MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
            PaintPriceMarkers = false;
            ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            BarsRequiredToPlot = 1;

            ExposureMetric = OptionExposureMetric.Overall;
            UseZeroDteOptions = true;
            TableXOffsetPixels = 0;
            TableYOffsetPixels = -10;
            HeatmapBandTicks = 12.0;
            HeatmapMaxOpacity = 50;
            HeatmapMaxWidthPercent = 35;
            GammaPositiveExposureBrush = Brushes.DodgerBlue;
            GammaNegativeExposureBrush = Brushes.Firebrick;
            VannaPositiveExposureBrush = Brushes.MediumPurple;
            VannaNegativeExposureBrush = Brushes.Goldenrod;
            CharmPositiveExposureBrush = Brushes.MediumSeaGreen;
            CharmNegativeExposureBrush = Brushes.DeepPink;
            ExposureProxySymbol = SpotProxySymbol.Auto;
            OptionsPollSeconds = 60;
            OptionsEndpointBase = CboeOptionsClient.DefaultOptionsEndpointBase;
            ImpliedDataDirectory = OptionExposureHistorySqliteStore.DefaultDatabaseDirectory;
            SpyOptionsSymbol = "SPY";
            SpxOptionsSymbol = "_SPX";
            QqqOptionsSymbol = "QQQ";
            SpyToEsMultiplier = 10.0;
            SpxToEsMultiplier = 1.0;
            QqqToNqMultiplier = 40.0;
        }
        else if (State == State.DataLoaded)
        {
            _isTerminated = false;
            _optionExposureDataSource = new OptionExposureDataSource();
            _backgroundOptionExposureDataSources = new List<OptionExposureDataSource>();
            _proxyConversionMultipliers = CreateProxyConversionMultipliers();
            _optionStrikeExposures = new List<OptionStrikeExposure>();
            _optionLevelTags = new HashSet<string>();
            StartVersionCheck();
        }
        else if (State == State.Historical)
        {
            SetZOrder(-1);
        }
        else if (State == State.Realtime)
        {
            SnapshotCurrentFuturesPrice();
            ConfigureSpotPriceProvider();
        }
        else if (State == State.Terminated)
        {
            _isTerminated = true;
            StopVersionCheck();
            StopExposurePersistenceWorker();
            DisposeOptionsPoller();
            ClearOptionLevelDrawings();
            DisposeHistogramRenderResources();
            _optionExposureDataSource = null;
            _backgroundOptionExposureDataSources = null;
            _proxyConversionMultipliers = null;
            _optionStrikeExposures = null;
            _optionLevelTags = null;
            Interlocked.Exchange(ref _optionsProcessingQueued, 0);
            RemoveExposureCardOverlay();
            _chartDispatcher = null;
        }
    }

    protected override void OnBarUpdate()
    {
        if (BarsInProgress != 0 || _optionExposureDataSource == null)
            return;
        if (!ShouldRunPriceDrivenRefresh())
            return;

        SnapshotCurrentFuturesPrice();
        RefreshOptionExposureRenderState();
    }

    private bool ShouldRunPriceDrivenRefresh()
    {
        var nowUtc = DateTime.UtcNow;
        if (_nextPriceDrivenRefreshUtc != DateTime.MinValue && nowUtc < _nextPriceDrivenRefreshUtc)
            return false;

        _nextPriceDrivenRefreshUtc = nowUtc.AddSeconds(15.0);
        return true;
    }

    private void SnapshotCurrentFuturesPrice()
    {
        if (CurrentBar < 0)
            return;

        var futuresPrice = Close[0];
        if (futuresPrice <= 0.0 || double.IsNaN(futuresPrice) || double.IsInfinity(futuresPrice))
            return;

        _latestFuturesPrice = futuresPrice;
        _hasLatestFuturesPrice = true;
    }

    private void RefreshOptionExposureRenderState()
    {
        lock (_optionDataProcessingSyncRoot)
        {
            if (_isTerminated || _optionExposureDataSource == null)
                return;

            RefreshRuntimeSettingsIfChanged();

            OptionExposureSummary exposureSummary;
            TryResolveOptionExposureSummary(out exposureSummary);

            UpdateOptionLevelDrawings();
            DrawOptionStatusText();
        }
    }

    private void OnOptionsPollCompleted(object sender, EventArgs eventArgs)
    {
        QueueOptionsProcessing();
    }

    private void OnBackgroundOptionsPollCompleted(object sender, EventArgs eventArgs)
    {
        PersistBackgroundOptionsData(sender as OptionExposureDataSource);
    }

    private void QueueOptionsProcessing()
    {
        if (_isTerminated || Interlocked.Exchange(ref _optionsProcessingQueued, 1) == 1)
            return;

        var dispatcher = _chartDispatcher;
        var chartControl = ChartControl;
        if (dispatcher == null && chartControl != null)
        {
            dispatcher = chartControl.Dispatcher;
            _chartDispatcher = dispatcher;
        }

        if (dispatcher == null)
        {
            Interlocked.Exchange(ref _optionsProcessingQueued, 0);
            return;
        }

        try
        {
            dispatcher.BeginInvoke(new Action(ProcessQueuedOptionsUpdate), DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _optionsProcessingQueued, 0);
        }
    }

    private void ProcessQueuedOptionsUpdate()
    {
        Interlocked.Exchange(ref _optionsProcessingQueued, 0);
        if (_isTerminated)
            return;

        RefreshOptionExposureRenderState();
    }
}
