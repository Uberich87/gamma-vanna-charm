using System;
using GammaVannaCharm.Core.Exposure;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Tests.Options;

internal static class OptionGreekCalculatorTests
{
    public static void Run()
    {
        MatchesKnownBlackScholesGreekBenchmarks();
        CallAndPutShareGammaAndVannaForSameInputs();
        GammaMatchesFiniteDifferenceDeltaByUnderlying();
        VannaMatchesFiniteDifferenceDeltaByVolatility();
        CharmMatchesFiniteDifferenceDeltaByCalendarTime();
        KeepsNearExpiryGreeksFinite();
        RejectsInvalidGreekInputs();
    }

    public static void MatchesKnownBlackScholesGreekBenchmarks()
    {
        var calculator = new BlackScholesOptionGreekCalculator();
        var callGreeks = calculator.Calculate(CreateInput(OptionRight.Call));
        var putGreeks = calculator.Calculate(CreateInput(OptionRight.Put));

        TestAssert.Equal(0.029740835852449, callGreeks.Gamma, "Call gamma benchmark");
        TestAssert.Equal(0.584426047321480, callGreeks.Vanna, "Call vanna benchmark");
        TestAssert.Equal(-0.356668224503157, callGreeks.Charm, "Call charm benchmark");
        TestAssert.Equal(0.029740835852449, putGreeks.Gamma, "Put gamma benchmark");
        TestAssert.Equal(0.584426047321480, putGreeks.Vanna, "Put vanna benchmark");
        TestAssert.Equal(-0.366641348583294, putGreeks.Charm, "Put charm benchmark");
    }

    public static void CallAndPutShareGammaAndVannaForSameInputs()
    {
        var calculator = new BlackScholesOptionGreekCalculator();
        var callGreeks = calculator.Calculate(CreateInput(OptionRight.Call));
        var putGreeks = calculator.Calculate(CreateInput(OptionRight.Put));

        TestAssert.Equal(callGreeks.Gamma, putGreeks.Gamma, "Call and put gamma match for same strike/expiry/IV");
        TestAssert.Equal(callGreeks.Vanna, putGreeks.Vanna, "Call and put vanna match for same strike/expiry/IV");
        TestAssert.True(Math.Abs(callGreeks.Charm - putGreeks.Charm) > 0.000001, "Call and put charm differ when dividend yield is non-zero");
    }

    public static void GammaMatchesFiniteDifferenceDeltaByUnderlying()
    {
        var input = CreateInput(OptionRight.Call);
        var greeks = new BlackScholesOptionGreekCalculator().Calculate(input);
        var finiteDifferenceGamma = CalculateDeltaUnderlyingDerivative(input, 0.01);

        TestAssert.True(Math.Abs(greeks.Gamma - finiteDifferenceGamma) < 0.000001, "Call gamma matches finite-difference delta by underlying");
    }

    public static void VannaMatchesFiniteDifferenceDeltaByVolatility()
    {
        var input = CreateInput(OptionRight.Call);
        var greeks = new BlackScholesOptionGreekCalculator().Calculate(input);
        var finiteDifferenceVanna = CalculateDeltaVolatilityDerivative(input, 0.0001);

        TestAssert.True(Math.Abs(greeks.Vanna - finiteDifferenceVanna) < 0.000001, "Call vanna matches finite-difference delta by volatility");
        TestAssert.True(Math.Abs(greeks.Vanna - greeks.Gamma) > 0.0001, "Vanna is distinct from gamma for non-ATM call input");
    }

    public static void CharmMatchesFiniteDifferenceDeltaByCalendarTime()
    {
        var callInput = CreateInput(OptionRight.Call);
        var putInput = CreateInput(OptionRight.Put);
        var calculator = new BlackScholesOptionGreekCalculator();
        var timeStepYears = 0.00001;

        var callGreeks = calculator.Calculate(callInput);
        var putGreeks = calculator.Calculate(putInput);
        var finiteDifferenceCallCharm = CalculateDeltaCalendarTimeDerivative(callInput, timeStepYears);
        var finiteDifferencePutCharm = CalculateDeltaCalendarTimeDerivative(putInput, timeStepYears);

        TestAssert.True(Math.Abs(callGreeks.Charm - finiteDifferenceCallCharm) < 0.000001, "Call charm matches finite-difference delta by calendar time");
        TestAssert.True(Math.Abs(putGreeks.Charm - finiteDifferencePutCharm) < 0.000001, "Put charm matches finite-difference delta by calendar time");
        TestAssert.True(Math.Abs(callGreeks.Charm - callGreeks.Vanna) > 0.0001, "Charm is distinct from vanna for non-ATM call input");
    }

    public static void KeepsNearExpiryGreeksFinite()
    {
        var calculator = new BlackScholesOptionGreekCalculator();
        var input = new OptionGreekInput
        {
            Right = OptionRight.Call,
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            ImpliedVolatility = 0.20,
            ValuationTimeUtc = new DateTime(2026, 5, 15, 20, 15, 0, DateTimeKind.Utc),
            ExpirationTimeUtc = new DateTime(2026, 5, 15, 20, 15, 0, DateTimeKind.Utc),
            RiskFreeRate = 0.04,
            DividendYield = 0.01
        };

        var greeks = calculator.Calculate(input);

        TestAssert.True(IsFinite(greeks.Gamma), "Near-expiry gamma is finite");
        TestAssert.True(IsFinite(greeks.Vanna), "Near-expiry vanna is finite");
        TestAssert.True(IsFinite(greeks.Charm), "Near-expiry charm is finite");
        TestAssert.True(greeks.Gamma > 0.0, "Near-expiry gamma remains positive");
    }

    public static void RejectsInvalidGreekInputs()
    {
        var calculator = new BlackScholesOptionGreekCalculator();
        AssertThrows<ArgumentNullException>(() => calculator.Calculate(null), "Null Greek input is rejected");

        var nonUtcInput = CreateInput(OptionRight.Call);
        nonUtcInput.ValuationTimeUtc = DateTime.SpecifyKind(nonUtcInput.ValuationTimeUtc, DateTimeKind.Unspecified);
        AssertThrows<InvalidOperationException>(() => calculator.Calculate(nonUtcInput), "Non-UTC valuation time is rejected");

        var invalidVolatilityInput = CreateInput(OptionRight.Call);
        invalidVolatilityInput.ImpliedVolatility = 10.1;
        AssertThrows<InvalidOperationException>(() => calculator.Calculate(invalidVolatilityInput), "Out-of-range IV is rejected");
    }

    private static OptionGreekInput CreateInput(OptionRight right)
    {
        return new OptionGreekInput
        {
            Right = right,
            UnderlyingPrice = 100.0,
            Strike = 105.0,
            ImpliedVolatility = 0.25,
            ValuationTimeUtc = new DateTime(2026, 5, 15, 14, 30, 0, DateTimeKind.Utc),
            ExpirationTimeUtc = new DateTime(2026, 8, 21, 20, 0, 0, DateTimeKind.Utc),
            RiskFreeRate = 0.04,
            DividendYield = 0.01
        };
    }

    private static double CalculateDeltaUnderlyingDerivative(OptionGreekInput input, double underlyingStep)
    {
        var up = CloneInput(input);
        var down = CloneInput(input);
        up.UnderlyingPrice += underlyingStep;
        down.UnderlyingPrice -= underlyingStep;

        return (CalculateBlackScholesDelta(up) - CalculateBlackScholesDelta(down)) / (2.0 * underlyingStep);
    }

    private static double CalculateDeltaVolatilityDerivative(OptionGreekInput input, double volatilityStep)
    {
        var up = CloneInput(input);
        var down = CloneInput(input);
        up.ImpliedVolatility += volatilityStep;
        down.ImpliedVolatility -= volatilityStep;

        return (CalculateBlackScholesDelta(up) - CalculateBlackScholesDelta(down)) / (2.0 * volatilityStep);
    }

    private static double CalculateDeltaCalendarTimeDerivative(OptionGreekInput input, double timeStepYears)
    {
        var forward = CloneInput(input);
        var backward = CloneInput(input);
        var timeStepDays = timeStepYears * 365.0;
        forward.ValuationTimeUtc = forward.ValuationTimeUtc.AddDays(timeStepDays);
        backward.ValuationTimeUtc = backward.ValuationTimeUtc.AddDays(-timeStepDays);

        return (CalculateBlackScholesDelta(forward) - CalculateBlackScholesDelta(backward)) / (2.0 * timeStepYears);
    }

    private static OptionGreekInput CloneInput(OptionGreekInput input)
    {
        return new OptionGreekInput
        {
            Right = input.Right,
            UnderlyingPrice = input.UnderlyingPrice,
            Strike = input.Strike,
            ImpliedVolatility = input.ImpliedVolatility,
            ValuationTimeUtc = input.ValuationTimeUtc,
            ExpirationTimeUtc = input.ExpirationTimeUtc,
            RiskFreeRate = input.RiskFreeRate,
            DividendYield = input.DividendYield
        };
    }

    private static double CalculateBlackScholesDelta(OptionGreekInput input)
    {
        var timeToExpiryYears = Math.Max(1.0 / (365.0 * 24.0 * 60.0), (input.ExpirationTimeUtc - input.ValuationTimeUtc).TotalDays / 365.0);
        var sqrtTime = Math.Sqrt(timeToExpiryYears);
        var d1 = (Math.Log(input.UnderlyingPrice / input.Strike)
            + (input.RiskFreeRate - input.DividendYield + (0.5 * input.ImpliedVolatility * input.ImpliedVolatility)) * timeToExpiryYears)
            / (input.ImpliedVolatility * sqrtTime);
        var discountDividend = Math.Exp(-input.DividendYield * timeToExpiryYears);

        if (input.Right == OptionRight.Call)
            return discountDividend * StandardNormalCdf(d1);

        return discountDividend * (StandardNormalCdf(d1) - 1.0);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message + ": expected " + typeof(TException).Name + ", actual " + exception.GetType().Name, exception);
        }

        throw new InvalidOperationException(message + ": expected " + typeof(TException).Name + ", no exception was thrown");
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
