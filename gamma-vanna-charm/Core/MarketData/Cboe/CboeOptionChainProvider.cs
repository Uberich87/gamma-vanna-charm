using System;
using System.Threading;
using System.Threading.Tasks;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.MarketData.Cboe;

public sealed class CboeOptionChainProvider : IOptionChainProvider
{
    private readonly CboeOptionsClient _optionsClient;
    private readonly bool _zeroDteOnly;

    public CboeOptionChainProvider(bool zeroDteOnly)
        : this(new CboeOptionsClient(), zeroDteOnly)
    {
    }

    public CboeOptionChainProvider(CboeOptionsClient optionsClient, bool zeroDteOnly)
    {
        _optionsClient = optionsClient ?? throw new ArgumentNullException(nameof(optionsClient));
        _zeroDteOnly = zeroDteOnly;
    }

    public async Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken)
    {
        var optionsJson = await _optionsClient.FetchOptionsAsync(symbol, cancellationToken).ConfigureAwait(false);

        OptionChainSnapshot snapshot;
        if (CboeOptionsParser.TryParseOptionChainSnapshot(symbol, optionsJson, _zeroDteOnly, out snapshot))
            return snapshot;

        throw new InvalidOperationException(_zeroDteOnly
            ? "CBOE response received, but no 0DTE option contracts parsed. Try turning off Use 0DTE options."
            : "CBOE response received, but no option contracts parsed.");
    }
}
