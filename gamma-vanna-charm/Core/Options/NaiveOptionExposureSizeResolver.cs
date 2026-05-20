using System;

namespace GammaVannaCharm.Core.Options;

/// <summary>
/// NAIVE exposure size resolver: always uses open interest as a static "open positioning"
/// proxy. Intraday flow inference is intentionally left to <see cref="ImpliedDealerOptionExposureSizeResolver"/>
/// (FLOW mode) so users have a clean mental model: NAIVE = positioning snapshot, FLOW = inferred
/// dealer flow. Substituting volume on near-expiry contracts would silently mix the two.
/// </summary>
public sealed class NaiveOptionExposureSizeResolver : IOptionExposureSizeResolver
{
    public bool TryGetExposureSize(OptionContractData contract, DateTime valuationTimeUtc, DateTime expirationTimeUtc, out double exposureSize)
    {
        exposureSize = 0.0;
        if (contract == null)
            return false;

        exposureSize = contract.OpenInterest;
        return exposureSize > 0.0;
    }
}
