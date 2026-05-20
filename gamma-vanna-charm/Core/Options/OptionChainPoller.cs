using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GammaVannaCharm.Core.Options;

public sealed class OptionChainPoller : IDisposable
{
    private readonly object _syncRoot = new object();
    private readonly string _symbol;
    private readonly TimeSpan _pollInterval;
    private readonly IOptionChainProvider _optionChainProvider;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pollTask;
    private OptionChainSnapshot? _latestSnapshot;
    private DateTime _lastCheckedUtc;
    private DateTime _lastUpdatedUtc;
    private ulong _latestSnapshotSignature;
    private string? _lastError;
    private volatile bool _disposed;

    public OptionChainPoller(string symbol, TimeSpan pollInterval, IOptionChainProvider optionChainProvider)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must be positive.");

        _symbol = symbol;
        _pollInterval = pollInterval;
        _optionChainProvider = optionChainProvider ?? throw new ArgumentNullException(nameof(optionChainProvider));
    }

    public DateTime LastCheckedUtc
    {
        get
        {
            lock (_syncRoot)
                return _lastCheckedUtc;
        }
    }

    public DateTime LastUpdatedUtc
    {
        get
        {
            lock (_syncRoot)
                return _lastUpdatedUtc;
        }
    }

    public string? LastError
    {
        get
        {
            lock (_syncRoot)
                return _lastError;
        }
    }

    public event EventHandler? PollCompleted;

    public void Start()
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            if (_pollTask != null)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_cancellationTokenSource.Token));
        }
    }

    /// <summary>
    /// Requests poll-loop cancellation and returns without waiting for an in-flight provider call.
    /// Providers should honor cancellation; if a provider completes after stop, its result is discarded
    /// before publishing state or PollCompleted.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? cancellationTokenSource;
        Task? pollTask;

        lock (_syncRoot)
        {
            cancellationTokenSource = _cancellationTokenSource;
            pollTask = _pollTask;
            _cancellationTokenSource = null;
            _pollTask = null;
        }

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            if (pollTask != null)
            {
                pollTask.ContinueWith(task =>
                {
                    var ignored = task.Exception;
                    cancellationTokenSource.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else
            {
                cancellationTokenSource.Dispose();
            }
        }
    }

    public bool TryGetLatestSnapshot(out OptionChainSnapshot? snapshot, out DateTime timestampUtc)
    {
        lock (_syncRoot)
        {
            snapshot = _latestSnapshot;
            timestampUtc = _lastUpdatedUtc;
            return snapshot != null;
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var checkedUtc = DateTime.UtcNow;
        lock (_syncRoot)
            _lastCheckedUtc = checkedUtc;

        var snapshot = await _optionChainProvider.GetOptionChainAsync(_symbol, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot == null)
            throw new InvalidOperationException("Option-chain provider returned no snapshot.");

        var snapshotTimestampUtc = snapshot.TimestampUtc == DateTime.MinValue ? checkedUtc : snapshot.TimestampUtc;
        var snapshotSignature = CalculateSnapshotContentSignature(snapshot);
        lock (_syncRoot)
        {
            if (_latestSnapshot == null || snapshotSignature != _latestSnapshotSignature)
            {
                _latestSnapshot = snapshot;
                _latestSnapshotSignature = snapshotSignature;
                _lastUpdatedUtc = ResolveLastUpdatedUtc(snapshotTimestampUtc, checkedUtc, _lastUpdatedUtc);
            }
            _lastError = null;
        }

        OnPollCompleted();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }

    private async Task PollLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                lock (_syncRoot)
                    _lastError = exception.Message;
                OnPollCompleted();
            }

            try
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnPollCompleted()
    {
        var handler = PollCompleted;
        if (handler != null)
            handler(this, EventArgs.Empty);
    }

    private static DateTime ResolveLastUpdatedUtc(DateTime snapshotTimestampUtc, DateTime checkedUtc, DateTime previousUpdatedUtc)
    {
        var updatedUtc = snapshotTimestampUtc > previousUpdatedUtc ? snapshotTimestampUtc : checkedUtc;
        if (updatedUtc <= previousUpdatedUtc)
            updatedUtc = previousUpdatedUtc.AddTicks(1L);

        return updatedUtc.Kind == DateTimeKind.Utc ? updatedUtc : DateTime.SpecifyKind(updatedUtc, DateTimeKind.Utc);
    }

    private static ulong CalculateSnapshotContentSignature(OptionChainSnapshot snapshot)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            var hash = offsetBasis;
            AddString(ref hash, snapshot.UnderlyingSymbol);
            AddDouble(ref hash, snapshot.UnderlyingPrice);
            AddLong(ref hash, snapshot.Contracts.Count);

            var orderedContracts = new List<OptionContractData>(snapshot.Contracts);
            orderedContracts.Sort((a, b) =>
            {
                var symbolCompare = string.CompareOrdinal(a.Symbol, b.Symbol);
                if (symbolCompare != 0) return symbolCompare;
                var rightCompare = ((int)a.Right).CompareTo((int)b.Right);
                if (rightCompare != 0) return rightCompare;
                var strikeCompare = a.Strike.CompareTo(b.Strike);
                if (strikeCompare != 0) return strikeCompare;
                return a.ExpirationUnixSeconds.CompareTo(b.ExpirationUnixSeconds);
            });

            for (var index = 0; index < orderedContracts.Count; index++)
            {
                var contract = orderedContracts[index];
                AddString(ref hash, contract.Symbol);
                AddLong(ref hash, (long)contract.Right);
                AddDouble(ref hash, contract.Strike);
                AddLong(ref hash, contract.OpenInterest);
                AddLong(ref hash, contract.Volume);
                AddDouble(ref hash, contract.ImpliedVolatility);
                AddDouble(ref hash, contract.Gamma);
                AddDouble(ref hash, contract.Bid);
                AddDouble(ref hash, contract.Ask);
                AddDouble(ref hash, contract.LastPrice);
                AddLong(ref hash, contract.ExpirationUnixSeconds);
            }

            return hash;
        }
    }

    private static void AddString(ref ulong hash, string? value)
    {
        AddLong(ref hash, value == null ? -1L : value.Length);
        if (value == null)
            return;

        for (var index = 0; index < value.Length; index++)
            AddLong(ref hash, value[index]);
    }

    private static void AddDouble(ref ulong hash, double value)
    {
        AddLong(ref hash, QuantizeDouble(value));
    }

    private static long QuantizeDouble(double value)
    {
        if (double.IsNaN(value))
            return long.MinValue;
        if (double.IsPositiveInfinity(value))
            return long.MaxValue;
        if (double.IsNegativeInfinity(value))
            return long.MinValue + 1L;

        var scaledValue = value * 1000000.0;
        if (scaledValue >= long.MaxValue)
            return long.MaxValue;
        if (scaledValue <= long.MinValue)
            return long.MinValue;

        return (long)Math.Round(scaledValue, MidpointRounding.AwayFromZero);
    }

    private static void AddLong(ref ulong hash, long value)
    {
        unchecked
        {
            const ulong prime = 1099511628211UL;
            var unsignedValue = (ulong)value;
            hash = (hash ^ unsignedValue) * prime;
            hash = (hash ^ (unsignedValue >> 32)) * prime;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
