using System;

namespace GammaVannaCharm.Core.Exposure;

public sealed class NaiveExposureModel : IExposureModel
{
    private readonly IExposureSignModel _signModel;

    public NaiveExposureModel()
        : this(new DefaultNaiveExposureSignModel())
    {
    }

    public NaiveExposureModel(IExposureSignModel signModel)
    {
        _signModel = signModel ?? throw new ArgumentNullException(nameof(signModel));
    }

    public double CalculateGammaExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        var exposure = option.Gamma
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.Spot
            * ExposureDefaults.OnePercentMove;

        return _signModel.ApplyGammaExposureSign(option, marketContext, exposure);
    }

    public double CalculateVannaExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        var exposure = option.Vanna
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.IvMove;

        return _signModel.ApplyVannaExposureSign(option, marketContext, exposure);
    }

    public double CalculateCharmExposure(OptionExposureInput option, MarketContext marketContext)
    {
        ValidateInputs(option, marketContext);

        var exposure = option.Charm
            * option.OpenInterest
            * option.ContractSize
            * marketContext.Spot
            * marketContext.TimeStep;

        return _signModel.ApplyCharmExposureSign(option, marketContext, exposure);
    }

    private static void ValidateInputs(OptionExposureInput option, MarketContext marketContext)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));
        if (marketContext == null)
            throw new ArgumentNullException(nameof(marketContext));
    }
}
