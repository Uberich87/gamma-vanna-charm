using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GammaVannaCharm.Core.Options;

public static partial class OptionExposureHistorySqliteStore
{
    private sealed class SqliteConnectionHandle : IDisposable
    {
        private readonly object _connection;
        private readonly Type _connectionType;

        public SqliteConnectionHandle(object connection)
        {
            _connection = connection;
            _connectionType = connection.GetType();
        }

        public void Open()
        {
            _connectionType.GetMethod("Open", Type.EmptyTypes).Invoke(_connection, null);
        }

        public SqliteTransactionHandle BeginTransaction()
        {
            return new SqliteTransactionHandle(_connectionType.GetMethod("BeginTransaction", Type.EmptyTypes).Invoke(_connection, null));
        }

        public SqliteCommandHandle CreateCommand()
        {
            return new SqliteCommandHandle(_connectionType.GetMethod("CreateCommand", Type.EmptyTypes).Invoke(_connection, null));
        }

        public void Dispose()
        {
            var disposable = _connection as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private sealed class SqliteTransactionHandle : IDisposable
    {
        private readonly object _transaction;
        private readonly Type _transactionType;

        public SqliteTransactionHandle(object transaction)
        {
            _transaction = transaction;
            _transactionType = transaction.GetType();
        }

        public object Inner
        {
            get { return _transaction; }
        }

        public void Commit()
        {
            _transactionType.GetMethod("Commit", Type.EmptyTypes).Invoke(_transaction, null);
        }

        public void Dispose()
        {
            var disposable = _transaction as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private sealed class SqliteCommandHandle : IDisposable
    {
        private readonly object _command;
        private readonly Type _commandType;

        public SqliteCommandHandle(object command)
        {
            _command = command;
            _commandType = command.GetType();
        }

        public SqliteTransactionHandle Transaction
        {
            set
            {
                var transaction = value != null ? value.Inner : null;
                foreach (var property in _commandType.GetProperties())
                {
                    if (property.Name == "Transaction" && property.CanWrite && (transaction == null || property.PropertyType.IsInstanceOfType(transaction)))
                    {
                        property.SetValue(_command, transaction, null);
                        return;
                    }
                }

                throw new InvalidOperationException("SQLite provider command does not expose a compatible Transaction property.");
            }
        }

        public string CommandText
        {
            set { _commandType.GetProperty("CommandText").SetValue(_command, value, null); }
        }

        public void AddParameter(string name, object value)
        {
            foreach (var property in _commandType.GetProperties())
            {
                if (property.Name != "Parameters" || !property.CanRead)
                    continue;

                var parameters = property.GetValue(_command, null);
                if (parameters == null)
                    continue;

                var addWithValue = parameters.GetType().GetMethod("AddWithValue", new[] { typeof(string), typeof(object) });
                if (addWithValue == null)
                    continue;

                addWithValue.Invoke(parameters, new[] { name, value });
                return;
            }

            throw new InvalidOperationException("SQLite provider command parameters do not support AddWithValue.");
        }

        public int ExecuteNonQuery()
        {
            return Convert.ToInt32(_commandType.GetMethod("ExecuteNonQuery", Type.EmptyTypes).Invoke(_command, null), CultureInfo.InvariantCulture);
        }

        public object ExecuteScalar()
        {
            return _commandType.GetMethod("ExecuteScalar", Type.EmptyTypes).Invoke(_command, null);
        }

        public SqliteDataReaderHandle ExecuteReader()
        {
            return new SqliteDataReaderHandle(_commandType.GetMethod("ExecuteReader", Type.EmptyTypes).Invoke(_command, null));
        }

        public void Dispose()
        {
            var disposable = _command as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private sealed class SqliteDataReaderHandle : IDisposable
    {
        private readonly object _reader;
        private readonly Type _readerType;

        public SqliteDataReaderHandle(object reader)
        {
            _reader = reader;
            _readerType = reader.GetType();
        }

        public bool Read()
        {
            return Convert.ToBoolean(_readerType.GetMethod("Read", Type.EmptyTypes).Invoke(_reader, null), CultureInfo.InvariantCulture);
        }

        public bool IsDBNull(int ordinal)
        {
            return Convert.ToBoolean(_readerType.GetMethod("IsDBNull", new[] { typeof(int) }).Invoke(_reader, new object[] { ordinal }), CultureInfo.InvariantCulture);
        }

        public string GetString(int ordinal)
        {
            return Convert.ToString(GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        public double GetDouble(int ordinal)
        {
            return Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        public long GetInt64(int ordinal)
        {
            return Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private object GetValue(int ordinal)
        {
            return _readerType.GetMethod("GetValue", new[] { typeof(int) }).Invoke(_reader, new object[] { ordinal });
        }

        public void Dispose()
        {
            var disposable = _reader as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }
}
