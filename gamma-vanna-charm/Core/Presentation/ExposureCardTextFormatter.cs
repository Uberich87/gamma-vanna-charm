using System;
using System.Globalization;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureCardTextFormatter
{
    public static string FormatCompactExposure(double value)
    {
        var absValue = Math.Abs(value);
        if (absValue >= 1000000000.0)
            return (value / 1000000000.0).ToString("0.##B", CultureInfo.InvariantCulture);
        if (absValue >= 1000000.0)
            return (value / 1000000.0).ToString("0.##M", CultureInfo.InvariantCulture);
        if (absValue >= 1000.0)
            return (value / 1000.0).ToString("0.##K", CultureInfo.InvariantCulture);

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string FormatSignedCompactExposure(double value)
    {
        var text = FormatCompactExposure(value);
        return value > 0.0 ? "+" + text : text;
    }

    public static string FormatTime(DateTime timestampUtc)
    {
        if (timestampUtc == DateTime.MinValue)
            return "--:--:--";

        return NormalizeUtc(timestampUtc).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatDateTime(DateTime timestampUtc)
    {
        if (timestampUtc == DateTime.MinValue)
            return "--/-- --:--:--";

        return NormalizeUtc(timestampUtc).ToLocalTime().ToString("MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatUtcDateTime(DateTime timestampUtc)
    {
        if (timestampUtc == DateTime.MinValue)
            return "--/-- --:--:--";

        return NormalizeUtc(timestampUtc).ToString("MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static DateTime NormalizeUtc(DateTime timestampUtc)
    {
        if (timestampUtc.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);
        if (timestampUtc.Kind == DateTimeKind.Local)
            return timestampUtc.ToUniversalTime();

        return timestampUtc;
    }

    public static string GetStatusBadgeText(string statusText)
    {
        if (statusText != null && statusText.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ERROR";
        if (statusText != null && statusText.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) >= 0)
            return "WAIT";

        return "INFO";
    }

    public static string CleanStatusText(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
            return string.Empty;

        var text = statusText;
        const string gvcPrefix = "GVC: ";
        const string optionsErrorPrefix = "GVC: options data error: ";
        const string legacyCboeErrorPrefix = "GVC CBOE API error: ";
        if (text.StartsWith(optionsErrorPrefix, StringComparison.Ordinal))
        {
            var errorText = text.Substring(optionsErrorPrefix.Length).Trim();
            text = errorText.Length == 0 ? "Options data error" : "Options data error: " + errorText;
        }
        else if (text.StartsWith(legacyCboeErrorPrefix, StringComparison.Ordinal))
        {
            var errorText = text.Substring(legacyCboeErrorPrefix.Length).Trim();
            text = errorText.Length == 0 ? "CBOE API error" : "CBOE API error: " + errorText;
        }
        else if (text.StartsWith(gvcPrefix, StringComparison.Ordinal))
        {
            text = text.Substring(gvcPrefix.Length).Trim();
        }

        if (text.StartsWith("waiting for options data", StringComparison.OrdinalIgnoreCase))
            return "Checking options data...";
        if (text.StartsWith("waiting for CBOE options data", StringComparison.OrdinalIgnoreCase))
            return "Checking CBOE options data...";

        return text;
    }

    public static string FormatHeaderSymbol(string optionsSymbol, double underlyingPrice)
    {
        var symbol = optionsSymbol ?? string.Empty;
        if (symbol.StartsWith("_", StringComparison.Ordinal))
            symbol = symbol.Substring(1);

        var price = underlyingPrice.ToString("0.00", CultureInfo.InvariantCulture);
        return symbol.Length == 0 ? price : symbol + " " + price;
    }
}
