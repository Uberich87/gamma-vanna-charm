using System;

namespace GammaVannaCharm.Core.MarketData;

public interface ISpotPriceProvider : IDisposable
{
    string Name { get; }

    string LastError { get; }

    void Start();

    void Stop();

    bool TryGetLatestQuote(out SpotPriceQuote quote);
}
