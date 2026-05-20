using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    private static long UpsertSnapshot(
        SqliteConnectionHandle connection,
        SqliteTransactionHandle transaction,
        DateTime timestampUtc,
        string symbol,
        double underlyingPrice,
        string modelKey,
        OptionExposureSummary summary,
        bool hasSeededBaseline,
        bool hasFlowIntervals)
    {
        var timestampText = FormatDateTime(timestampUtc);
        var modelText = Clean(modelKey);

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = @"
    INSERT OR IGNORE INTO exposure_snapshots (
    timestamp_utc, underlying_symbol, underlying_price, model,
    total_gex, total_vex, total_cex, contract_count,
    has_seeded_baseline, has_flow_intervals)
    VALUES (
    $timestamp_utc, $underlying_symbol, $underlying_price, $model,
    $total_gex, $total_vex, $total_cex, $contract_count,
    $has_seeded_baseline, $has_flow_intervals);";
            AddSnapshotParameters(insert, timestampText, symbol, underlyingPrice, modelText, summary, hasSeededBaseline, hasFlowIntervals);
            insert.ExecuteNonQuery();
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
    UPDATE exposure_snapshots
    SET underlying_price = $underlying_price,
    total_gex = $total_gex,
    total_vex = $total_vex,
    total_cex = $total_cex,
    contract_count = $contract_count,
    has_seeded_baseline = $has_seeded_baseline,
    has_flow_intervals = $has_flow_intervals
    WHERE timestamp_utc = $timestamp_utc
      AND underlying_symbol = $underlying_symbol
      AND model = $model;";
            AddSnapshotParameters(update, timestampText, symbol, underlyingPrice, modelText, summary, hasSeededBaseline, hasFlowIntervals);
            update.ExecuteNonQuery();
        }

        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = @"
    SELECT snapshot_id
    FROM exposure_snapshots
    WHERE timestamp_utc = $timestamp_utc
      AND underlying_symbol = $underlying_symbol
      AND model = $model;";
            AddParameter(select, "$timestamp_utc", timestampText);
            AddParameter(select, "$underlying_symbol", symbol);
            AddParameter(select, "$model", modelText);
            return Convert.ToInt64(select.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    private static void SaveLevels(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, long snapshotId, IList<OptionStrikeExposure> levels)
    {
        if (levels == null)
            return;

        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
    INSERT INTO exposure_levels (
    snapshot_id, strike, gex, vex, cex, open_interest, contract_count)
    VALUES (
    $snapshot_id, $strike, $gex, $vex, $cex, $open_interest, $contract_count);";
                AddParameter(command, "$snapshot_id", snapshotId);
                AddParameter(command, "$strike", level.Strike);
                AddParameter(command, "$gex", level.GammaExposure);
                AddParameter(command, "$vex", level.VannaExposure);
                AddParameter(command, "$cex", level.CharmExposure);
                AddParameter(command, "$open_interest", level.OpenInterest);
                AddParameter(command, "$contract_count", level.ContractCount);
                command.ExecuteNonQuery();
            }
        }
    }

    private static void SaveDealerStates(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, long snapshotId, OptionImpliedDealerSnapshot impliedDealerSnapshot)
    {
        if (impliedDealerSnapshot == null || !impliedDealerSnapshot.HasDealerState)
            return;

        foreach (var state in impliedDealerSnapshot.States)
        {
            if (state == null)
                continue;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
    INSERT INTO dealer_states (
    snapshot_id, contract_symbol, dealer_contracts, confidence)
    VALUES (
    $snapshot_id, $contract_symbol, $dealer_contracts, $confidence);";
                AddParameter(command, "$snapshot_id", snapshotId);
                AddParameter(command, "$contract_symbol", Clean(state.Symbol));
                AddParameter(command, "$dealer_contracts", state.DealerContracts);
                AddParameter(command, "$confidence", state.Confidence);
                command.ExecuteNonQuery();
            }
        }
    }

    private static void SaveFlowIntervals(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, long snapshotId, IReadOnlyList<OptionFlowInterval> flowIntervals)
    {
        if (flowIntervals == null || flowIntervals.Count == 0)
            return;

        for (var index = 0; index < flowIntervals.Count; index++)
        {
            var interval = flowIntervals[index];
            if (interval == null)
                continue;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
    INSERT INTO flow_intervals (
    snapshot_id, contract_symbol, start_utc, end_utc,
    volume_delta, dealer_contracts_delta, confidence)
    VALUES (
    $snapshot_id, $contract_symbol, $start_utc, $end_utc,
    $volume_delta, $dealer_contracts_delta, $confidence);";
                AddParameter(command, "$snapshot_id", snapshotId);
                AddParameter(command, "$contract_symbol", Clean(interval.Symbol));
                AddParameter(command, "$start_utc", FormatDateTime(interval.StartTimestampUtc));
                AddParameter(command, "$end_utc", FormatDateTime(interval.EndTimestampUtc));
                AddParameter(command, "$volume_delta", interval.VolumeDelta);
                AddParameter(command, "$dealer_contracts_delta", interval.DealerContractsDelta);
                AddParameter(command, "$confidence", interval.Confidence);
                command.ExecuteNonQuery();
            }
        }
    }

    private static void DeleteSnapshotChildren(SqliteConnectionHandle connection, SqliteTransactionHandle transaction, long snapshotId)
    {
        Execute(connection, transaction, "DELETE FROM exposure_levels WHERE snapshot_id = $snapshot_id;", snapshotId);
        Execute(connection, transaction, "DELETE FROM dealer_states WHERE snapshot_id = $snapshot_id;", snapshotId);
        Execute(connection, transaction, "DELETE FROM flow_intervals WHERE snapshot_id = $snapshot_id;", snapshotId);
    }
}
