using System;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionImpliedDealerState
{
    public OptionImpliedDealerState(string symbol, double dealerContracts, double confidence)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null, empty, or whitespace.", nameof(symbol));
        if (double.IsNaN(dealerContracts) || double.IsInfinity(dealerContracts))
            throw new ArgumentOutOfRangeException(nameof(dealerContracts), "DealerContracts must be a finite number.");
        if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be a finite number between 0 and 1.");

        Symbol = symbol;
        DealerContracts = dealerContracts;
        Confidence = confidence;
    }

    public string Symbol { get; private set; }

    public double DealerContracts { get; private set; }

    public double Confidence { get; private set; }
}
