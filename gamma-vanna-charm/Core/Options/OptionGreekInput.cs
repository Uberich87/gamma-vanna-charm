using System;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionGreekInput
{
    public OptionRight Right { get; set; }

    public double UnderlyingPrice { get; set; }

    public double Strike { get; set; }

    public double ImpliedVolatility { get; set; }

    public DateTime ValuationTimeUtc { get; set; }

    public DateTime ExpirationTimeUtc { get; set; }

    public double RiskFreeRate { get; set; }

    public double DividendYield { get; set; }

    public void Validate()
    {
        if (Right != OptionRight.Call && Right != OptionRight.Put)
            throw new InvalidOperationException("Option right must be Call or Put.");
        if (double.IsNaN(UnderlyingPrice) || double.IsInfinity(UnderlyingPrice) || UnderlyingPrice <= 0.0)
            throw new InvalidOperationException("Underlying price must be finite and greater than zero.");
        if (double.IsNaN(Strike) || double.IsInfinity(Strike) || Strike <= 0.0)
            throw new InvalidOperationException("Strike must be finite and greater than zero.");
        if (double.IsNaN(ImpliedVolatility) || double.IsInfinity(ImpliedVolatility) || ImpliedVolatility <= 0.0 || ImpliedVolatility > 10.0)
            throw new InvalidOperationException("Implied volatility must be greater than zero and no more than 10.");
        if (ValuationTimeUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Valuation time must be UTC.");
        if (ExpirationTimeUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Expiration time must be UTC.");
        if (ExpirationTimeUtc.Date < ValuationTimeUtc.Date)
            throw new InvalidOperationException("Expiration date must not be before valuation date.");
        if (double.IsNaN(RiskFreeRate) || double.IsInfinity(RiskFreeRate) || RiskFreeRate < -1.0 || RiskFreeRate > 1.0)
            throw new InvalidOperationException("Risk-free rate must be between -1 and 1.");
        if (double.IsNaN(DividendYield) || double.IsInfinity(DividendYield) || DividendYield < -1.0 || DividendYield > 1.0)
            throw new InvalidOperationException("Dividend yield must be between -1 and 1.");
    }
}
