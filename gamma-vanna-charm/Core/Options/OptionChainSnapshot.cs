using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionChainSnapshot
{
    public OptionChainSnapshot(string underlyingSymbol, double underlyingPrice, DateTime timestampUtc, IList<OptionContractData> contracts)
    {
        if (string.IsNullOrWhiteSpace(underlyingSymbol))
            throw new ArgumentException("Underlying symbol is required.", nameof(underlyingSymbol));
        if (double.IsNaN(underlyingPrice) || double.IsInfinity(underlyingPrice) || underlyingPrice < 0.0)
            throw new ArgumentOutOfRangeException(nameof(underlyingPrice), "Underlying price must be finite and non-negative.");
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));

        UnderlyingSymbol = underlyingSymbol;
        UnderlyingPrice = underlyingPrice;
        TimestampUtc = timestampUtc;
        Contracts = new List<OptionContractData>(contracts ?? new List<OptionContractData>()).AsReadOnly();
    }

    public string UnderlyingSymbol { get; private set; }

    public double UnderlyingPrice { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public IReadOnlyList<OptionContractData> Contracts { get; private set; }
}
