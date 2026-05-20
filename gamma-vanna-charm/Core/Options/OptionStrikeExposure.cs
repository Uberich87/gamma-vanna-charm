namespace GammaVannaCharm.Core.Options;

public sealed class OptionStrikeExposure
{
    public OptionStrikeExposure(double strike, double gammaExposure, double vannaExposure, double charmExposure, long openInterest, int contractCount)
    {
        Strike = strike;
        GammaExposure = gammaExposure;
        VannaExposure = vannaExposure;
        CharmExposure = charmExposure;
        OpenInterest = openInterest;
        ContractCount = contractCount;
    }

    public double Strike { get; private set; }

    public double GammaExposure { get; private set; }

    public double VannaExposure { get; private set; }

    public double CharmExposure { get; private set; }

    public long OpenInterest { get; private set; }

    public int ContractCount { get; private set; }
}
