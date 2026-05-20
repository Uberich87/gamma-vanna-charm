using System;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureCalculator
{
    private readonly OptionChainExposureCalculator _naiveChainExposureCalculator;
    private readonly OptionStrikeExposureCalculator _naiveStrikeExposureCalculator;

    public OptionExposureCalculator()
        : this(new OptionChainExposureCalculator(), new OptionStrikeExposureCalculator())
    {
    }

    public OptionExposureCalculator(
        OptionChainExposureCalculator chainExposureCalculator,
        OptionStrikeExposureCalculator strikeExposureCalculator)
    {
        _naiveChainExposureCalculator = chainExposureCalculator ?? throw new ArgumentNullException(nameof(chainExposureCalculator));
        _naiveStrikeExposureCalculator = strikeExposureCalculator ?? throw new ArgumentNullException(nameof(strikeExposureCalculator));
    }

    public OptionExposureCalculation Calculate(OptionChainSnapshot snapshot, DateTime valuationTimeUtc)
    {
        return Calculate(snapshot, valuationTimeUtc, null);
    }

    public OptionExposureCalculation Calculate(OptionChainSnapshot snapshot, DateTime valuationTimeUtc, OptionImpliedDealerSnapshot impliedDealerSnapshot)
    {
        return Calculate(
            snapshot,
            valuationTimeUtc,
            impliedDealerSnapshot,
            0.0,
            0.0,
            ExposureDefaults.OnePercentMove,
            ExposureDefaults.OneCalendarDay,
            ExposureDefaults.ContractSize);
    }

    public OptionExposureCalculation Calculate(
        OptionChainSnapshot snapshot,
        DateTime valuationTimeUtc,
        double riskFreeRate,
        double dividendYield,
        double ivMove,
        double timeStep,
        double contractSize)
    {
        return Calculate(
            snapshot,
            valuationTimeUtc,
            null,
            riskFreeRate,
            dividendYield,
            ivMove,
            timeStep,
            contractSize);
    }

    public OptionExposureCalculation Calculate(
        OptionChainSnapshot snapshot,
        DateTime valuationTimeUtc,
        OptionImpliedDealerSnapshot impliedDealerSnapshot,
        double riskFreeRate,
        double dividendYield,
        double ivMove,
        double timeStep,
        double contractSize)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        var naiveSummary = _naiveChainExposureCalculator.Calculate(
            snapshot,
            valuationTimeUtc,
            riskFreeRate,
            dividendYield,
            ivMove,
            timeStep,
            contractSize);
        var naiveStrikeExposures = _naiveStrikeExposureCalculator.Calculate(
            snapshot,
            valuationTimeUtc,
            riskFreeRate,
            dividendYield,
            ivMove,
            timeStep,
            contractSize);

        OptionExposureSummary flowImpliedDealerSummary = null;
        var hasFlowImpliedDealerExposure = impliedDealerSnapshot != null && impliedDealerSnapshot.HasDealerState;
        var flowImpliedDealerStrikeExposures = new System.Collections.Generic.List<OptionStrikeExposure>();
        if (hasFlowImpliedDealerExposure)
        {
            var exposureSizeResolver = new ImpliedDealerOptionExposureSizeResolver(impliedDealerSnapshot);
            var exposureModel = new SignedContractExposureModel();
            var chainExposureCalculator = new OptionChainExposureCalculator(new BlackScholesOptionGreekCalculator(), exposureModel, exposureSizeResolver);
            var strikeExposureCalculator = new OptionStrikeExposureCalculator(new BlackScholesOptionGreekCalculator(), exposureModel, exposureSizeResolver);

            flowImpliedDealerSummary = chainExposureCalculator.Calculate(
                snapshot,
                valuationTimeUtc,
                riskFreeRate,
                dividendYield,
                ivMove,
                timeStep,
                contractSize);
            flowImpliedDealerStrikeExposures = strikeExposureCalculator.Calculate(
                snapshot,
                valuationTimeUtc,
                riskFreeRate,
                dividendYield,
                ivMove,
                timeStep,
                contractSize);
        }

        return new OptionExposureCalculation(
            snapshot.UnderlyingSymbol,
            snapshot.UnderlyingPrice,
            snapshot.TimestampUtc,
            naiveSummary,
            naiveStrikeExposures,
            flowImpliedDealerSummary,
            flowImpliedDealerStrikeExposures,
            hasFlowImpliedDealerExposure);
    }
}
