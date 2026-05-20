using System;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionFlowInterval
{
    public OptionFlowInterval(
        string symbol,
        DateTime startTimestampUtc,
        DateTime endTimestampUtc,
        long volumeDelta,
        double dealerContractsDelta,
        double confidence)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (startTimestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Start timestamp must be UTC.", nameof(startTimestampUtc));
        if (endTimestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("End timestamp must be UTC.", nameof(endTimestampUtc));
        if (startTimestampUtc > endTimestampUtc)
            throw new ArgumentException("Start timestamp cannot be after end timestamp.", nameof(startTimestampUtc));
        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        Symbol = symbol;
        StartTimestampUtc = startTimestampUtc;
        EndTimestampUtc = endTimestampUtc;
        VolumeDelta = volumeDelta;
        DealerContractsDelta = dealerContractsDelta;
        Confidence = confidence;
    }

    public string Symbol { get; private set; }

    public DateTime StartTimestampUtc { get; private set; }

    public DateTime EndTimestampUtc { get; private set; }

    public long VolumeDelta { get; private set; }

    public double DealerContractsDelta { get; private set; }

    public double Confidence { get; private set; }
}
