using System.Threading;
using System.Threading.Tasks;

namespace GammaVannaCharm.Core.Options;

public interface IOptionChainProvider
{
    /// <summary>
    /// Fetches a provider-neutral option-chain snapshot for the requested underlying/options symbol.
    /// </summary>
    Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, CancellationToken cancellationToken);
}
