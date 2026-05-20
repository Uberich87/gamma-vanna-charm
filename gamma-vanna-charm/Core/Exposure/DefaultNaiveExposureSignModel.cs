using System;

namespace GammaVannaCharm.Core.Exposure;

/// <summary>
/// NAIVE sign convention for dealer-implied option exposure.
///
/// The naive model assumes: dealers are <em>long call open interest</em> and <em>short put
/// open interest</em>. This is the standard "naive DDOI" / industry convention (e.g.
/// SqueezeMetrics-style GEX) and is a positioning proxy, not a measurement. The actual
/// dealer side is inferred separately by <see cref="GammaVannaCharm.Core.Options.OptionFlowImpliedDealerStore"/>
/// (FLOW mode).
///
/// Concretely, the sign rule is the same for all three Greeks: call exposure is taken as-is;
/// put exposure is negated. The rationale per metric:
///   * Gamma: BS gamma is the same for a call and a put at the same strike/IV, so flipping
///     the put gives "dealer long call gamma − dealer short put gamma."
///   * Vanna: same as gamma (BS vanna is right-symmetric).
///   * Charm: BS charm is right-specific. The raw <see cref="OptionExposureInput.Charm"/>
///     coming in is already the right-specific BS charm; flipping the put again gives
///     "dealer long call charm − dealer short put charm." This is intentional — do not
///     remove the put flip on the assumption that BS charm "already encodes sign."
/// </summary>
public sealed class DefaultNaiveExposureSignModel : IExposureSignModel
{
    public double ApplyGammaExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));

        return option.Right == OptionRight.Put ? -Math.Abs(exposure) : Math.Abs(exposure);
    }

    public double ApplyVannaExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));

        return option.Right == OptionRight.Put ? -exposure : exposure;
    }

    public double ApplyCharmExposureSign(OptionExposureInput option, MarketContext marketContext, double exposure)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));

        return option.Right == OptionRight.Put ? -exposure : exposure;
    }
}
