using System;

namespace GammaVannaCharm.Core.Exposure;

public sealed class SignedContractExposureModel : IExposureModel
{
    public double CalculateGammaExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        return option.Gamma
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.Spot
            * ExposureDefaults.OnePercentMove;
    }

    public double CalculateVannaExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        return option.Vanna
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.IvMove;
    }

    public double CalculateCharmExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        return option.Charm
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.TimeStep;
    }

    private static void ValidateInputs(OptionExposureInput option, MarketContext marketContext)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));
        if (marketContext == null)
            throw new ArgumentNullException(nameof(marketContext));
    }
}
