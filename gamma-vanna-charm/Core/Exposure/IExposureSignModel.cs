namespace GammaVannaCharm.Core.Exposure;

public interface IExposureSignModel
{
    double ApplyGammaExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure);

    double ApplyVannaExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure);

    double ApplyCharmExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure);
}
