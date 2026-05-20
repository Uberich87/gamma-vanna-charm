using System;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.MarketData.Cboe;

public sealed class CboeExposureCalculator
{
    private readonly OptionExposureCalculator _exposureCalculator;

    public CboeExposureCalculator()
        : this(new OptionExposureCalculator())
    {
    }

    public CboeExposureCalculator(OptionExposureCalculator exposureCalculator)
    {
        _exposureCalculator = exposureCalculator ?? throw new ArgumentNullException(nameof(exposureCalculator));
    }

    public bool TryCalculate(
        string optionsSymbol,
        string optionsJson,
        bool useZeroDteOptions,
        DateTime valuationTimeUtc,
        out CboeExposureCalculation calculation)
    {
        calculation = null;

        OptionChainSnapshot snapshot;
        if (!CboeOptionsParser.TryParseOptionChainSnapshot(optionsSymbol, optionsJson, useZeroDteOptions, out snapshot))
            return false;

        var exposureCalculation = _exposureCalculator.Calculate(snapshot, valuationTimeUtc);
        calculation = new CboeExposureCalculation(
            exposureCalculation.UnderlyingPrice,
            exposureCalculation.NaiveSummary,
            exposureCalculation.NaiveStrikeExposures);
        return true;
    }
}
