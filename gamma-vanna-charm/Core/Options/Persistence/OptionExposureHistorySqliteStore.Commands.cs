using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    private static void AddSnapshotParameters(
        SqliteCommandHandle command,
        string timestampUtc,
        string symbol,
        double underlyingPrice,
        string model,
        OptionExposureSummary summary,
        bool hasSeededBaseline,
        bool hasFlowIntervals)
    {
        AddParameter(command, "$timestamp_utc", timestampUtc);
        AddParameter(command, "$underlying_symbol", symbol);
        AddParameter(command, "$underlying_price", underlyingPrice);
        AddParameter(command, "$model", model);
        AddParameter(command, "$total_gex", summary != null ? summary.GammaExposure : 0.0);
        AddParameter(command, "$total_vex", summary != null ? summary.VannaExposure : 0.0);
        AddParameter(command, "$total_cex", summary != null ? summary.CharmExposure : 0.0);
        AddParameter(command, "$contract_count", summary != null ? summary.ContractCount : 0);
        AddParameter(command, "$has_seeded_baseline", hasSeededBaseline ? 1 : 0);
        AddParameter(command, "$has_flow_intervals", hasFlowIntervals ? 1 : 0);
    }

    private static void AddParameter(SqliteCommandHandle command, string name, object value)
    {
        command.AddParameter(name, value ?? DBNull.Value);
    }

    private static void Execute(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, string commandText)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }
    }

    private static void Execute(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, string commandText, long snapshotId)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = commandText;
            AddParameter(command, "$snapshot_id", snapshotId);
            command.ExecuteNonQuery();
        }
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string FormatDateTime(DateTime timestampUtc)
    {
        var value = NormalizeUtc(timestampUtc);
        if (value == DateTime.MinValue)
            return string.Empty;

        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDateTime(string timestampText)
    {
        if (string.IsNullOrWhiteSpace(timestampText))
            return DateTime.MinValue;

        DateTime parsed;
        if (DateTime.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            return NormalizeUtc(parsed);

        return DateTime.MinValue;
    }

    private static DateTime NormalizeUtc(DateTime timestampUtc)
    {
        if (timestampUtc == DateTime.MinValue)
            return timestampUtc;
        if (timestampUtc.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);
        if (timestampUtc.Kind == DateTimeKind.Local)
            return timestampUtc.ToUniversalTime();

        return timestampUtc;
    }
}
