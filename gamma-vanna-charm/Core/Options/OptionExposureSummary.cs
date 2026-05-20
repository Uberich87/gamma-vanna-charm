namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureSummary
{
    public OptionExposureSummary(double gammaExposure, double vannaExposure, double charmExposure, int contractCount)
        : this(gammaExposure, vannaExposure, charmExposure, contractCount, 0.0)
    {
    }

    public OptionExposureSummary(double gammaExposure, double vannaExposure, double charmExposure, int contractCount, double weightedImpliedVolatility)
    {
        GammaExposure = gammaExposure;
        VannaExposure = vannaExposure;
        CharmExposure = charmExposure;
        ContractCount = contractCount;
        WeightedImpliedVolatility = weightedImpliedVolatility;
    }

    public double GammaExposure { get; private set; }

    public double VannaExposure { get; private set; }

    public double CharmExposure { get; private set; }

    public int ContractCount { get; private set; }

    public double WeightedImpliedVolatility { get; private set; }
}
