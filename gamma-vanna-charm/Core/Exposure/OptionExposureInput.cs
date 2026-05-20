namespace GammaVannaCharm.Core.Exposure;

public sealed class OptionExposureInput
{
    public OptionRight Right { get; set; }

    public double Gamma { get; set; }

    public double Vanna { get; set; }

    public double Charm { get; set; }

    public double OpenInterest { get; set; }

    public double ContractSize { get; set; } = ExposureDefaults.ContractSize;
}
