using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.MarketData.Cboe;

public sealed class CboeExposureCalculation
{
    public CboeExposureCalculation(
        double underlyingPrice,
        OptionExposureSummary summary,
        List<OptionStrikeExposure> strikeExposures)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        if (strikeExposures == null)
            throw new ArgumentNullException(nameof(strikeExposures));

        UnderlyingPrice = underlyingPrice;
        Summary = summary;
        StrikeExposures = strikeExposures;
    }

    public double UnderlyingPrice { get; private set; }

    public OptionExposureSummary Summary { get; private set; }

    public List<OptionStrikeExposure> StrikeExposures { get; private set; }
}
