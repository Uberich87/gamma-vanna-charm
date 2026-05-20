using System;

namespace GammaVannaCharm.Core.Options;

public interface IOptionExposureSizeResolver
{
    bool TryGetExposureSize(OptionContractData contract, DateTime valuationTimeUtc, DateTime expirationTimeUtc, out double exposureSize);
}
