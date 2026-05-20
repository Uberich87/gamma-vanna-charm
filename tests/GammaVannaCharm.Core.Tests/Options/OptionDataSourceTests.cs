using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GammaVannaCharm.Core.Exposure;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Tests.Options;

internal static class OptionDataSourceTests
{
    public static void Run()
    {
        CalculatesProviderNeutralExposureBundle();
        ReportsWaitingStatusBeforeFirstProviderSnapshot();
        PollsInjectedOptionChainProvider();
        DataSourcePollCompletedPublishesCalculatedSnapshot();
        PublishesCompletedPollsForUnchangedSnapshotTimestamps();
        IgnoresProviderTimestampOnlyChanges();
        CalculatesLatestExpiredZeroDteSnapshotAtExpiration();
        PollerCanRestartImmediatelyAfterStoppingNonCooperativeProvider();
        PollerDiscardsStoppedProviderErrorsAfterRestart();
        PollerStopCanBeRepeatedWhileProviderCallsAreInFlight();
        CalculatesFromInjectedDataSourceProvider();
        ProducesEquivalentExposuresFromDifferentProviderMappings();
    }

    public static void CalculatesProviderNeutralExposureBundle()
    {
        var snapshot = CreateSnapshot();
        var calculation = new OptionExposureCalculator().Calculate(
            snapshot,
            new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc));

        TestAssert.Equal("TEST", calculation.UnderlyingSymbol, "Provider-neutral calculation keeps underlying symbol");
        TestAssert.Equal(100.0, calculation.UnderlyingPrice, "Provider-neutral calculation keeps underlying price");
        TestAssert.True(calculation.NaiveSummary.ContractCount > 0, "Provider-neutral calculation builds summary");
        TestAssert.True(calculation.NaiveStrikeExposures.Count > 0, "Provider-neutral calculation builds strike exposures");
    }

    public static void ReportsWaitingStatusBeforeFirstProviderSnapshot()
    {
        var provider = new FakeOptionChainProvider(CreateSnapshot());
        using (var dataSource = new OptionExposureDataSource())
        {
            dataSource.Start("TEST", TimeSpan.FromMinutes(1.0), provider);

            OptionExposureCalculation calculation;
            string status;
            TestAssert.False(dataSource.TryGetCalculation(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), out calculation, out status), "Data source waits before provider snapshot");
            TestAssert.Equal<OptionExposureCalculation>(null, calculation, "Waiting data source has no calculation");
            TestAssert.Equal("Waiting for options data for TEST.", status, "Waiting status is provider neutral");
        }
    }

    public static void PollsInjectedOptionChainProvider()
    {
        var provider = new FakeOptionChainProvider(CreateSnapshot());
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            poller.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            OptionChainSnapshot snapshot;
            DateTime timestampUtc;
            TestAssert.True(poller.TryGetLatestSnapshot(out snapshot, out timestampUtc), "Injected provider poll stores snapshot");
            TestAssert.Equal("TEST", snapshot.UnderlyingSymbol, "Injected provider snapshot symbol");
            TestAssert.True(timestampUtc.Kind == DateTimeKind.Utc, "Poll timestamp is UTC");
            TestAssert.Equal(snapshot.TimestampUtc, timestampUtc, "Poll updated timestamp uses provider snapshot timestamp");
            TestAssert.Equal<string>(null, poller.LastError, "Successful injected provider poll clears error");
            TestAssert.Equal(1, provider.CallCount, "Injected provider called once");
            TestAssert.Equal("TEST", provider.LastRequestedSymbol, "Injected provider receives requested symbol");
        }
    }

    public static void DataSourcePollCompletedPublishesCalculatedSnapshot()
    {
        var provider = new FakeOptionChainProvider(CreateSnapshot());
        using (var dataSource = new OptionExposureDataSource())
        {
            OptionExposureCalculation eventCalculation = null;
            string eventStatus = null;
            dataSource.PollCompleted += delegate(object sender, EventArgs args)
            {
                dataSource.TryGetCalculation(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), out eventCalculation, out eventStatus);
            };

            dataSource.Start("TEST", TimeSpan.FromMinutes(1.0), provider);
            dataSource.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            TestAssert.Equal<string>(null, eventStatus, "Data-source poll completion has no calculation status");
            TestAssert.True(eventCalculation != null, "Data-source poll completion publishes after calculation is available");
            TestAssert.True(eventCalculation.NaiveSummary.ContractCount > 0, "Data-source poll completion calculation has exposure");
        }
    }

    public static void PublishesCompletedPollsForUnchangedSnapshotTimestamps()
    {
        var provider = new FakeOptionChainProvider(CreateSnapshot());
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            var pollCompletedCount = 0;
            poller.PollCompleted += (sender, args) => pollCompletedCount++;

            poller.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();
            var firstLastUpdatedUtc = poller.LastUpdatedUtc;
            poller.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            TestAssert.Equal(2, provider.CallCount, "Provider is still polled for unchanged timestamps");
            TestAssert.Equal(2, pollCompletedCount, "Each successful provider poll publishes completion so checked time can refresh");
            TestAssert.Equal(firstLastUpdatedUtc, poller.LastUpdatedUtc, "Unchanged snapshot timestamp does not change last updated time");
        }
    }

    public static void IgnoresProviderTimestampOnlyChanges()
    {
        var firstSnapshot = CreateSnapshot(new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
        var secondSnapshot = CreateSnapshot(new DateTime(2026, 5, 15, 12, 1, 0, DateTimeKind.Utc));
        var provider = new SequenceOptionChainProvider(firstSnapshot, secondSnapshot);
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            var pollCompletedCount = 0;
            poller.PollCompleted += (sender, args) => pollCompletedCount++;

            poller.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();
            var firstLastUpdatedUtc = poller.LastUpdatedUtc;
            poller.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            OptionChainSnapshot snapshot;
            DateTime timestampUtc;
            TestAssert.True(poller.TryGetLatestSnapshot(out snapshot, out timestampUtc), "Timestamp-only provider poll keeps a latest snapshot");
            TestAssert.Equal(2, provider.CallCount, "Timestamp-only provider is still polled");
            TestAssert.Equal(2, pollCompletedCount, "Timestamp-only provider poll still publishes completion");
            TestAssert.Equal(firstLastUpdatedUtc, poller.LastUpdatedUtc, "Provider timestamp-only change does not change last updated time");
            TestAssert.Equal(firstSnapshot.TimestampUtc, snapshot.TimestampUtc, "Provider timestamp-only change does not replace latest snapshot");
        }
    }

    public static void CalculatesLatestExpiredZeroDteSnapshotAtExpiration()
    {
        var expirationUtc = new DateTime(2026, 5, 15, 20, 15, 0, DateTimeKind.Utc);
        var expiredSnapshot = CreateSingleExpirationSnapshot(new DateTime(2026, 5, 15, 20, 16, 0, DateTimeKind.Utc), expirationUtc, 11);
        var provider = new FakeOptionChainProvider(expiredSnapshot);
        using (var dataSource = new OptionExposureDataSource())
        {
            dataSource.Start("TEST", TimeSpan.FromMinutes(1.0), provider, true);
            dataSource.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            OptionExposureCalculation calculation;
            string status;
            TestAssert.True(dataSource.TryGetCalculation(DateTime.MinValue, out calculation, out status), "Expired 0DTE snapshot still calculates from latest provider data");
            TestAssert.Equal(expiredSnapshot.TimestampUtc, calculation.SnapshotTimestampUtc, "Expired 0DTE calculation keeps provider snapshot timestamp");
            TestAssert.Equal(1, calculation.NaiveSummary.ContractCount, "Expired 0DTE calculation uses contracts from the latest API pull");
        }
    }

    public static void PollerCanRestartImmediatelyAfterStoppingNonCooperativeProvider()
    {
        var provider = new RestartableBlockingOptionChainProvider(CreateSnapshot());
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            var pollCompletedCount = 0;
            poller.PollCompleted += (sender, args) => Interlocked.Increment(ref pollCompletedCount);

            poller.Start();
            TestAssert.True(SpinWait.SpinUntil(() => provider.FirstCallStarted.IsSet, TimeSpan.FromSeconds(2.0)), "First poll starts before restart test");

            poller.Stop();
            poller.Start();

            TestAssert.True(SpinWait.SpinUntil(() => provider.CallCount >= 2, TimeSpan.FromSeconds(2.0)), "Poller can restart while stopped provider call is still unwinding");
            TestAssert.True(SpinWait.SpinUntil(() => Volatile.Read(ref pollCompletedCount) >= 1, TimeSpan.FromSeconds(2.0)), "Restarted poll publishes completion");
            provider.CompleteFirstCall();
            AssertPollCompletedCountStable(() => Volatile.Read(ref pollCompletedCount), 1, "Stopped in-flight poll result is discarded");
        }
    }

    public static void PollerDiscardsStoppedProviderErrorsAfterRestart()
    {
        var provider = new RestartableFailingOptionChainProvider(CreateSnapshot());
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            var pollCompletedCount = 0;
            poller.PollCompleted += (sender, args) => Interlocked.Increment(ref pollCompletedCount);

            poller.Start();
            TestAssert.True(SpinWait.SpinUntil(() => provider.FirstCallStarted.IsSet, TimeSpan.FromSeconds(2.0)), "First poll starts before stopped error test");

            poller.Stop();
            poller.Start();

            TestAssert.True(SpinWait.SpinUntil(() => provider.CallCount >= 2, TimeSpan.FromSeconds(2.0)), "Poller restarts before stopped provider error is released");
            TestAssert.True(SpinWait.SpinUntil(() => Volatile.Read(ref pollCompletedCount) >= 1, TimeSpan.FromSeconds(2.0)), "Restarted poll publishes completion before stopped provider error");
            provider.FailFirstCall();
            AssertPollCompletedCountStable(() => Volatile.Read(ref pollCompletedCount), 1, "Stopped provider error is discarded after restart");
            TestAssert.Equal<string>(null, poller.LastError, "Stopped provider error does not publish last error after restart");
        }
    }

    public static void PollerStopCanBeRepeatedWhileProviderCallsAreInFlight()
    {
        var provider = new SharedBlockingOptionChainProvider(CreateSnapshot());
        using (var poller = new OptionChainPoller("TEST", TimeSpan.FromMinutes(1.0), provider))
        {
            var pollCompletedCount = 0;
            poller.PollCompleted += (sender, args) => Interlocked.Increment(ref pollCompletedCount);

            const int cycleCount = 8;
            for (var index = 0; index < cycleCount; index++)
            {
                poller.Start();
                TestAssert.True(SpinWait.SpinUntil(() => provider.CallCount >= index + 1, TimeSpan.FromSeconds(2.0)), "Provider call starts for stop cycle " + index);
                poller.Stop();
            }

            provider.CompleteAllCalls();
            AssertPollCompletedCountStable(() => Volatile.Read(ref pollCompletedCount), 0, "Stopped in-flight poll results are discarded after repeated stops");
        }
    }

    public static void CalculatesFromInjectedDataSourceProvider()
    {
        var provider = new FakeOptionChainProvider(CreateSnapshot());
        using (var dataSource = new OptionExposureDataSource())
        {
            dataSource.Start("TEST", TimeSpan.FromMinutes(1.0), provider);
            dataSource.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            OptionExposureCalculation calculation;
            string status;
            TestAssert.True(dataSource.TryGetCalculation(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), out calculation, out status), "Injected data source calculates exposure");
            TestAssert.Equal<string>(null, status, "Injected data source has no waiting status");
            TestAssert.Equal("TEST", calculation.UnderlyingSymbol, "Injected data source calculation symbol");
            TestAssert.True(calculation.NaiveSummary.ContractCount > 0, "Injected data source calculation summary");
        }
    }

    public static void ProducesEquivalentExposuresFromDifferentProviderMappings()
    {
        var sourceAProvider = new FakeOptionChainProvider(CreateSnapshot("SOURCE_A_UNDERLYING", "A:"));
        var sourceBProvider = new FakeOptionChainProvider(CreateSnapshot("SOURCE_B_UNDERLYING", "B-"));

        var sourceACalculation = CalculateOnce("SOURCE_A_SYMBOL", sourceAProvider);
        var sourceBCalculation = CalculateOnce("SOURCE_B_SYMBOL", sourceBProvider);

        TestAssert.Equal("SOURCE_A_SYMBOL", sourceAProvider.LastRequestedSymbol, "Source A provider receives its own symbol format");
        TestAssert.Equal("SOURCE_B_SYMBOL", sourceBProvider.LastRequestedSymbol, "Source B provider receives its own symbol format");
        TestAssert.Equal("SOURCE_A_UNDERLYING", sourceACalculation.UnderlyingSymbol, "Source A mapping keeps provider-neutral underlying symbol");
        TestAssert.Equal("SOURCE_B_UNDERLYING", sourceBCalculation.UnderlyingSymbol, "Source B mapping keeps provider-neutral underlying symbol");

        AssertEquivalentExposureSummary(sourceACalculation.NaiveSummary, sourceBCalculation.NaiveSummary, "Different provider mappings produce equivalent summary");
        TestAssert.Equal(sourceACalculation.NaiveStrikeExposures.Count, sourceBCalculation.NaiveStrikeExposures.Count, "Different provider mappings produce same strike count");
        for (var index = 0; index < sourceACalculation.NaiveStrikeExposures.Count; index++)
            AssertEquivalentStrikeExposure(sourceACalculation.NaiveStrikeExposures[index], sourceBCalculation.NaiveStrikeExposures[index], "Different provider mappings produce equivalent strike " + index);
    }

    private static OptionExposureCalculation CalculateOnce(string symbol, IOptionChainProvider provider)
    {
        using (var dataSource = new OptionExposureDataSource())
        {
            dataSource.Start(symbol, TimeSpan.FromMinutes(1.0), provider);
            dataSource.PollOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

            OptionExposureCalculation calculation;
            string status;
            TestAssert.True(dataSource.TryGetCalculation(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), out calculation, out status), "Provider-neutral data source calculates exposure for " + symbol);
            TestAssert.Equal<string>(null, status, "Provider-neutral data source has no waiting status for " + symbol);
            return calculation;
        }
    }

    private static void AssertPollCompletedCountStable(Func<int> getPollCompletedCount, int expectedCount, string message)
    {
        TestAssert.False(SpinWait.SpinUntil(() => getPollCompletedCount() != expectedCount, TimeSpan.FromSeconds(1.0)), message);
        TestAssert.Equal(expectedCount, getPollCompletedCount(), message + " count");
    }

    private static void AssertEquivalentExposureSummary(OptionExposureSummary expected, OptionExposureSummary actual, string message)
    {
        TestAssert.Equal(expected.ContractCount, actual.ContractCount, message + " contract count");
        TestAssert.Equal(expected.GammaExposure, actual.GammaExposure, message + " gamma");
        TestAssert.Equal(expected.VannaExposure, actual.VannaExposure, message + " vanna");
        TestAssert.Equal(expected.CharmExposure, actual.CharmExposure, message + " charm");
    }

    private static void AssertEquivalentStrikeExposure(OptionStrikeExposure expected, OptionStrikeExposure actual, string message)
    {
        TestAssert.Equal(expected.Strike, actual.Strike, message + " strike");
        TestAssert.Equal(expected.ContractCount, actual.ContractCount, message + " contract count");
        TestAssert.Equal(expected.OpenInterest, actual.OpenInterest, message + " open interest");
        TestAssert.Equal(expected.GammaExposure, actual.GammaExposure, message + " gamma");
        TestAssert.Equal(expected.VannaExposure, actual.VannaExposure, message + " vanna");
        TestAssert.Equal(expected.CharmExposure, actual.CharmExposure, message + " charm");
    }

    private static OptionChainSnapshot CreateSnapshot()
    {
        return CreateSnapshot(new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    private static OptionChainSnapshot CreateSnapshot(DateTime timestampUtc)
    {
        return CreateSnapshot("TEST", "TEST260515", timestampUtc);
    }

    private static OptionChainSnapshot CreateSnapshot(string underlyingSymbol, string contractSymbolPrefix)
    {
        return CreateSnapshot(underlyingSymbol, contractSymbolPrefix, new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    private static OptionChainSnapshot CreateSnapshot(string underlyingSymbol, string contractSymbolPrefix, DateTime timestampUtc)
    {
        return new OptionChainSnapshot(
            underlyingSymbol,
            100.0,
            timestampUtc,
            new List<OptionContractData>
            {
                new OptionContractData
                {
                    Symbol = contractSymbolPrefix + "C00100000",
                    Right = OptionRight.Call,
                    Strike = 100.0,
                    OpenInterest = 10,
                    Volume = 0,
                    ImpliedVolatility = 0.20,
                    Gamma = 0.05,
                    ExpirationUnixSeconds = ToUnixSeconds(new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc))
                },
                new OptionContractData
                {
                    Symbol = contractSymbolPrefix + "P00095000",
                    Right = OptionRight.Put,
                    Strike = 95.0,
                    OpenInterest = 7,
                    Volume = 0,
                    ImpliedVolatility = 0.24,
                    Gamma = 0.04,
                    ExpirationUnixSeconds = ToUnixSeconds(new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc))
                }
            });
    }

    private static OptionChainSnapshot CreateSingleExpirationSnapshot(DateTime timestampUtc, DateTime expirationUtc, long openInterest)
    {
        return new OptionChainSnapshot(
            "TEST",
            100.0,
            timestampUtc,
            new List<OptionContractData>
            {
                new OptionContractData
                {
                    Symbol = "TEST260515C00100000",
                    Right = OptionRight.Call,
                    Strike = 100.0,
                    OpenInterest = openInterest,
                    Volume = 0,
                    ImpliedVolatility = 0.20,
                    Gamma = 0.05,
                    ExpirationUnixSeconds = ToUnixSeconds(expirationUtc)
                }
            });
    }

    private static long ToUnixSeconds(DateTime timestampUtc)
    {
        return (long)(timestampUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    private sealed class SharedBlockingOptionChainProvider : IOptionChainProvider
    {
        private readonly OptionChainSnapshot _snapshot;
        private readonly TaskCompletionSource<OptionChainSnapshot> _completion = new TaskCompletionSource<OptionChainSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public SharedBlockingOptionChainProvider(OptionChainSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int CallCount
        {
            get { return Volatile.Read(ref _callCount); }
        }

        public void CompleteAllCalls()
        {
            _completion.TrySetResult(_snapshot);
        }

        public Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return _completion.Task;
        }
    }

    private sealed class RestartableBlockingOptionChainProvider : IOptionChainProvider
    {
        private readonly OptionChainSnapshot _snapshot;
        private readonly TaskCompletionSource<OptionChainSnapshot> _firstCallCompletion = new TaskCompletionSource<OptionChainSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public RestartableBlockingOptionChainProvider(OptionChainSnapshot snapshot)
        {
            _snapshot = snapshot;
            FirstCallStarted = new ManualResetEventSlim(false);
        }

        public ManualResetEventSlim FirstCallStarted { get; private set; }

        public int CallCount
        {
            get { return Volatile.Read(ref _callCount); }
        }

        public void CompleteFirstCall()
        {
            _firstCallCompletion.TrySetResult(_snapshot);
        }

        public Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
        {
            var callCount = Interlocked.Increment(ref _callCount);
            if (callCount == 1)
            {
                FirstCallStarted.Set();
                return _firstCallCompletion.Task;
            }

            return Task.FromResult(_snapshot);
        }
    }

    private sealed class RestartableFailingOptionChainProvider : IOptionChainProvider
    {
        private readonly OptionChainSnapshot _snapshot;
        private readonly TaskCompletionSource<OptionChainSnapshot> _firstCallCompletion = new TaskCompletionSource<OptionChainSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public RestartableFailingOptionChainProvider(OptionChainSnapshot snapshot)
        {
            _snapshot = snapshot;
            FirstCallStarted = new ManualResetEventSlim(false);
        }

        public ManualResetEventSlim FirstCallStarted { get; private set; }

        public int CallCount
        {
            get { return Volatile.Read(ref _callCount); }
        }

        public void FailFirstCall()
        {
            _firstCallCompletion.TrySetException(new InvalidOperationException("Stopped provider failed."));
        }

        public Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
        {
            var callCount = Interlocked.Increment(ref _callCount);
            if (callCount == 1)
            {
                FirstCallStarted.Set();
                return _firstCallCompletion.Task;
            }

            return Task.FromResult(_snapshot);
        }
    }

    private sealed class FakeOptionChainProvider : IOptionChainProvider
    {
        private readonly OptionChainSnapshot _snapshot;

        public FakeOptionChainProvider(OptionChainSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int CallCount { get; private set; }

        public string LastRequestedSymbol { get; private set; }

        public Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestedSymbol = symbol;
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class SequenceOptionChainProvider : IOptionChainProvider
    {
        private readonly OptionChainSnapshot[] _snapshots;

        public SequenceOptionChainProvider(params OptionChainSnapshot[] snapshots)
        {
            _snapshots = snapshots;
        }

        public int CallCount { get; private set; }

        public Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
        {
            var index = Math.Min(CallCount, _snapshots.Length - 1);
            CallCount++;
            return Task.FromResult(_snapshots[index]);
        }
    }
}
