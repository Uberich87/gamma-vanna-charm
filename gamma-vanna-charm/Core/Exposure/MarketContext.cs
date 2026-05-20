namespace GammaVannaCharm.Core.Exposure;

public sealed class MarketContext
{
    public double Spot { get; set; }

    public double IvMove { get; set; } = ExposureDefaults.OnePercentMove;

    public double TimeStep { get; set; } = ExposureDefaults.OneCalendarDay;
}
