using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    public static IList<OptionExposureHistoryPoint> LoadRecentSnapshots(
        string directory,
        string symbol,
        OptionPositionModel model,
        DateTime sinceUtc,
        int maxCount)
    {
        return LoadRecentSnapshots(directory, symbol, model.ToString(), sinceUtc, maxCount);
    }

    public static IList<OptionExposureHistoryPoint> LoadRecentSnapshots(
        string directory,
        string symbol,
        OptionPositionModel model,
        bool useZeroDteOptions,
        DateTime sinceUtc,
        int maxCount)
    {
        return LoadRecentSnapshots(directory, symbol, GetScopedModelKey(model, useZeroDteOptions), sinceUtc, maxCount);
    }

    private static IList<OptionExposureHistoryPoint> LoadRecentSnapshots(
        string directory,
        string symbol,
        string modelKey,
        DateTime sinceUtc,
        int maxCount)
    {
        var points = new List<OptionExposureHistoryPoint>();
        if (string.IsNullOrWhiteSpace(symbol) || maxCount <= 0)
            return points.AsReadOnly();

        var databaseDirectory = GetDatabaseDirectory(directory);
        if (string.IsNullOrWhiteSpace(databaseDirectory) || !Directory.Exists(databaseDirectory))
            return points.AsReadOnly();

        var snapshotReferences = LoadRecentSnapshotWindow(databaseDirectory, symbol, modelKey, sinceUtc, maxCount);
        if (snapshotReferences.Count == 0)
            return points.AsReadOnly();

        var groups = BuildStrikeSnapshotGroups(snapshotReferences);
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            LoadHistoryPointsForSnapshotGroup(groups[groupIndex], points);

        points.Sort((left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));
        return points.AsReadOnly();
    }

    public static bool TryLoadStrikeTrend(
        string directory,
        string symbol,
        OptionPositionModel model,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        out OptionExposureStrikeTrend trend)
    {
        return TryLoadStrikeTrend(directory, symbol, model.ToString(), metric, sinceUtc, out trend);
    }

    public static bool TryLoadStrikeTrend(
        string directory,
        string symbol,
        OptionPositionModel model,
        bool useZeroDteOptions,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        out OptionExposureStrikeTrend trend)
    {
        return TryLoadStrikeTrend(directory, symbol, GetScopedModelKey(model, useZeroDteOptions), metric, sinceUtc, out trend);
    }

    private static bool TryLoadStrikeTrend(
        string directory,
        string symbol,
        string modelKey,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        out OptionExposureStrikeTrend trend)
    {
        trend = null;
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        var metricColumn = GetExposureLevelMetricColumn(metric);
        if (string.IsNullOrWhiteSpace(metricColumn))
            return false;

        var databaseDirectory = GetDatabaseDirectory(directory);
        if (string.IsNullOrWhiteSpace(databaseDirectory) || !Directory.Exists(databaseDirectory))
            return false;

        var snapshotReferences = LoadRecentSnapshotReferences(databaseDirectory, symbol, modelKey, sinceUtc, int.MaxValue);
        if (snapshotReferences.Count < 2)
            return false;

        snapshotReferences.Sort((left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));
        var firstSnapshot = snapshotReferences[0];
        var latestSnapshot = snapshotReferences[snapshotReferences.Count - 1];
        if (firstSnapshot.SnapshotId <= 0L || latestSnapshot.SnapshotId <= 0L
            || (string.Equals(firstSnapshot.DatabasePath, latestSnapshot.DatabasePath, StringComparison.OrdinalIgnoreCase) && firstSnapshot.SnapshotId == latestSnapshot.SnapshotId))
        {
            return false;
        }

        double firstTopStrike;
        double firstWeightedStrike;
        double latestTopStrike;
        double latestWeightedStrike;
        if (!TryLoadStrikeStats(firstSnapshot.DatabasePath, firstSnapshot.SnapshotId, metricColumn, out firstTopStrike, out firstWeightedStrike)
            || !TryLoadStrikeStats(latestSnapshot.DatabasePath, latestSnapshot.SnapshotId, metricColumn, out latestTopStrike, out latestWeightedStrike))
        {
            return false;
        }

        trend = new OptionExposureStrikeTrend(metric, snapshotReferences.Count, firstTopStrike, latestTopStrike, firstWeightedStrike, latestWeightedStrike);
        return true;
    }

    private static void LoadHistoryPointsForSnapshotGroup(StrikeSnapshotGroup group, List<OptionExposureHistoryPoint> points)
    {
        if (group == null || group.SnapshotIds.Count == 0 || points == null)
            return;

        using (var connection = CreateConnection(group.DatabasePath))
        {
            connection.Open();
            EnsureSchema(connection);
            using (var command = connection.CreateCommand())
            {
                var parameterNames = new List<string>();
                for (var index = 0; index < group.SnapshotIds.Count; index++)
                    parameterNames.Add("$snapshot_id_" + index.ToString(CultureInfo.InvariantCulture));

                command.CommandText = @"
    SELECT s.timestamp_utc,
           s.total_gex,
           s.total_vex,
           s.total_cex,
           COALESCE(SUM(ABS(l.gex)), ABS(s.total_gex)),
           COALESCE(SUM(ABS(l.vex)), ABS(s.total_vex)),
           COALESCE(SUM(ABS(l.cex)), ABS(s.total_cex))
    FROM exposure_snapshots s
    LEFT JOIN exposure_levels l ON l.snapshot_id = s.snapshot_id
    WHERE s.snapshot_id IN (" + string.Join(", ", parameterNames.ToArray()) + @")
    GROUP BY s.snapshot_id, s.timestamp_utc, s.total_gex, s.total_vex, s.total_cex
    ORDER BY s.timestamp_utc ASC;";
                for (var index = 0; index < group.SnapshotIds.Count; index++)
                    AddParameter(command, parameterNames[index], group.SnapshotIds[index]);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var timestampUtc = ParseDateTime(reader.GetString(0));
                        if (timestampUtc == DateTime.MinValue)
                            continue;

                        points.Add(new OptionExposureHistoryPoint(
                            timestampUtc,
                            reader.GetDouble(1),
                            reader.GetDouble(2),
                            reader.GetDouble(3),
                            reader.GetDouble(4),
                            reader.GetDouble(5),
                            reader.GetDouble(6)));
                    }
                }
            }
        }
    }

    private static bool TryLoadStrikeStats(string databasePath, long snapshotId, string metricColumn, out double topStrike, out double weightedStrike)
    {
        topStrike = 0.0;
        weightedStrike = 0.0;
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            return false;

        using (var connection = CreateConnection(databasePath))
        {
            connection.Open();
            EnsureSchema(connection);
            return TryLoadStrikeStats(connection, snapshotId, metricColumn, out topStrike, out weightedStrike);
        }
    }

    private static bool TryLoadStrikeStats(
        SqliteConnectionHandle connection,
        long snapshotId,
        string metricColumn,
        out double topStrike,
        out double weightedStrike)
    {
        topStrike = 0.0;
        weightedStrike = 0.0;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
    SELECT strike, " + metricColumn + @"
    FROM exposure_levels
    WHERE snapshot_id = $snapshot_id;";
            AddParameter(command, "$snapshot_id", snapshotId);

            var topMagnitude = 0.0;
            var weightedSum = 0.0;
            var totalWeight = 0.0;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var strike = reader.GetDouble(0);
                    var value = reader.GetDouble(1);
                    var magnitude = Math.Abs(value);
                    if (magnitude <= 0.0000001)
                        continue;

                    if (magnitude > topMagnitude)
                    {
                        topMagnitude = magnitude;
                        topStrike = strike;
                    }

                    weightedSum += strike * magnitude;
                    totalWeight += magnitude;
                }
            }

            if (totalWeight <= 0.0000001)
                return false;

            weightedStrike = weightedSum / totalWeight;
            return true;
        }
    }

    public static bool TryLoadLatestImpliedDealerSnapshot(
        string directory,
        string symbol,
        OptionPositionModel model,
        DateTime dateUtc,
        out OptionImpliedDealerSnapshot snapshot)
    {
        return TryLoadLatestImpliedDealerSnapshot(directory, symbol, model.ToString(), dateUtc, out snapshot);
    }

    public static bool TryLoadLatestImpliedDealerSnapshot(
        string directory,
        string symbol,
        OptionPositionModel model,
        bool useZeroDteOptions,
        DateTime dateUtc,
        out OptionImpliedDealerSnapshot snapshot)
    {
        return TryLoadLatestImpliedDealerSnapshot(directory, symbol, GetScopedModelKey(model, useZeroDteOptions), dateUtc, out snapshot);
    }

    private static bool TryLoadLatestImpliedDealerSnapshot(
        string directory,
        string symbol,
        string modelKey,
        DateTime dateUtc,
        out OptionImpliedDealerSnapshot snapshot)
    {
        snapshot = null;
        var databasePath = GetDatabasePath(directory, dateUtc);
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath) || string.IsNullOrWhiteSpace(symbol))
            return false;

        var dayStartUtc = NormalizeUtc(dateUtc).Date;
        var dayEndUtc = dayStartUtc.AddDays(1.0);
        long snapshotId = 0L;
        DateTime timestampUtc = DateTime.MinValue;
        var hasSeededBaseline = false;
        var hasFlowIntervals = false;
        var states = new Dictionary<string, OptionImpliedDealerState>(StringComparer.Ordinal);

        using (var connection = CreateConnection(databasePath))
        {
            connection.Open();
            EnsureSchema(connection);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
    SELECT snapshot_id, timestamp_utc, has_seeded_baseline, has_flow_intervals
    FROM exposure_snapshots
    WHERE underlying_symbol = $underlying_symbol
      AND model = $model
      AND timestamp_utc >= $day_start_utc
      AND timestamp_utc < $day_end_utc
    ORDER BY timestamp_utc DESC
    LIMIT 1;";
                AddParameter(command, "$underlying_symbol", Clean(symbol));
                AddParameter(command, "$model", modelKey);
                AddParameter(command, "$day_start_utc", FormatDateTime(dayStartUtc));
                AddParameter(command, "$day_end_utc", FormatDateTime(dayEndUtc));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return false;

                    snapshotId = reader.GetInt64(0);
                    timestampUtc = ParseDateTime(reader.GetString(1));
                    hasSeededBaseline = reader.GetInt64(2) != 0L;
                    hasFlowIntervals = reader.GetInt64(3) != 0L;
                }
            }

            if (snapshotId <= 0L || timestampUtc == DateTime.MinValue)
                return false;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
    SELECT contract_symbol, dealer_contracts, confidence
    FROM dealer_states
    WHERE snapshot_id = $snapshot_id;";
                AddParameter(command, "$snapshot_id", snapshotId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var contractSymbol = reader.GetString(0);
                        if (string.IsNullOrWhiteSpace(contractSymbol))
                            continue;

                        states[contractSymbol] = new OptionImpliedDealerState(
                            contractSymbol,
                            reader.GetDouble(1),
                            reader.GetDouble(2));
                    }
                }
            }
        }

        if (states.Count == 0)
            return false;

        snapshot = new OptionImpliedDealerSnapshot(timestampUtc, hasSeededBaseline, hasFlowIntervals, states);
        return true;
    }
}
