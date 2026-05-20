using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    private static void EnsureSchema(SqliteConnectionHandle connection)
    {
        Execute(connection, null, "PRAGMA busy_timeout = 10000;");
        Execute(connection, null, "PRAGMA journal_mode = WAL;");
        Execute(connection, null, "PRAGMA foreign_keys = ON;");
        Execute(connection, null, @"
    CREATE TABLE IF NOT EXISTS schema_info (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
    ); ");
        Execute(connection, null, @"
    INSERT OR REPLACE INTO schema_info (key, value)
    VALUES ('schema_version', '1');");
        Execute(connection, null, @"
    CREATE TABLE IF NOT EXISTS exposure_snapshots (
    snapshot_id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    underlying_symbol TEXT NOT NULL,
    underlying_price REAL NOT NULL,
    model TEXT NOT NULL,
    total_gex REAL NOT NULL,
    total_vex REAL NOT NULL,
    total_cex REAL NOT NULL,
    contract_count INTEGER NOT NULL,
    has_seeded_baseline INTEGER NOT NULL,
    has_flow_intervals INTEGER NOT NULL,
    UNIQUE(timestamp_utc, underlying_symbol, model)
    ); ");
        Execute(connection, null, @"
    CREATE TABLE IF NOT EXISTS exposure_levels (
    level_id INTEGER PRIMARY KEY AUTOINCREMENT,
    snapshot_id INTEGER NOT NULL,
    strike REAL NOT NULL,
    gex REAL NOT NULL,
    vex REAL NOT NULL,
    cex REAL NOT NULL,
    open_interest INTEGER NOT NULL,
    contract_count INTEGER NOT NULL,
    FOREIGN KEY(snapshot_id) REFERENCES exposure_snapshots(snapshot_id) ON DELETE CASCADE
    ); ");
        Execute(connection, null, @"
    CREATE TABLE IF NOT EXISTS dealer_states (
    state_id INTEGER PRIMARY KEY AUTOINCREMENT,
    snapshot_id INTEGER NOT NULL,
    contract_symbol TEXT NOT NULL,
    dealer_contracts REAL NOT NULL,
    confidence REAL NOT NULL,
    FOREIGN KEY(snapshot_id) REFERENCES exposure_snapshots(snapshot_id) ON DELETE CASCADE
    ); ");
        Execute(connection, null, @"
    CREATE TABLE IF NOT EXISTS flow_intervals (
    interval_id INTEGER PRIMARY KEY AUTOINCREMENT,
    snapshot_id INTEGER NOT NULL,
    contract_symbol TEXT NOT NULL,
    start_utc TEXT NOT NULL,
    end_utc TEXT NOT NULL,
    volume_delta INTEGER NOT NULL,
    dealer_contracts_delta REAL NOT NULL,
    confidence REAL NOT NULL,
    FOREIGN KEY(snapshot_id) REFERENCES exposure_snapshots(snapshot_id) ON DELETE CASCADE
    ); ");
        Execute(connection, null, "CREATE INDEX IF NOT EXISTS ix_exposure_snapshots_symbol_time ON exposure_snapshots(underlying_symbol, timestamp_utc);");
        Execute(connection, null, "CREATE INDEX IF NOT EXISTS ix_exposure_levels_snapshot ON exposure_levels(snapshot_id);");
        Execute(connection, null, "CREATE INDEX IF NOT EXISTS ix_dealer_states_snapshot ON dealer_states(snapshot_id);");
        Execute(connection, null, "CREATE INDEX IF NOT EXISTS ix_flow_intervals_snapshot ON flow_intervals(snapshot_id);");
    }
}
