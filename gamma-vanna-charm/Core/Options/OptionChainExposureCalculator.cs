using System;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionChainExposureCalculator
{
    private readonly IOptionGreekCalculator _greekCalculator;
    private readonly IExposureModel _exposureModel;
    private readonly IOptionExposureSizeResolver _exposureSizeResolver;

    public OptionChainExposureCalculator()
        : this(new BlackScholesOptionGreekCalculator(), new NaiveExposureModel())
    {
    }

    public OptionChainExposureCalculator(IOptionGreekCalculator greekCalculator, IExposureModel exposureModel)
        : this(greekCalculator, exposureModel, new NaiveOptionExposureSizeResolver())
    {
    }

    public OptionChainExposureCalculator(IOptionGreekCalculator greekCalculator, IExposureModel exposureModel, IOptionExposureSizeResolver exposureSizeResolver)
    {
        _greekCalculator = greekCalculator ?? throw new ArgumentNullException(nameof(greekCalculator));
        _exposureModel = exposureModel ?? throw new ArgumentNullException(nameof(exposureModel));
        _exposureSizeResolver = exposureSizeResolver ?? throw new ArgumentNullException(nameof(exposureSizeResolver));
    }

    public OptionExposureSummary Calculate(
        OptionChainSnapshot snapshot,
        DateTime valuationTimeUtc,
        double riskFreeRate,
        double dividendYield,
        double ivMove,
        double timeStep,
        double contractSize)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Contracts == null || snapshot.UnderlyingPrice <= 0.0)
            return new OptionExposureSummary(0.0, 0.0, 0.0, 0);

        var totalGammaExposure = 0.0;
        var totalVannaExposure = 0.0;
        var totalCharmExposure = 0.0;
        var weightedImpliedVolatilitySum = 0.0;
        var impliedVolatilityWeight = 0.0;
        var contractCount = 0;

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

            totalGammaExposure += _exposureModel.CalculateGammaExposure(option, marketContext);
            totalVannaExposure += _exposureModel.CalculateVannaExposure(option, marketContext);
            totalCharmExposure += _exposureModel.CalculateCharmExposure(option, marketContext);
            var contractWeight = Math.Abs(exposureSize);
            weightedImpliedVolatilitySum += contract.ImpliedVolatility * contractWeight;
            impliedVolatilityWeight += contractWeight;
            contractCount++;
        }

        var weightedImpliedVolatility = impliedVolatilityWeight > 0.0000001 ? weightedImpliedVolatilitySum / impliedVolatilityWeight : 0.0;
        return new OptionExposureSummary(totalGammaExposure, totalVannaExposure, totalCharmExposure, contractCount, weightedImpliedVolatility);
    }

    private static DateTime UnixSecondsToUtc(long unixSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
    }
}
