using System;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureStrikeTrend
{
    public OptionExposureStrikeTrend(
        OptionExposureMetric metric,
        int snapshotCount,
        double firstTopStrike,
        double latestTopStrike,
        double firstWeightedStrike,
        double latestWeightedStrike)
    {
        Metric = metric;
        SnapshotCount = snapshotCount;
        FirstTopStrike = firstTopStrike;
        LatestTopStrike = latestTopStrike;
        FirstWeightedStrike = firstWeightedStrike;
        LatestWeightedStrike = latestWeightedStrike;
    }

    public OptionExposureMetric Metric { get; private set; }

    public int SnapshotCount { get; private set; }

    public double FirstTopStrike { get; private set; }

    public double LatestTopStrike { get; private set; }

    public double FirstWeightedStrike { get; private set; }

    public double LatestWeightedStrike { get; private set; }

    public bool HasRotation
    {
        get { return Math.Abs(LatestTopStrike - FirstTopStrike) > 0.0000001; }
    }

    public double WeightedStrikeChange
    {
        get { return LatestWeightedStrike - FirstWeightedStrike; }
    }
}
