using System;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionContractData
{
    public string Symbol { get; set; }

    public OptionRight Right { get; set; }

    public double Strike { get; set; }

    public long OpenInterest { get; set; }

    public long Volume { get; set; }

    public double ImpliedVolatility { get; set; }

    public double Gamma { get; set; }

    public double Bid { get; set; }

    public double Ask { get; set; }

    public double LastPrice { get; set; }

    public long ExpirationUnixSeconds { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            throw new InvalidOperationException("Symbol is required.");
        if (Right != OptionRight.Call && Right != OptionRight.Put)
            throw new InvalidOperationException("Option right must be Call or Put.");
        if (double.IsNaN(Strike) || double.IsInfinity(Strike) || Strike <= 0.0)
            throw new InvalidOperationException("Strike must be finite and greater than zero.");
        if (OpenInterest < 0L)
            throw new InvalidOperationException("Open interest must be non-negative.");
        if (Volume < 0L)
            throw new InvalidOperationException("Volume must be non-negative.");
        if (double.IsNaN(ImpliedVolatility) || double.IsInfinity(ImpliedVolatility) || ImpliedVolatility < 0.0 || ImpliedVolatility > 10.0)
            throw new InvalidOperationException("Implied volatility must be between 0 and 10.");
        if (Bid < 0.0 || double.IsNaN(Bid) || double.IsInfinity(Bid))
            throw new InvalidOperationException("Bid must be finite and non-negative.");
        if (Ask < 0.0 || double.IsNaN(Ask) || double.IsInfinity(Ask))
            throw new InvalidOperationException("Ask must be finite and non-negative.");
        if (Ask < Bid)
            throw new InvalidOperationException("Ask must be greater than or equal to Bid.");
        if (LastPrice < 0.0 || double.IsNaN(LastPrice) || double.IsInfinity(LastPrice))
            throw new InvalidOperationException("Last price must be finite and non-negative.");
        if (ExpirationUnixSeconds < 0L)
            throw new InvalidOperationException("Expiration timestamp must be non-negative.");
    }
}
