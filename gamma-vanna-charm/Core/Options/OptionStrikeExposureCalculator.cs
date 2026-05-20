using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionStrikeExposureCalculator
{
    private readonly IOptionGreekCalculator _greekCalculator;
    private readonly IExposureModel _exposureModel;
    private readonly IOptionExposureSizeResolver _exposureSizeResolver;

    public OptionStrikeExposureCalculator()
        : this(new BlackScholesOptionGreekCalculator(), new NaiveExposureModel())
    {
    }

    public OptionStrikeExposureCalculator(IOptionGreekCalculator greekCalculator, IExposureModel exposureModel)
        : this(greekCalculator, exposureModel, new NaiveOptionExposureSizeResolver())
    {
    }

    public OptionStrikeExposureCalculator(IOptionGreekCalculator greekCalculator, IExposureModel exposureModel, IOptionExposureSizeResolver exposureSizeResolver)
    {
        _greekCalculator = greekCalculator ?? throw new ArgumentNullException(nameof(greekCalculator));
        _exposureModel = exposureModel ?? throw new ArgumentNullException(nameof(exposureModel));
        _exposureSizeResolver = exposureSizeResolver ?? throw new ArgumentNullException(nameof(exposureSizeResolver));
    }

    public List<OptionStrikeExposure> Calculate(
        OptionChainSnapshot snapshot,
        DateTime valuationTimeUtc,
        double riskFreeRate,
        double dividendYield,
        double ivMove,
        double timeStep,
        double contractSize)
    {
        var exposures = new Dictionary<double, StrikeExposureAccumulator>();

        if (snapshot == null || snapshot.Contracts == null || snapshot.UnderlyingPrice <= 0.0)
            return new List<OptionStrikeExposure>();

        foreach (var contract in snapshot.Contracts)
        {
            if (contract == null || contract.Strike <= 0.0 || contract.ImpliedVolatility <= 0.0 || contract.ExpirationUnixSeconds <= 0L)
                continue;

            var expirationTimeUtc = UnixSecondsToUtc(contract.ExpirationUnixSeconds);
            // Compare the full timestamp, not just the date, so same-day contracts stop
            // contributing after their actual close instead of being priced with clamped time.
            if (expirationTimeUtc <= valuationTimeUtc)
                continue;

            double exposureSize;
            if (!_exposureSizeResolver.TryGetExposureSize(contract, valuationTimeUtc, expirationTimeUtc, out exposureSize)
                || Math.Abs(exposureSize) <= 0.0000001)
            {
                continue;
            }

            var greeks = _greekCalculator.Calculate(new OptionGreekInput
            {
                Right = contract.Right,
                UnderlyingPrice = snapshot.UnderlyingPrice,
                Strike = contract.Strike,
                ImpliedVolatility = contract.ImpliedVolatility,
                ValuationTimeUtc = valuationTimeUtc,
                ExpirationTimeUtc = expirationTimeUtc,
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield
            });

            var option = new OptionExposureInput
            {
                Right = contract.Right,
                Gamma = contract.Gamma > 0.0 ? contract.Gamma : greeks.Gamma,
                Vanna = greeks.Vanna,
                Charm = greeks.Charm,
                OpenInterest = exposureSize,
                ContractSize = contractSize
            };
            var marketContext = new MarketContext
            {
                Spot = snapshot.UnderlyingPrice,
                IvMove = ivMove,
                TimeStep = timeStep
            };

            StrikeExposureAccumulator accumulator;
            if (!exposures.TryGetValue(contract.Strike, out accumulator))
            {
                accumulator = new StrikeExposureAccumulator(contract.Strike);
                exposures.Add(contract.Strike, accumulator);
            }

            accumulator.GammaExposure += _exposureModel.CalculateGammaExposure(option, marketContext);
            accumulator.VannaExposure += _exposureModel.CalculateVannaExposure(option, marketContext);
            accumulator.CharmExposure += _exposureModel.CalculateCharmExposure(option, marketContext);
            accumulator.OpenInterest += (long)Math.Round(exposureSize);
            accumulator.ContractCount++;
        }

        var result = new List<OptionStrikeExposure>();
        foreach (var accumulator in exposures.Values)
            result.Add(accumulator.ToExposure());

        return result;
    }

    private static DateTime UnixSecondsToUtc(long unixSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
    }

    private sealed class StrikeExposureAccumulator
    {
        public StrikeExposureAccumulator(double strike)
        {
            Strike = strike;
        }

        public double Strike { get; private set; }

        public double GammaExposure { get; set; }

        public double VannaExposure { get; set; }

        public double CharmExposure { get; set; }

        public long OpenInterest { get; set; }

        public int ContractCount { get; set; }

        public OptionStrikeExposure ToExposure()
        {
            return new OptionStrikeExposure(Strike, GammaExposure, VannaExposure, CharmExposure, OpenInterest, ContractCount);
        }
    }
}
