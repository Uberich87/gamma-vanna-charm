using System;
using System.Collections.Generic;
using System.IO;
using GammaVannaCharm.Core.Exposure;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Tests.Options;

internal static class OptionFlowImpliedDealerTests
{
    public static void Run()
    {
        FirstSnapshotSeedsFlowImpliedExposure();
        AppliesInferredFlowToSeededDealerState();
        ResetsFlowStateWhenSnapshotDateChanges();
        PersistsExposureHistoryDatabase();
        LoadsRecentExposureHistoryAndLatestDealerState();
        LoadsRecentExposureHistoryWithAbsoluteLevelTotals();
        LoadsStrikeHistoryAcrossDateShards();
        InitializesExposureHistoryDatabase();
        CountsEmptyExistingHistoryDatabaseAsZero();
        PersistsEffectiveTimestampWhenSnapshotTimestampIsMissing();
        SeparatesExposureHistoryByDataScope();
        RejectsInvalidFlowIntervals();
    }

    public static void RejectsInvalidFlowIntervals()
    {
        var startTimestampUtc = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc);
        var endTimestampUtc = startTimestampUtc.AddMinutes(1.0);

        AssertThrows<ArgumentException>(() => new OptionFlowInterval(null, startTimestampUtc, endTimestampUtc, 0L, 0.0, 0.5), "Option flow interval requires symbol");
        AssertThrows<ArgumentException>(() => new OptionFlowInterval("TEST", DateTime.SpecifyKind(startTimestampUtc, DateTimeKind.Unspecified), endTimestampUtc, 0L, 0.0, 0.5), "Option flow interval requires UTC start timestamp");
        AssertThrows<ArgumentException>(() => new OptionFlowInterval("TEST", startTimestampUtc, DateTime.SpecifyKind(endTimestampUtc, DateTimeKind.Unspecified), 0L, 0.0, 0.5), "Option flow interval requires UTC end timestamp");
        AssertThrows<ArgumentException>(() => new OptionFlowInterval("TEST", endTimestampUtc, startTimestampUtc, 0L, 0.0, 0.5), "Option flow interval rejects inverted interval");
        AssertThrows<ArgumentOutOfRangeException>(() => new OptionFlowInterval("TEST", startTimestampUtc, endTimestampUtc, 0L, 0.0, 1.5), "Option flow interval rejects invalid confidence");
    }

    public static void FirstSnapshotSeedsFlowImpliedExposure()
    {
        var store = new OptionFlowImpliedDealerStore();
        var firstSnapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.20);

        var impliedSnapshot = store.Update(firstSnapshot);
        var calculation = new OptionExposureCalculator().Calculate(
            firstSnapshot,
            ValuationTimeUtc,
            impliedSnapshot);

        TestAssert.True(impliedSnapshot.HasSeededBaseline, "First snapshot seeds flow baseline from OI");
        TestAssert.True(!impliedSnapshot.HasFlowIntervals, "First snapshot has no classified flow interval yet");
        TestAssert.True(calculation.HasFlowImpliedDealerExposure, "First snapshot produces seeded flow-implied dealer exposure");
        TestAssert.Equal(1, calculation.FlowImpliedDealerSummary.ContractCount, "First snapshot flow summary includes seeded contract");
        TestAssert.Equal(1, calculation.FlowImpliedDealerStrikeExposures.Count, "First snapshot flow strikes include seeded strike");
        TestAssert.Equal(calculation.NaiveSummary.GammaExposure, calculation.FlowImpliedDealerSummary.GammaExposure, "Seeded call GEX matches naive baseline before flow deltas");
    }

    public static void AppliesInferredFlowToSeededDealerState()
    {
        var store = new OptionFlowImpliedDealerStore();
        var firstSnapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10);
        var secondSnapshot = CreateSnapshot(20, 100, 0.22, 1.00, 1.20, 1.20);
        store.Update(firstSnapshot);

        var impliedSnapshot = store.Update(secondSnapshot);
        var calculation = new OptionExposureCalculator().Calculate(
            secondSnapshot,
            ValuationTimeUtc,
            impliedSnapshot);

        TestAssert.True(calculation.HasFlowImpliedDealerExposure, "Second snapshot has flow-implied exposure");
        TestAssert.True(calculation.NaiveSummary.GammaExposure > 0.0, "Naive call gamma remains positive");
        TestAssert.True(calculation.FlowImpliedDealerSummary.GammaExposure < calculation.NaiveSummary.GammaExposure, "Customer buy flow reduces seeded dealer long-call gamma");
        TestAssert.True(impliedSnapshot.HasFlowIntervals, "Second snapshot records classified flow interval");
        TestAssert.Equal(1, calculation.FlowImpliedDealerSummary.ContractCount, "Flow-implied summary includes flowed contract");
        TestAssert.Equal(1, calculation.FlowImpliedDealerStrikeExposures.Count, "Flow-implied strikes include flowed strike");
    }

    public static void ResetsFlowStateWhenSnapshotDateChanges()
    {
        // Build prior-day dealer state from two snapshots, then verify a new day clears it.
        var store = new OptionFlowImpliedDealerStore();
        var previousDayFirst = CreateSnapshot(
            10,
            100,
            0.20,
            1.00,
            1.20,
            1.10,
            new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc));
        var previousDaySecond = CreateSnapshot(
            100,
            100,
            0.22,
            1.00,
            1.20,
            1.20,
            new DateTime(2026, 5, 15, 14, 1, 0, DateTimeKind.Utc));
        store.Update(previousDayFirst);
        var previousDayImpliedSnapshot = store.Update(previousDaySecond);
        var previousDayCalculation = new OptionExposureCalculator().Calculate(
            previousDaySecond,
            ValuationTimeUtc,
            previousDayImpliedSnapshot);
        TestAssert.True(previousDayCalculation.HasFlowImpliedDealerExposure, "Prior-day snapshots build flow-implied dealer state");

        var currentDayFirst = CreateSnapshot(
            1,
            100,
            0.20,
            1.00,
            1.20,
            1.20,
            new DateTime(2026, 5, 16, 14, 0, 0, DateTimeKind.Utc));
        var currentDayImpliedSnapshot = store.Update(currentDayFirst);
        var calculation = new OptionExposureCalculator().Calculate(
            currentDayFirst,
            new DateTime(2026, 5, 16, 14, 0, 0, DateTimeKind.Utc),
            currentDayImpliedSnapshot);

        TestAssert.True(currentDayImpliedSnapshot.HasSeededBaseline, "New day's first snapshot seeds a fresh flow baseline");
        TestAssert.True(!currentDayImpliedSnapshot.HasFlowIntervals, "New day's first snapshot has no classified flow interval after reset");
        TestAssert.True(calculation.HasFlowImpliedDealerExposure, "New day's first snapshot has seeded flow-implied exposure after reset");
        TestAssert.Equal(1, calculation.FlowImpliedDealerSummary.ContractCount, "New day's first snapshot produces a seeded flow-implied summary");
    }

    public static void PersistsExposureHistoryDatabase()
    {
        var store = new OptionFlowImpliedDealerStore();
        var snapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10);
        var secondSnapshot = CreateSnapshot(20, 100, 0.22, 1.00, 1.20, 1.20);
        store.Update(snapshot);
        var impliedSnapshot = store.Update(secondSnapshot);
        var calculation = new OptionExposureCalculator().Calculate(secondSnapshot, ValuationTimeUtc, impliedSnapshot);
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.Save(directory, calculation, impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);

            var databasePath = OptionExposureHistorySqliteStore.GetDatabasePath(directory, calculation.SnapshotTimestampUtc);
            TestAssert.True(File.Exists(databasePath), "History store writes SQLite database");
            TestAssert.Equal(1, OptionExposureHistorySqliteStore.GetSnapshotCount(directory), "History store writes one exposure snapshot");

            OptionExposureHistorySqliteStore.Save(directory, calculation, impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);
            TestAssert.Equal(1, OptionExposureHistorySqliteStore.GetSnapshotCount(directory), "History store upserts duplicate exposure snapshot");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void LoadsRecentExposureHistoryAndLatestDealerState()
    {
        var store = new OptionFlowImpliedDealerStore();
        var snapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10);
        var secondSnapshot = CreateSnapshot(20, 100, 0.22, 1.00, 1.20, 1.20);
        store.Update(snapshot);
        var impliedSnapshot = store.Update(secondSnapshot);
        var calculation = new OptionExposureCalculator().Calculate(secondSnapshot, ValuationTimeUtc, impliedSnapshot);
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.Save(directory, calculation, impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);
            OptionExposureHistorySqliteStore.Save(directory, calculation, ValuationTimeUtc.AddMinutes(1.0), impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);

            var history = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.FlowImpliedDealer, ValuationTimeUtc.AddHours(-1.0), 10);
            TestAssert.Equal(2, history.Count, "History store loads recent exposure snapshots");
            TestAssert.Equal(calculation.FlowImpliedDealerSummary.VannaExposure, history[0].VannaExposure, "History store loads VEX for sparklines");

            var strikeSeries = OptionExposureHistorySqliteStore.LoadRecentStrikeSeries(directory, "TEST", OptionPositionModel.FlowImpliedDealer, OptionExposureMetric.Vanna, ValuationTimeUtc.AddHours(-1.0), 10);
            TestAssert.Equal(1, strikeSeries.Count, "History store loads per-strike exposure history");
            TestAssert.Equal(2, strikeSeries[0].Values.Count, "Per-strike exposure history includes recent snapshots");

            OptionExposureStrikeTrend strikeTrend;
            TestAssert.True(OptionExposureHistorySqliteStore.TryLoadStrikeTrend(directory, "TEST", OptionPositionModel.FlowImpliedDealer, OptionExposureMetric.Vanna, ValuationTimeUtc.AddHours(-1.0), out strikeTrend), "History store loads strike trend");
            TestAssert.Equal(100.0, strikeTrend.LatestTopStrike, "Strike trend loads latest top VEX strike");

            OptionImpliedDealerSnapshot loadedSnapshot;
            TestAssert.True(OptionExposureHistorySqliteStore.TryLoadLatestImpliedDealerSnapshot(directory, "TEST", OptionPositionModel.FlowImpliedDealer, ValuationTimeUtc, out loadedSnapshot), "History store loads latest persisted dealer state");
            TestAssert.True(loadedSnapshot.HasDealerState, "Loaded dealer snapshot has dealer state");

            var hydratedStore = new OptionFlowImpliedDealerStore();
            TestAssert.True(hydratedStore.Hydrate(loadedSnapshot), "Flow store hydrates persisted dealer state");
            var hydratedSnapshot = hydratedStore.CreateSnapshot();
            double dealerContracts;
            TestAssert.True(hydratedSnapshot.TryGetDealerContracts("TEST260522C00100000", out dealerContracts), "Hydrated dealer state contains contract");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void LoadsRecentExposureHistoryWithAbsoluteLevelTotals()
    {
        var calculation = new OptionExposureCalculation(
            "TEST",
            100.0,
            ValuationTimeUtc,
            new OptionExposureSummary(5.0, -5.0, -3.0, 2),
            new List<OptionStrikeExposure>
            {
                new OptionStrikeExposure(100.0, 10.0, -2.0, 1.0, 1L, 1),
                new OptionStrikeExposure(101.0, -5.0, -3.0, -4.0, 1L, 1)
            });
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.Save(directory, calculation, ValuationTimeUtc, null, null, OptionPositionModel.Naive);

            var history = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.Naive, ValuationTimeUtc.AddMinutes(-1.0), 10);
            TestAssert.Equal(1, history.Count, "History store loads one synthetic exposure snapshot");
            TestAssert.Equal(5.0, history[0].GammaExposure, "History store keeps signed total GEX");
            TestAssert.Equal(15.0, history[0].AbsoluteGammaExposure, "History store loads absolute per-level GEX total");
            TestAssert.Equal(5.0, history[0].AbsoluteVannaExposure, "History store loads absolute per-level VEX total");
            TestAssert.Equal(5.0, history[0].AbsoluteCharmExposure, "History store loads absolute per-level CEX total");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void LoadsStrikeHistoryAcrossDateShards()
    {
        var store = new OptionFlowImpliedDealerStore();
        var snapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10);
        var secondSnapshot = CreateSnapshot(20, 100, 0.22, 1.00, 1.20, 1.20);
        store.Update(snapshot);
        var impliedSnapshot = store.Update(secondSnapshot);
        var calculation = new OptionExposureCalculator().Calculate(secondSnapshot, ValuationTimeUtc, impliedSnapshot);
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));
        var beforeMidnightUtc = new DateTime(2026, 5, 15, 23, 59, 0, DateTimeKind.Utc);
        var afterMidnightUtc = beforeMidnightUtc.AddMinutes(2.0);

        try
        {
            OptionExposureHistorySqliteStore.Save(directory, calculation, beforeMidnightUtc, impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);
            OptionExposureHistorySqliteStore.Save(directory, calculation, afterMidnightUtc, impliedSnapshot, store.LastIntervals, OptionPositionModel.FlowImpliedDealer);

            TestAssert.True(File.Exists(OptionExposureHistorySqliteStore.GetDatabasePath(directory, beforeMidnightUtc)), "History store writes first date shard");
            TestAssert.True(File.Exists(OptionExposureHistorySqliteStore.GetDatabasePath(directory, afterMidnightUtc)), "History store writes second date shard");

            var history = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.FlowImpliedDealer, beforeMidnightUtc.AddMinutes(-1.0), 10);
            TestAssert.Equal(2, history.Count, "Recent exposure history loads across date shards");
            TestAssert.Equal(beforeMidnightUtc, history[0].TimestampUtc, "Recent exposure history keeps chronological order across date shards");
            TestAssert.Equal(afterMidnightUtc, history[1].TimestampUtc, "Recent exposure history includes latest date shard");

            var limitedHistory = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.FlowImpliedDealer, beforeMidnightUtc.AddMinutes(-1.0), 1);
            TestAssert.Equal(1, limitedHistory.Count, "Recent exposure history applies a global snapshot limit");
            TestAssert.Equal(afterMidnightUtc, limitedHistory[0].TimestampUtc, "Recent exposure history global limit keeps latest snapshot");

            OptionExposureStrikeTrend strikeTrend;
            TestAssert.True(OptionExposureHistorySqliteStore.TryLoadStrikeTrend(directory, "TEST", OptionPositionModel.FlowImpliedDealer, OptionExposureMetric.Vanna, beforeMidnightUtc.AddMinutes(-1.0), out strikeTrend), "Strike trend loads across date shards");
            TestAssert.Equal(2, strikeTrend.SnapshotCount, "Strike trend counts snapshots across date shards");

            var strikeSeries = OptionExposureHistorySqliteStore.LoadRecentStrikeSeries(directory, "TEST", OptionPositionModel.FlowImpliedDealer, OptionExposureMetric.Vanna, beforeMidnightUtc.AddMinutes(-1.0), 10);
            TestAssert.Equal(1, strikeSeries.Count, "Per-strike history loads across date shards");
            TestAssert.Equal(2, strikeSeries[0].Values.Count, "Per-strike history includes snapshots on both date shards");

            var limitedStrikeSeries = OptionExposureHistorySqliteStore.LoadRecentStrikeSeries(directory, "TEST", OptionPositionModel.FlowImpliedDealer, OptionExposureMetric.Vanna, beforeMidnightUtc.AddMinutes(-1.0), 1);
            TestAssert.Equal(1, limitedStrikeSeries.Count, "Limited per-strike history loads across date shards");
            TestAssert.Equal(1, limitedStrikeSeries[0].Values.Count, "Per-strike history applies a global snapshot limit");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void InitializesExposureHistoryDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.EnsureDatabase(directory);

            var databasePath = OptionExposureHistorySqliteStore.GetDatabasePath(directory, DateTime.UtcNow);
            TestAssert.True(File.Exists(databasePath), "History store initialization writes SQLite database");
            TestAssert.Equal(0, OptionExposureHistorySqliteStore.GetSnapshotCount(directory), "Initialized history database starts with no exposure snapshots");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void CountsEmptyExistingHistoryDatabaseAsZero()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(OptionExposureHistorySqliteStore.GetDatabasePath(directory), Array.Empty<byte>());

            TestAssert.Equal(0, OptionExposureHistorySqliteStore.GetSnapshotCount(directory), "History store counts an existing database without exposure schema as empty");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void PersistsEffectiveTimestampWhenSnapshotTimestampIsMissing()
    {
        var timestamp = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        var snapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10, timestamp);
        var store = new OptionFlowImpliedDealerStore();
        var impliedSnapshot = store.Update(snapshot);
        var calculation = new OptionExposureCalculator().Calculate(snapshot, ValuationTimeUtc, impliedSnapshot);
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.Save(
                directory,
                calculation,
                ValuationTimeUtc,
                impliedSnapshot,
                store.LastIntervals,
                OptionPositionModel.FlowImpliedDealer);
            OptionExposureHistorySqliteStore.Save(
                directory,
                calculation,
                ValuationTimeUtc.AddMinutes(1.0),
                impliedSnapshot,
                store.LastIntervals,
                OptionPositionModel.FlowImpliedDealer);

            TestAssert.Equal(2, OptionExposureHistorySqliteStore.GetSnapshotCount(directory), "History store uses effective snapshot timestamps when provider timestamps are missing");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    public static void SeparatesExposureHistoryByDataScope()
    {
        var firstSnapshot = CreateSnapshot(10, 100, 0.20, 1.00, 1.20, 1.10, ValuationTimeUtc);
        var secondSnapshot = CreateSnapshot(20, 120, 0.22, 1.00, 1.20, 1.20, ValuationTimeUtc.AddMinutes(1.0));
        var calculator = new OptionExposureCalculator();
        var firstCalculation = calculator.Calculate(firstSnapshot, ValuationTimeUtc);
        var secondCalculation = calculator.Calculate(secondSnapshot, ValuationTimeUtc.AddMinutes(1.0));
        var directory = Path.Combine(Path.GetTempPath(), "gvc-history-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            OptionExposureHistorySqliteStore.Save(directory, firstCalculation, ValuationTimeUtc, null, null, OptionPositionModel.Naive, true);
            OptionExposureHistorySqliteStore.Save(directory, secondCalculation, ValuationTimeUtc.AddMinutes(1.0), null, null, OptionPositionModel.Naive, true);
            OptionExposureHistorySqliteStore.Save(directory, firstCalculation, ValuationTimeUtc, null, null, OptionPositionModel.Naive, false);

            var zeroDteHistory = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.Naive, true, ValuationTimeUtc.AddMinutes(-1.0), 10);
            var allHistory = OptionExposureHistorySqliteStore.LoadRecentSnapshots(directory, "TEST", OptionPositionModel.Naive, false, ValuationTimeUtc.AddMinutes(-1.0), 10);
            TestAssert.Equal(2, zeroDteHistory.Count, "0DTE exposure history uses its own snapshot bucket");
            TestAssert.Equal(1, allHistory.Count, "ALL exposure history does not mix 0DTE snapshots");

            OptionExposureStrikeTrend zeroDteTrend;
            OptionExposureStrikeTrend allTrend;
            TestAssert.True(OptionExposureHistorySqliteStore.TryLoadStrikeTrend(directory, "TEST", OptionPositionModel.Naive, true, OptionExposureMetric.Gamma, ValuationTimeUtc.AddMinutes(-1.0), out zeroDteTrend), "0DTE trend sees two scoped snapshots");
            TestAssert.False(OptionExposureHistorySqliteStore.TryLoadStrikeTrend(directory, "TEST", OptionPositionModel.Naive, false, OptionExposureMetric.Gamma, ValuationTimeUtc.AddMinutes(-1.0), out allTrend), "ALL trend waits for its own second scoped snapshot");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static DateTime ValuationTimeUtc
    {
        get { return new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc); }
    }

    private static OptionChainSnapshot CreateSnapshot(long volume, long openInterest, double impliedVolatility, double bid, double ask, double lastPrice)
    {
        return CreateSnapshot(volume, openInterest, impliedVolatility, bid, ask, lastPrice, ValuationTimeUtc.AddSeconds(volume));
    }

    private static OptionChainSnapshot CreateSnapshot(long volume, long openInterest, double impliedVolatility, double bid, double ask, double lastPrice, DateTime timestampUtc)
    {
        return new OptionChainSnapshot(
            "TEST",
            100.0,
            timestampUtc,
            new List<OptionContractData>
            {
                new OptionContractData
                {
                    Symbol = "TEST260522C00100000",
                    Right = OptionRight.Call,
                    Strike = 100.0,
                    OpenInterest = openInterest,
                    Volume = volume,
                    ImpliedVolatility = impliedVolatility,
                    Gamma = 0.05,
                    Bid = bid,
                    Ask = ask,
                    LastPrice = lastPrice,
                    ExpirationUnixSeconds = ToUnixSeconds(new DateTime(2026, 5, 22, 20, 0, 0, DateTimeKind.Utc))
                }
            });
    }

    private static long ToUnixSeconds(DateTime timestampUtc)
    {
        return (long)(timestampUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message + ": expected " + typeof(TException).Name + ", actual " + exception.GetType().Name, exception);
        }

        throw new InvalidOperationException("Expected exception: " + message);
    }
}
