using System;

namespace GammaVannaCharm.Core.MarketData;

public sealed class SpotPriceQuote
{
    public SpotPriceQuote(string symbol, double price, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (double.IsNaN(price) || double.IsInfinity(price) || price < 0.0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be finite and non-negative.");
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));

        Symbol = symbol;
        Price = price;
        TimestampUtc = timestampUtc;
    }

    public string Symbol { get; private set; }

    public double Price { get; private set; }

    public DateTime TimestampUtc { get; private set; }
}
