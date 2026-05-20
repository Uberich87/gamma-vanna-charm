using System;

namespace GammaVannaCharm.Core.Options;

public sealed class ImpliedDealerOptionExposureSizeResolver : IOptionExposureSizeResolver
{
    private readonly OptionImpliedDealerSnapshot _snapshot;

    public ImpliedDealerOptionExposureSizeResolver(OptionImpliedDealerSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public bool TryGetExposureSize(OptionContractData contract, DateTime valuationTimeUtc, DateTime expirationTimeUtc, out double exposureSize)
    {
        exposureSize = 0.0;
        return contract != null && _snapshot.TryGetDealerContracts(contract.Symbol, out exposureSize);
    }
}
