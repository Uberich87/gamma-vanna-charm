namespace GammaVannaCharm.Core.Exposure;

public interface IExposureModel
{
    double CalculateGammaExposure(OptionExposureInput option, MarketContext marketContext);

    double CalculateVannaExposure(OptionExposureInput option, MarketContext marketContext);

    double CalculateCharmExposure(OptionExposureInput option, MarketContext marketContext);
}
