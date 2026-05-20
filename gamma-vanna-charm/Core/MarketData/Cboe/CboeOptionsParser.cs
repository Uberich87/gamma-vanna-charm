using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GammaVannaCharm.Core.Exposure;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.MarketData.Cboe;

public static class CboeOptionsParser
{
    private const string DataFieldName = "data";
    private const string OptionsFieldName = "options";
    private static readonly string[] TimestampFieldNames = { "timestamp", "time", "last_update", "lastUpdate" };
    private static readonly string[] CurrentPriceFieldNames = { "current_price", "currentPrice", "last", "price" };
    private static readonly string[] OptionFieldNames = { "option", "option_symbol", "optionSymbol", "symbol" };
    private static readonly string[] OpenInterestFieldNames = { "open_interest", "openInterest", "oi" };
    private static readonly string[] VolumeFieldNames = { "volume", "vol" };
    private static readonly string[] ImpliedVolatilityFieldNames = { "iv", "implied_volatility", "impliedVolatility" };
    private static readonly string[] GammaFieldNames = { "gamma" };
    private static readonly string[] BidFieldNames = { "bid" };
    private static readonly string[] AskFieldNames = { "ask" };
    private static readonly string[] LastTradePriceFieldNames = { "last_trade_price", "lastTradePrice", "last_price", "lastPrice", "last" };

    private static readonly Regex OptionSymbolRegex = new Regex(
        "^(.*?)(\\d{6})([CP])(\\d{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Lazy<TimeZoneInfo> s_usEasternTimeZone = new Lazy<TimeZoneInfo>(() =>
    {
        TimeZoneInfo timeZone;
        if (TryFindTimeZone("Eastern Standard Time", out timeZone)
            || TryFindTimeZone("America/New_York", out timeZone))
        {
            return timeZone;
        }
        return null;
    });

    public static bool TryParseOptionChainSnapshot(string underlyingSymbol, string json, bool zeroDteOnly, out OptionChainSnapshot snapshot)
    {
        snapshot = null;

        if (string.IsNullOrWhiteSpace(underlyingSymbol) || string.IsNullOrWhiteSpace(json))
            return false;

        JsonDocument document;
        if (!TryParseJsonObject(json, out document))
            return false;

        using (document)
        {
            var root = document.RootElement;
            JsonElement data;
            var hasData = TryGetObjectField(root, DataFieldName, out data);

            double underlyingPrice;
            if (!TryReadNumberField(root, CurrentPriceFieldNames, out underlyingPrice)
                && !(hasData && TryReadNumberField(data, CurrentPriceFieldNames, out underlyingPrice)))
            {
                return false;
            }
            if (underlyingPrice <= 0.0)
                return false;

            List<JsonElement> optionRows;
            if (!TryReadOptionRows(root, hasData, data, out optionRows) || optionRows.Count == 0)
                return false;

            var snapshotTimestampUtc = ResolveSnapshotTimestampUtc(root, hasData, data);
            var zeroDteDate = ResolveZeroDteDate(optionRows, snapshotTimestampUtc);
            var contracts = new List<OptionContractData>();
            for (var index = 0; index < optionRows.Count; index++)
            {
                var optionRow = optionRows[index];
                if (optionRow.ValueKind != JsonValueKind.Object)
                    continue;

                string optionSymbol;
                if (!TryReadStringField(optionRow, OptionFieldNames, out optionSymbol))
                    continue;

                OptionRight right;
                double strike;
                long expirationUnixSeconds;
                if (!TryParseOptionSymbol(optionSymbol, out right, out strike, out expirationUnixSeconds))
                    continue;

                if (zeroDteOnly && UnixSecondsToUtc(expirationUnixSeconds).Date != zeroDteDate)
                    continue;

                var openInterest = ReadLongField(optionRow, OpenInterestFieldNames);
                var volume = ReadLongField(optionRow, VolumeFieldNames);
                var impliedVolatility = ReadDoubleField(optionRow, ImpliedVolatilityFieldNames);
                var gamma = ReadDoubleField(optionRow, GammaFieldNames);
                var bid = ReadDoubleField(optionRow, BidFieldNames);
                var ask = ReadDoubleField(optionRow, AskFieldNames);
                var lastPrice = ReadDoubleField(optionRow, LastTradePriceFieldNames);

                if (impliedVolatility <= 0.0)
                    continue;

                var contract = new OptionContractData
                {
                    Symbol = optionSymbol,
                    Right = right,
                    Strike = strike,
                    OpenInterest = openInterest,
                    Volume = volume,
                    ImpliedVolatility = impliedVolatility,
                    Gamma = gamma,
                    Bid = bid,
                    Ask = ask,
                    LastPrice = lastPrice,
                    ExpirationUnixSeconds = expirationUnixSeconds
                };
                if (!TryValidateContract(contract))
                    continue;

                contracts.Add(contract);
            }

            if (contracts.Count == 0)
                return false;

            snapshot = new OptionChainSnapshot(underlyingSymbol, underlyingPrice, snapshotTimestampUtc, contracts);
            return true;
        }
    }

    private static bool TryParseJsonObject(string json, out JsonDocument document)
    {
        document = null;
        try
        {
            document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return true;

            document.Dispose();
            document = null;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryValidateContract(OptionContractData contract)
    {
        try
        {
            contract.Validate();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryGetObjectField(JsonElement fields, string fieldName, out JsonElement value)
    {
        value = default(JsonElement);
        return fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(fieldName, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryReadOptionRows(JsonElement root, bool hasData, JsonElement data, out List<JsonElement> optionRows)
    {
        optionRows = new List<JsonElement>();

        AddOptionRows(root, optionRows);
        if (optionRows.Count == 0 && hasData)
            AddOptionRows(data, optionRows);

        return optionRows.Count > 0;
    }

    private static void AddOptionRows(JsonElement fields, List<JsonElement> optionRows)
    {
        if (fields.ValueKind != JsonValueKind.Object || optionRows == null)
            return;

        JsonElement options;
        if (!fields.TryGetProperty(OptionsFieldName, out options))
            return;

        AddOptionRowsFromElement(options, optionRows);
    }

    private static void AddOptionRowsFromElement(JsonElement options, List<JsonElement> optionRows)
    {
        if (optionRows == null)
            return;

        if (options.ValueKind == JsonValueKind.Array)
        {
            foreach (var optionRow in options.EnumerateArray())
            {
                if (optionRow.ValueKind == JsonValueKind.Object)
                    optionRows.Add(optionRow);
            }
            return;
        }

        if (options.ValueKind != JsonValueKind.Object)
            return;

        foreach (var optionGroup in options.EnumerateObject())
            AddOptionRowsFromElement(optionGroup.Value, optionRows);
    }

    private static DateTime ResolveSnapshotTimestampUtc(JsonElement root, bool hasData, JsonElement data)
    {
        string timestampText;
        if (TryReadStringField(root, TimestampFieldNames, out timestampText))
        {
            var resolvedTimestamp = ResolveSnapshotTimestampUtc(timestampText);
            if (resolvedTimestamp != default(DateTime) && resolvedTimestamp != DateTime.MinValue)
                return resolvedTimestamp;
            if (!hasData)
                return resolvedTimestamp;
        }

        if (hasData && TryReadStringField(data, TimestampFieldNames, out timestampText))
        {
            return ResolveSnapshotTimestampUtc(timestampText);
        }

        return DateTime.UtcNow;
    }

    private static DateTime ResolveSnapshotTimestampUtc(string timestampText)
    {
        if (string.IsNullOrWhiteSpace(timestampText))
            return DateTime.UtcNow;

        timestampText = timestampText.Trim();
        DateTimeOffset timestampOffset;
        if (DateTimeOffset.TryParse(
            timestampText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestampOffset))
        {
            return timestampOffset.UtcDateTime;
        }

        DateTime parsedTimestamp;
        if (DateTime.TryParseExact(
                timestampText,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTimestamp)
            || DateTime.TryParseExact(
                timestampText,
                "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTimestamp)
            || DateTime.TryParseExact(
                timestampText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTimestamp))
        {
            // CBOE delayed_quotes/options payloads used by CboeOptionsClient publish UTC
            // response/update timestamps, not a guaranteed delayed-market as-of time, but omit
            // a timezone designator in current responses and fixtures.
            return DateTime.SpecifyKind(parsedTimestamp, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    private static DateTime ResolveZeroDteDate(IList<JsonElement> optionRows, DateTime snapshotTimestampUtc)
    {
        var chainDate = snapshotTimestampUtc.Date;
        var zeroDteDate = DateTime.MaxValue.Date;
        for (var index = 0; index < optionRows.Count; index++)
        {
            var optionRow = optionRows[index];
            if (optionRow.ValueKind != JsonValueKind.Object)
                continue;

            string optionSymbol;
            if (!TryReadStringField(optionRow, OptionFieldNames, out optionSymbol))
                continue;

            OptionRight right;
            double strike;
            long expirationUnixSeconds;
            if (!TryParseOptionSymbol(optionSymbol, out right, out strike, out expirationUnixSeconds))
                continue;

            var expirationDate = UnixSecondsToUtc(expirationUnixSeconds).Date;
            if (expirationDate >= chainDate && expirationDate < zeroDteDate)
                zeroDteDate = expirationDate;
        }

        return zeroDteDate == DateTime.MaxValue.Date ? chainDate : zeroDteDate;
    }

    private static bool TryParseOptionSymbol(string optionSymbol, out OptionRight right, out double strike, out long expirationUnixSeconds)
    {
        right = OptionRight.Call;
        strike = 0.0;
        expirationUnixSeconds = 0L;

        var match = OptionSymbolRegex.Match(optionSymbol ?? string.Empty);
        if (!match.Success || match.Groups.Count < 5)
            return false;

        var optionRoot = match.Groups[1].Value;
        right = match.Groups[3].Value == "P" ? OptionRight.Put : OptionRight.Call;
        long rawStrike;
        if (!long.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rawStrike))
            return false;
        strike = rawStrike / 1000.0;

        var expirationDateText = match.Groups[2].Value;
        DateTime expirationDate;
        if (!DateTime.TryParseExact(expirationDateText, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out expirationDate))
            return false;

        expirationUnixSeconds = GetCboeOptionExpirationUnixSeconds(optionRoot, expirationDate.Date);
        return strike > 0.0 && expirationUnixSeconds > 0L;
    }

    private static long GetCboeOptionExpirationUnixSeconds(string optionRoot, DateTime expirationDate)
    {
        // CBOE symbols carry only the expiration date. Treating that date as midnight UTC makes
        // live 0DTE contracts look expired and forces the Greeks path into its minimum-time clamp.
        // Use the root-specific listed close where CBOE symbols provide enough information.
        var normalizedRoot = (optionRoot ?? string.Empty).Trim().ToUpperInvariant();
        var closeDate = expirationDate.Date;
        var closeHour = 16.0;

        if (normalizedRoot == "SPY" || normalizedRoot == "QQQ")
        {
            closeHour = 16.25;
        }
        else if (normalizedRoot == "SPX" && IsStandardMonthlyExpiration(expirationDate.Date))
        {
            closeDate = GetPreviousWeekday(expirationDate.Date);
            closeHour = 17.0;
        }

        return ToUsEasternUnixSeconds(closeDate, closeHour);
    }

    private static long ToUsEasternUnixSeconds(DateTime localDate, double hour)
    {
        var easternTime = DateTime.SpecifyKind(localDate.Date.AddHours(hour), DateTimeKind.Unspecified);
        var easternTimeZone = s_usEasternTimeZone.Value;
        if (easternTimeZone != null)
            return ToUnixSeconds(TimeZoneInfo.ConvertTimeToUtc(easternTime, easternTimeZone));

        var utcTime = easternTime - GetFallbackUsEasternUtcOffset(localDate.Date);
        return ToUnixSeconds(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc));
    }

    private static bool IsStandardMonthlyExpiration(DateTime expirationDate)
    {
        return expirationDate.DayOfWeek == DayOfWeek.Friday
            && expirationDate.Day >= 15
            && expirationDate.Day <= 21;
    }

    private static DateTime GetPreviousWeekday(DateTime localDate)
    {
        var previous = localDate.Date.AddDays(-1.0);
        while (previous.DayOfWeek == DayOfWeek.Saturday || previous.DayOfWeek == DayOfWeek.Sunday)
            previous = previous.AddDays(-1.0);

        return previous;
    }


    private static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = null;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return timeZone != null;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static TimeSpan GetFallbackUsEasternUtcOffset(DateTime localDate)
    {
        return IsFallbackUsEasternDaylightSavingDate(localDate.Date)
            ? TimeSpan.FromHours(-4.0)
            : TimeSpan.FromHours(-5.0);
    }

    private static bool IsFallbackUsEasternDaylightSavingDate(DateTime localDate)
    {
        var daylightStart = GetNthSunday(localDate.Year, 3, 2);
        var daylightEnd = GetNthSunday(localDate.Year, 11, 1);
        return localDate >= daylightStart && localDate < daylightEnd;
    }

    private static DateTime GetNthSunday(int year, int month, int occurrence)
    {
        var firstDayOfMonth = new DateTime(year, month, 1);
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)firstDayOfMonth.DayOfWeek + 7) % 7;
        return firstDayOfMonth.AddDays(daysUntilSunday + ((occurrence - 1) * 7));
    }

    private static double ReadDoubleField(JsonElement fields, string[] fieldNames)
    {
        double value;
        return TryReadNumberField(fields, fieldNames, out value) ? value : 0.0;
    }

    private static long ReadLongField(JsonElement fields, string[] fieldNames)
    {
        var value = ReadDoubleField(fields, fieldNames);
        if (value <= 0.0)
            return 0L;

        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static bool TryReadStringField(JsonElement fields, string fieldName, out string value)
    {
        return TryReadStringField(fields, new[] { fieldName }, out value);
    }

    private static bool TryReadStringField(JsonElement fields, string[] fieldNames, out string value)
    {
        value = null;
        if (fields.ValueKind != JsonValueKind.Object || fieldNames == null)
            return false;

        for (var index = 0; index < fieldNames.Length; index++)
        {
            JsonElement rawValue;
            if (string.IsNullOrWhiteSpace(fieldNames[index])
                || !fields.TryGetProperty(fieldNames[index], out rawValue)
                || rawValue.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            value = rawValue.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return true;
        }

        return false;
    }

    private static bool TryReadNumberField(JsonElement fields, string fieldName, out double value)
    {
        return TryReadNumberField(fields, new[] { fieldName }, out value);
    }

    private static bool TryReadNumberField(JsonElement fields, string[] fieldNames, out double value)
    {
        value = 0.0;
        if (fields.ValueKind != JsonValueKind.Object || fieldNames == null)
            return false;

        for (var index = 0; index < fieldNames.Length; index++)
        {
            JsonElement rawValue;
            if (!string.IsNullOrWhiteSpace(fieldNames[index])
                && fields.TryGetProperty(fieldNames[index], out rawValue)
                && TryReadNumber(rawValue, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadNumber(JsonElement rawValue, out double value)
    {
        value = 0.0;
        if (rawValue.ValueKind == JsonValueKind.Number)
            return rawValue.TryGetDouble(out value) && IsFinite(value);

        if (rawValue.ValueKind != JsonValueKind.String)
            return false;

        var text = rawValue.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;

        return IsFinite(value);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static DateTime UnixSecondsToUtc(long unixSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
    }

    private static long ToUnixSeconds(DateTime timestampUtc)
    {
        return (long)(timestampUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}
