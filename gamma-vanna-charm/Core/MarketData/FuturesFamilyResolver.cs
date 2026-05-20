using System;

namespace GammaVannaCharm.Core.MarketData;

public static class FuturesFamilyResolver
{
    public static FuturesFamily Resolve(string instrumentName)
    {
        if (string.IsNullOrWhiteSpace(instrumentName))
            return FuturesFamily.Unknown;

        var normalized = instrumentName.Trim().ToUpperInvariant();
        if (normalized == "ES" || normalized == "MES" || normalized.StartsWith("ES ", StringComparison.Ordinal) || normalized.StartsWith("MES ", StringComparison.Ordinal))
            return FuturesFamily.SP500;

        if (normalized == "NQ" || normalized == "MNQ" || normalized.StartsWith("NQ ", StringComparison.Ordinal) || normalized.StartsWith("MNQ ", StringComparison.Ordinal))
            return FuturesFamily.Nasdaq100;

        return FuturesFamily.Unknown;
    }
}
