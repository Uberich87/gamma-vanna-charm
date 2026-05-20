using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureDataSource : IDisposable
{
    private readonly OptionExposureCalculator _exposureCalculator;
    private readonly OptionFlowImpliedDealerStore _flowImpliedDealerStore = new OptionFlowImpliedDealerStore();
    private readonly object _calculationLock = new object();
    private readonly object _lifecycleLock = new object();
    private OptionChainPoller _poller;
    private OptionExposureCalculation _lastCalculation;
    private OptionImpliedDealerSnapshot _lastImpliedDealerSnapshot;
    private List<OptionFlowInterval> _lastFlowIntervals = new List<OptionFlowInterval>();
    private DateTime _lastCalculatedSnapshotTimestampUtc;
    private string _lastCalculationError;
    private string _symbol;
    private bool _useZeroDteOptions;
    private volatile bool _disposed;

    public OptionExposureDataSource()
        : this(new OptionExposureCalculator())
    {
    }

    public OptionExposureDataSource(OptionExposureCalculator exposureCalculator)
    {
        _exposureCalculator = exposureCalculator ?? throw new ArgumentNullException(nameof(exposureCalculator));
    }

    public event EventHandler PollCompleted;

    public DateTime LastCheckedUtc
    {
        get
        {
            var poller = _poller;
            return poller != null ? poller.LastCheckedUtc : DateTime.MinValue;
        }
    }

    public DateTime LastUpdatedUtc
    {
        get
        {
            var poller = _poller;
            return poller != null ? poller.LastUpdatedUtc : DateTime.MinValue;
        }
    }

    public string LastError
    {
        get
        {
            var poller = _poller;
            var pollerError = poller != null ? poller.LastError : null;
            if (!string.IsNullOrWhiteSpace(pollerError))
                return pollerError;

            lock (_calculationLock)
                return _lastCalculationError;
        }
    }

    public OptionImpliedDealerSnapshot LastImpliedDealerSnapshot
    {
        get
        {
            lock (_calculationLock)
                return _lastImpliedDealerSnapshot;
        }
    }

    public IReadOnlyList<OptionFlowInterval> LastFlowIntervals
    {
        get
        {
            lock (_calculationLock)
                return new List<OptionFlowInterval>(_lastFlowIntervals).AsReadOnly();
        }
    }

    public string Symbol
    {
        get
        {
            lock (_lifecycleLock)
                return _symbol;
        }
    }

    public bool UseZeroDteOptions
    {
        get
        {
            lock (_lifecycleLock)
                return _useZeroDteOptions;
        }
    }

    public bool IsRunning
    {
        get
        {
            var poller = _poller;
            return poller != null;
        }
    }

    public void Start(string symbol, TimeSpan pollInterval, IOptionChainProvider optionChainProvider)
    {
        Start(symbol, pollInterval, optionChainProvider, false);
    }

    public void Start(string symbol, TimeSpan pollInterval, IOptionChainProvider optionChainProvider, bool useZeroDteOptions)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (optionChainProvider == null)
            throw new ArgumentNullException(nameof(optionChainProvider));

        lock (_lifecycleLock)
        {
            ThrowIfDisposed();
            Stop();
            _symbol = symbol;
            _useZeroDteOptions = useZeroDteOptions;
            ClearCalculation();
            _poller = new OptionChainPoller(symbol, pollInterval, optionChainProvider);
            _poller.PollCompleted += OnPollCompleted;
            _poller.Start();
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            var poller = _poller;
            if (poller == null)
                return;

            poller.PollCompleted -= OnPollCompleted;
            poller.Dispose();
            _poller = null;
            _symbol = null;
            _useZeroDteOptions = false;
            ClearCalculation();
        }
    }

    public void ClearCalculation()
    {
        lock (_calculationLock)
        {
            _lastCalculation = null;
            _lastImpliedDealerSnapshot = null;
            _lastFlowIntervals = new List<OptionFlowInterval>();
            _lastCalculatedSnapshotTimestampUtc = DateTime.MinValue;
            _lastCalculationError = null;
            _flowImpliedDealerStore.Clear();
        }
    }

    public bool HydrateImpliedDealerSnapshot(OptionImpliedDealerSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        lock (_calculationLock)
        {
            if (_lastCalculation != null || _flowImpliedDealerStore.LastSnapshotTimestampUtc != DateTime.MinValue)
                return false;

            var hydrated = _flowImpliedDealerStore.Hydrate(snapshot);
            if (hydrated)
                _lastImpliedDealerSnapshot = _flowImpliedDealerStore.CreateSnapshot();
            return hydrated;
        }
    }

    public Task PollOnceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var poller = _poller;
        if (poller == null)
            throw new InvalidOperationException("Options poller is not running.");

        return poller.PollOnceAsync(cancellationToken);
    }

    public bool TryGetCalculation(DateTime valuationTimeUtc, out OptionExposureCalculation calculation, out string status)
    {
        ThrowIfDisposed();
        calculation = null;
        status = null;

        var poller = _poller;
        if (poller == null)
        {
            status = "Options poller is not running.";
            return false;
        }

        OptionChainSnapshot snapshot;
        DateTime timestampUtc;
        if (!poller.TryGetLatestSnapshot(out snapshot, out timestampUtc))
        {
            status = "Waiting for options data for " + (_symbol ?? "selected symbol") + ".";
            return false;
        }

        lock (_calculationLock)
        {
            if (_lastCalculation == null || timestampUtc != _lastCalculatedSnapshotTimestampUtc)
            {
                try
                {
                    var impliedDealerSnapshot = _flowImpliedDealerStore.Update(snapshot);
                    // Use the provider snapshot time for Greek/exposure valuation so repeated polls of
                    // unchanged closed-market data do not drift just because wall-clock time advanced.
                    var snapshotValuationTimeUtc = snapshot.TimestampUtc == DateTime.MinValue ? valuationTimeUtc : snapshot.TimestampUtc;
                    var nextCalculation = _exposureCalculator.Calculate(snapshot, snapshotValuationTimeUtc, impliedDealerSnapshot);
                    DateTime expiredZeroDteValuationTimeUtc;
                    if (!HasActiveContracts(nextCalculation)
                        && _useZeroDteOptions
                        && TryResolveExpiredZeroDteValuationTime(snapshot, snapshotValuationTimeUtc, out expiredZeroDteValuationTimeUtc))
                    {
                        nextCalculation = _exposureCalculator.Calculate(snapshot, expiredZeroDteValuationTimeUtc, impliedDealerSnapshot);
                    }

                    if (HasActiveContracts(nextCalculation) || !HasActiveContracts(_lastCalculation))
                    {
                        _lastCalculation = nextCalculation;
                        _lastImpliedDealerSnapshot = impliedDealerSnapshot;
                        _lastFlowIntervals = new List<OptionFlowInterval>(_flowImpliedDealerStore.LastIntervals);
                    }
                    _lastCalculatedSnapshotTimestampUtc = timestampUtc;
                    _lastCalculationError = null;
                }
                catch (Exception exception)
                {
                    _lastCalculationError = "Could not calculate options exposure: " + FormatCalculationException(exception);
                    status = _lastCalculationError;
                    return false;
                }
            }

            calculation = _lastCalculation;
            return calculation != null;
        }
    }

    private static bool HasActiveContracts(OptionExposureCalculation calculation)
    {
        return calculation != null
            && ((calculation.NaiveSummary != null && calculation.NaiveSummary.ContractCount > 0)
                || (calculation.FlowImpliedDealerSummary != null && calculation.FlowImpliedDealerSummary.ContractCount > 0));
    }

    private static bool TryResolveExpiredZeroDteValuationTime(OptionChainSnapshot snapshot, DateTime valuationTimeUtc, out DateTime expiredZeroDteValuationTimeUtc)
    {
        expiredZeroDteValuationTimeUtc = DateTime.MinValue;
        if (snapshot == null || snapshot.Contracts == null || snapshot.Contracts.Count == 0 || valuationTimeUtc == DateTime.MinValue)
            return false;

        var latestExpirationUtc = DateTime.MinValue;
        for (var index = 0; index < snapshot.Contracts.Count; index++)
        {
            var contract = snapshot.Contracts[index];
            if (contract == null || contract.ExpirationUnixSeconds <= 0L)
                continue;

            var expirationUtc = UnixSecondsToUtc(contract.ExpirationUnixSeconds);
            if (expirationUtc > latestExpirationUtc)
                latestExpirationUtc = expirationUtc;
        }

        if (latestExpirationUtc == DateTime.MinValue || latestExpirationUtc > valuationTimeUtc)
            return false;

        expiredZeroDteValuationTimeUtc = latestExpirationUtc.AddMinutes(-1.0);
        return expiredZeroDteValuationTimeUtc > DateTime.MinValue;
    }

    private static DateTime UnixSecondsToUtc(long unixSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
        }
    }

    private void OnPollCompleted(object sender, EventArgs eventArgs)
    {
        PrecalculateLatestSnapshot();

        var handler = PollCompleted;
        if (handler != null)
            handler(this, EventArgs.Empty);
    }

    private void PrecalculateLatestSnapshot()
    {
        if (_disposed)
            return;

        try
        {
            var poller = _poller;
            if (poller == null)
                return;

            OptionChainSnapshot snapshot;
            DateTime timestampUtc;
            if (!poller.TryGetLatestSnapshot(out snapshot, out timestampUtc))
                return;

            // Skip pre-calculation when snapshot has no definitive timestamp, as calculation
            // would depend on the caller's valuation time and shouldn't be cached globally.
            if (snapshot.TimestampUtc == DateTime.MinValue)
                return;

            OptionExposureCalculation calculation;
            string status;
            TryGetCalculation(DateTime.UtcNow, out calculation, out status);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string FormatCalculationException(Exception exception)
    {
        if (exception == null)
            return "unknown error";

        return string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
