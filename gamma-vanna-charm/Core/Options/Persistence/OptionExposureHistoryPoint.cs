using System;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureHistoryPoint
{
    public OptionExposureHistoryPoint(DateTime timestampUtc, double gammaExposure, double vannaExposure, double charmExposure)
        : this(timestampUtc, gammaExposure, vannaExposure, charmExposure, Math.Abs(gammaExposure), Math.Abs(vannaExposure), Math.Abs(charmExposure))
    {
    }

    public OptionExposureHistoryPoint(
        DateTime timestampUtc,
        double gammaExposure,
        double vannaExposure,
        double charmExposure,
        double absoluteGammaExposure,
        double absoluteVannaExposure,
        double absoluteCharmExposure)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc && timestampUtc != DateTime.MinValue)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));

        TimestampUtc = timestampUtc;
        GammaExposure = gammaExposure;
        VannaExposure = vannaExposure;
        CharmExposure = charmExposure;
        AbsoluteGammaExposure = absoluteGammaExposure;
        AbsoluteVannaExposure = absoluteVannaExposure;
        AbsoluteCharmExposure = absoluteCharmExposure;
    }

    public DateTime TimestampUtc { get; private set; }

    public double GammaExposure { get; private set; }

    public double VannaExposure { get; private set; }

    public double CharmExposure { get; private set; }

    public double AbsoluteGammaExposure { get; private set; }

    public double AbsoluteVannaExposure { get; private set; }

    public double AbsoluteCharmExposure { get; private set; }
}
