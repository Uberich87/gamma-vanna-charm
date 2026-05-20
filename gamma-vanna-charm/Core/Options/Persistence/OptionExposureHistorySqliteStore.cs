using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    public const string DefaultDatabaseDirectory = @"C:\gvc";
    private const int DefaultDatabaseRetentionDays = 14;
    private const string SqliteProviderDisplayName = "System.Data.SQLite";
    private const string SqliteConnectionTypeName = "System.Data.SQLite.SQLiteConnection, System.Data.SQLite";
    private const string SqliteConnectionStringBuilderTypeName = "System.Data.SQLite.SQLiteConnectionStringBuilder, System.Data.SQLite";

    public static string GetDatabasePath(string directory)
    {
        return GetDatabasePath(directory, DateTime.UtcNow);
    }

    public static string GetDatabasePath(string directory, DateTime timestampUtc)
    {
        return Path.Combine(GetDatabaseDirectory(directory), GetDatabaseFileName(timestampUtc));
    }

    public static void EnsureDatabase(string directory)
    {
        var databaseDirectory = GetDatabaseDirectory(directory);
        Directory.CreateDirectory(databaseDirectory);
        CleanupOldDatabases(databaseDirectory, DefaultDatabaseRetentionDays);
        var databasePath = GetDatabasePath(databaseDirectory, DateTime.UtcNow);
        using (var connection = CreateConnection(databasePath))
        {
            connection.Open();
            EnsureSchema(connection);
        }
    }

    public static void Save(
        string directory,
        OptionExposureCalculation calculation,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        IReadOnlyList<OptionFlowInterval> flowIntervals,
        OptionPositionModel model)
    {
        Save(
            directory,
            calculation,
            calculation != null ? calculation.SnapshotTimestampUtc : DateTime.MinValue,
            impliedDealerSnapshot,
            flowIntervals,
            model);
    }

    public static void Save(
        string directory,
        OptionExposureCalculation calculation,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        IReadOnlyList<OptionFlowInterval> flowIntervals,
        OptionPositionModel model,
        bool useZeroDteOptions)
    {
        Save(
            directory,
            calculation,
            calculation != null ? calculation.SnapshotTimestampUtc : DateTime.MinValue,
            impliedDealerSnapshot,
            flowIntervals,
            model,
            useZeroDteOptions);
    }

    public static void Save(
        string directory,
        OptionExposureCalculation calculation,
        DateTime effectiveSnapshotTimestampUtc,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        IReadOnlyList<OptionFlowInterval> flowIntervals,
        OptionPositionModel model)
    {
        SaveInternal(directory, calculation, effectiveSnapshotTimestampUtc, impliedDealerSnapshot, flowIntervals, model, model.ToString());
    }

    public static void Save(
        string directory,
        OptionExposureCalculation calculation,
        DateTime effectiveSnapshotTimestampUtc,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        IReadOnlyList<OptionFlowInterval> flowIntervals,
        OptionPositionModel model,
        bool useZeroDteOptions)
    {
        SaveInternal(directory, calculation, effectiveSnapshotTimestampUtc, impliedDealerSnapshot, flowIntervals, model, GetScopedModelKey(model, useZeroDteOptions));
    }

    private static void SaveInternal(
        string directory,
        OptionExposureCalculation calculation,
        DateTime effectiveSnapshotTimestampUtc,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        IReadOnlyList<OptionFlowInterval> flowIntervals,
        OptionPositionModel model,
        string modelKey)
    {
        if (calculation == null)
            return;

        var databaseDirectory = GetDatabaseDirectory(directory);
        Directory.CreateDirectory(databaseDirectory);
        var candidateTimestamp = effectiveSnapshotTimestampUtc == DateTime.MinValue ? calculation.SnapshotTimestampUtc : effectiveSnapshotTimestampUtc;
        if (candidateTimestamp == DateTime.MinValue)
            candidateTimestamp = DateTime.UtcNow;
        var timestampUtc = NormalizeUtc(candidateTimestamp);
        var databasePath = GetDatabasePath(databaseDirectory, timestampUtc);
        using (var connection = CreateConnection(databasePath))
        {
            connection.Open();
            EnsureSchema(connection);

            using (var transaction = connection.BeginTransaction())
            {
                var symbol = Clean(calculation.UnderlyingSymbol);
                var summary = calculation.GetSummary(model);
                var hasSeededBaseline = impliedDealerSnapshot != null && impliedDealerSnapshot.HasSeededBaseline;
                var hasFlowIntervals = flowIntervals != null && flowIntervals.Count > 0;
                var snapshotId = UpsertSnapshot(connection, transaction, timestampUtc, symbol, calculation.UnderlyingPrice, modelKey, summary, hasSeededBaseline, hasFlowIntervals);

                DeleteSnapshotChildren(connection, transaction, snapshotId);
                SaveLevels(connection, transaction, snapshotId, calculation.GetStrikeExposures(model));
                SaveDealerStates(connection, transaction, snapshotId, impliedDealerSnapshot);
                SaveFlowIntervals(connection, transaction, snapshotId, flowIntervals);

                transaction.Commit();
            }
        }
    }

    public static int GetSnapshotCount(string directory)
    {
        var databaseDirectory = GetDatabaseDirectory(directory);
        if (string.IsNullOrWhiteSpace(databaseDirectory) || !Directory.Exists(databaseDirectory))
            return 0;

        var count = 0;
        foreach (var databasePath in Directory.GetFiles(databaseDirectory, "*.sqlite"))
        {
            using (var connection = CreateConnection(databasePath))
            {
                connection.Open();
                EnsureSchema(connection);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM exposure_snapshots;";
                    count += Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }
        }

        return count;
    }

    public static IList<OptionExposureStrikeHistorySeries> LoadRecentStrikeSeries(
        string directory,
        string symbol,
        OptionPositionModel model,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        return LoadRecentStrikeSeries(directory, symbol, model.ToString(), metric, sinceUtc, maxSnapshotCount);
    }

    public static IList<OptionExposureStrikeHistorySeries> LoadRecentStrikeSeries(
        string directory,
        string symbol,
        OptionPositionModel model,
        bool useZeroDteOptions,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        return LoadRecentStrikeSeries(directory, symbol, GetScopedModelKey(model, useZeroDteOptions), metric, sinceUtc, maxSnapshotCount);
    }

    private static IList<OptionExposureStrikeHistorySeries> LoadRecentStrikeSeries(
        string directory,
        string symbol,
        string modelKey,
        OptionExposureMetric metric,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        var seriesByStrike = new Dictionary<double, List<double>>();
        if (string.IsNullOrWhiteSpace(symbol) || maxSnapshotCount <= 0)
            return new List<OptionExposureStrikeHistorySeries>().AsReadOnly();

        var metricColumn = GetExposureLevelMetricColumn(metric);
        if (string.IsNullOrWhiteSpace(metricColumn))
            return new List<OptionExposureStrikeHistorySeries>().AsReadOnly();

        var databaseDirectory = GetDatabaseDirectory(directory);
        if (string.IsNullOrWhiteSpace(databaseDirectory) || !Directory.Exists(databaseDirectory))
            return new List<OptionExposureStrikeHistorySeries>().AsReadOnly();

        var snapshotReferences = LoadRecentSnapshotWindow(databaseDirectory, symbol, modelKey, sinceUtc, maxSnapshotCount);
        if (snapshotReferences.Count == 0)
            return new List<OptionExposureStrikeHistorySeries>().AsReadOnly();

        var groups = BuildStrikeSnapshotGroups(snapshotReferences);
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            LoadStrikeSeriesForSnapshotGroup(groups[groupIndex], metricColumn, seriesByStrike);

        var series = new List<OptionExposureStrikeHistorySeries>();
        foreach (var pair in seriesByStrike)
            series.Add(new OptionExposureStrikeHistorySeries(pair.Key, pair.Value));

        series.Sort((left, right) => left.Strike.CompareTo(right.Strike));
        return series.AsReadOnly();
    }

    private static List<StrikeSnapshotReference> LoadRecentSnapshotWindow(
        string databaseDirectory,
        string symbol,
        OptionPositionModel model,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        return LoadRecentSnapshotWindow(databaseDirectory, symbol, model.ToString(), sinceUtc, maxSnapshotCount);
    }

    private static List<StrikeSnapshotReference> LoadRecentSnapshotWindow(
        string databaseDirectory,
        string symbol,
        string modelKey,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        var snapshots = LoadRecentSnapshotReferences(databaseDirectory, symbol, modelKey, sinceUtc, maxSnapshotCount);
        snapshots.Sort((left, right) => right.TimestampUtc.CompareTo(left.TimestampUtc));
        if (snapshots.Count > maxSnapshotCount)
            snapshots.RemoveRange(maxSnapshotCount, snapshots.Count - maxSnapshotCount);
        snapshots.Sort((left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));
        return snapshots;
    }

    private static List<StrikeSnapshotReference> LoadRecentSnapshotReferences(
        string databaseDirectory,
        string symbol,
        OptionPositionModel model,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        return LoadRecentSnapshotReferences(databaseDirectory, symbol, model.ToString(), sinceUtc, maxSnapshotCount);
    }

    private static List<StrikeSnapshotReference> LoadRecentSnapshotReferences(
        string databaseDirectory,
        string symbol,
        string modelKey,
        DateTime sinceUtc,
        int maxSnapshotCount)
    {
        var snapshots = new List<StrikeSnapshotReference>();
        var sinceTimestampUtc = NormalizeUtc(sinceUtc);
        var sinceDateUtc = sinceTimestampUtc == DateTime.MinValue ? DateTime.MinValue.Date : sinceTimestampUtc.Date;
        var databasePaths = Directory.GetFiles(databaseDirectory, "*.sqlite");
        Array.Sort(databasePaths, StringComparer.OrdinalIgnoreCase);
        for (var pathIndex = 0; pathIndex < databasePaths.Length; pathIndex++)
        {
            var databasePath = databasePaths[pathIndex];
            DateTime databaseDate;
            if (!TryParseDatabaseDate(databasePath, out databaseDate) || databaseDate < sinceDateUtc)
                continue;

            using (var connection = CreateConnection(databasePath))
            {
                connection.Open();
                EnsureSchema(connection);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
    SELECT snapshot_id, timestamp_utc
    FROM exposure_snapshots
    WHERE underlying_symbol = $underlying_symbol
      AND model = $model
      AND timestamp_utc >= $since_utc
    ORDER BY timestamp_utc DESC
    LIMIT $limit;";
                    AddParameter(command, "$underlying_symbol", Clean(symbol));
                    AddParameter(command, "$model", modelKey);
                    AddParameter(command, "$since_utc", FormatDateTime(sinceTimestampUtc));
                    AddParameter(command, "$limit", maxSnapshotCount);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var timestampUtc = ParseDateTime(reader.GetString(1));
                            if (timestampUtc == DateTime.MinValue)
                                continue;

                            snapshots.Add(new StrikeSnapshotReference(databasePath, reader.GetInt64(0), timestampUtc));
                        }
                    }
                }
            }
        }

        return snapshots;
    }

    private static List<StrikeSnapshotGroup> BuildStrikeSnapshotGroups(IList<StrikeSnapshotReference> snapshotReferences)
    {
        var groupsByPath = new Dictionary<string, StrikeSnapshotGroup>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < snapshotReferences.Count; index++)
        {
            var reference = snapshotReferences[index];
            StrikeSnapshotGroup group;
            if (!groupsByPath.TryGetValue(reference.DatabasePath, out group))
            {
                group = new StrikeSnapshotGroup(reference.DatabasePath, reference.TimestampUtc);
                groupsByPath[reference.DatabasePath] = group;
            }

            group.SnapshotIds.Add(reference.SnapshotId);
            if (reference.TimestampUtc < group.FirstTimestampUtc)
                group.FirstTimestampUtc = reference.TimestampUtc;
        }

        var groups = new List<StrikeSnapshotGroup>(groupsByPath.Values);
        groups.Sort((left, right) => left.FirstTimestampUtc.CompareTo(right.FirstTimestampUtc));
        return groups;
    }

    private static void LoadStrikeSeriesForSnapshotGroup(StrikeSnapshotGroup group, string metricColumn, Dictionary<double, List<double>> seriesByStrike)
    {
        if (group == null || group.SnapshotIds.Count == 0)
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
    SELECT l.strike, " + metricColumn + @"
    FROM exposure_levels l
    INNER JOIN exposure_snapshots s ON s.snapshot_id = l.snapshot_id
    WHERE l.snapshot_id IN (" + string.Join(", ", parameterNames.ToArray()) + @")
    ORDER BY s.timestamp_utc ASC, l.strike ASC;";
                for (var index = 0; index < group.SnapshotIds.Count; index++)
                    AddParameter(command, parameterNames[index], group.SnapshotIds[index]);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var strike = reader.GetDouble(0);
                        List<double> values;
                        if (!seriesByStrike.TryGetValue(strike, out values))
                        {
                            values = new List<double>();
                            seriesByStrike[strike] = values;
                        }

                        values.Add(reader.GetDouble(1));
                    }
                }
            }
        }
    }

    private sealed class StrikeSnapshotReference
    {
        public StrikeSnapshotReference(string databasePath, long snapshotId, DateTime timestampUtc)
        {
            DatabasePath = databasePath;
            SnapshotId = snapshotId;
            TimestampUtc = timestampUtc;
        }

        public string DatabasePath { get; private set; }

        public long SnapshotId { get; private set; }

        public DateTime TimestampUtc { get; private set; }
    }

    private sealed class StrikeSnapshotGroup
    {
        public StrikeSnapshotGroup(string databasePath, DateTime firstTimestampUtc)
        {
            DatabasePath = databasePath;
            FirstTimestampUtc = firstTimestampUtc;
            SnapshotIds = new List<long>();
        }

        public string DatabasePath { get; private set; }

        public DateTime FirstTimestampUtc { get; set; }

        public List<long> SnapshotIds { get; private set; }
    }

    private static string GetScopedModelKey(OptionPositionModel model, bool useZeroDteOptions)
    {
        return model.ToString() + "|" + (useZeroDteOptions ? "0DTE" : "ALL");
    }

    private static string GetExposureLevelMetricColumn(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
            return "vex";
        if (metric == OptionExposureMetric.Charm)
            return "cex";

        return "gex";
    }

    private static SqliteConnectionHandle CreateConnection(string databasePath)
    {
        var sqliteConnectionType = Type.GetType(SqliteConnectionTypeName, false);
        if (sqliteConnectionType == null)
            throw new InvalidOperationException("SQLite provider assembly " + SqliteProviderDisplayName + " is not available. Add the System.Data.SQLite assembly reference to enable exposure history persistence.");

        var connection = Activator.CreateInstance(sqliteConnectionType, CreateConnectionString(databasePath));
        if (connection == null)
            throw new InvalidOperationException("SQLite provider assembly " + SqliteProviderDisplayName + " did not create a database connection.");

        return new SqliteConnectionHandle(connection);
    }

    private static string CreateConnectionString(string databasePath)
    {
        var sqliteConnectionStringBuilderType = Type.GetType(SqliteConnectionStringBuilderTypeName, false);
        if (sqliteConnectionStringBuilderType != null)
        {
            var builder = Activator.CreateInstance(sqliteConnectionStringBuilderType);
            var dataSourceProperty = sqliteConnectionStringBuilderType.GetProperty("DataSource");
            if (builder != null && dataSourceProperty != null)
            {
                dataSourceProperty.SetValue(builder, databasePath, null);
                return builder.ToString();
            }
        }

        return "Data Source=\"" + databasePath.Replace("\"", "\"\"") + "\"";
    }

    private static string GetDatabaseDirectory(string directory)
    {
        return string.IsNullOrWhiteSpace(directory) ? DefaultDatabaseDirectory : directory;
    }

    private static string GetDatabaseFileName(DateTime timestampUtc)
    {
        return NormalizeUtc(timestampUtc == DateTime.MinValue ? DateTime.UtcNow : timestampUtc).ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".sqlite";
    }

    private static void CleanupOldDatabases(string databaseDirectory, int retentionDays)
    {
        if (string.IsNullOrWhiteSpace(databaseDirectory) || !Directory.Exists(databaseDirectory) || retentionDays <= 0)
            return;

        var cutoffDate = DateTime.UtcNow.Date.AddDays(-retentionDays);
        foreach (var databasePath in Directory.GetFiles(databaseDirectory, "*.sqlite"))
        {
            DateTime databaseDate;
            if (!TryParseDatabaseDate(databasePath, out databaseDate) || databaseDate >= cutoffDate)
                continue;

            TryDeleteFile(databasePath);
            TryDeleteFile(databasePath + "-wal");
            TryDeleteFile(databasePath + "-shm");
        }
    }

    private static bool TryParseDatabaseDate(string databasePath, out DateTime databaseDate)
    {
        databaseDate = DateTime.MinValue;
        var fileName = Path.GetFileNameWithoutExtension(databasePath);
        DateTime parsed;
        if (!DateTime.TryParseExact(fileName, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return false;

        databaseDate = parsed.Date;
        return true;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
