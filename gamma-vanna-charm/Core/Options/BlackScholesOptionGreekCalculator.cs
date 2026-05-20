using System;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class BlackScholesOptionGreekCalculator : IOptionGreekCalculator
{
    private const double MinimumTimeToExpiryYears = 1.0 / (365.0 * 24.0 * 60.0);

    public OptionGreeks Calculate(OptionGreekInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        input.Validate();
        if (input.UnderlyingPrice <= 0.0)
            return new OptionGreeks(0.0, 0.0, 0.0);
        if (input.Strike <= 0.0)
            return new OptionGreeks(0.0, 0.0, 0.0);
        if (input.ImpliedVolatility <= 0.0 || double.IsNaN(input.ImpliedVolatility) || double.IsInfinity(input.ImpliedVolatility))
            return new OptionGreeks(0.0, 0.0, 0.0);

        var timeToExpiryYears = Math.Max(
            MinimumTimeToExpiryYears,
            (input.ExpirationTimeUtc - input.ValuationTimeUtc).TotalDays / 365.0);
        var sqrtTime = Math.Sqrt(timeToExpiryYears);
        var sigmaSqrtTime = input.ImpliedVolatility * sqrtTime;
        if (sigmaSqrtTime <= 0.0)
            return new OptionGreeks(0.0, 0.0, 0.0);

        var d1 = (Math.Log(input.UnderlyingPrice / input.Strike)
            + (input.RiskFreeRate - input.DividendYield + (0.5 * input.ImpliedVolatility * input.ImpliedVolatility)) * timeToExpiryYears)
            / sigmaSqrtTime;
        var d2 = d1 - sigmaSqrtTime;
        var discountDividend = Math.Exp(-input.DividendYield * timeToExpiryYears);
        var pdfD1 = StandardNormalPdf(d1);

        var gamma = discountDividend * pdfD1 / (input.UnderlyingPrice * sigmaSqrtTime);
        var vanna = -discountDividend * pdfD1 * d2 / input.ImpliedVolatility;
        var charm = CalculateCharm(input.Right, input.RiskFreeRate, input.DividendYield, timeToExpiryYears, sigmaSqrtTime, d1, d2, discountDividend, pdfD1);

        return new OptionGreeks(gamma, vanna, charm);
    }

    private static double CalculateCharm(
        OptionRight right,
        double riskFreeRate,
        double dividendYield,
        double timeToExpiryYears,
        double sigmaSqrtTime,
        double d1,
        double d2,
        double discountDividend,
        double pdfD1)
    {
        var common = discountDividend * pdfD1
            * ((2.0 * (riskFreeRate - dividendYield) * timeToExpiryYears) - (d2 * sigmaSqrtTime))
            / (2.0 * timeToExpiryYears * sigmaSqrtTime);

        if (right == OptionRight.Call)
            return (dividendYield * discountDividend * StandardNormalCdf(d1)) - common;

        return (-dividendYield * discountDividend * StandardNormalCdf(-d1)) - common;
    }

    private static double StandardNormalPdf(double value)
    {
        return Math.Exp(-0.5 * value * value) / Math.Sqrt(2.0 * Math.PI);
    }

    private static double StandardNormalCdf(double value)
    {
        return 0.5 * (1.0 + Erf(value / Math.Sqrt(2.0)));
    }

    private static double Erf(double value)
    {
        var sign = value < 0.0 ? -1.0 : 1.0;
        var x = Math.Abs(value);
        var t = 1.0 / (1.0 + (0.3275911 * x));
        var y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }
}
