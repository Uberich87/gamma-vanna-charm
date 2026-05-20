using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureStrikeHistorySeries
{
    public OptionExposureStrikeHistorySeries(double strike, IList<double> values)
    {
        Strike = strike;
        Values = new List<double>(values ?? new List<double>()).AsReadOnly();
    }

    public double Strike { get; private set; }

    public IReadOnlyList<double> Values { get; private set; }
}
