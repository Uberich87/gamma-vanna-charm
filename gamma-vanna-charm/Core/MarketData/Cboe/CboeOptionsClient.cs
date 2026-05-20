using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
namespace GammaVannaCharm.Core.MarketData.Cboe;

public sealed class CboeOptionsClient
{
    public const string DefaultOptionsEndpointBase = "https://cdn.cboe.com/api/global/delayed_quotes/options/";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly string _optionsEndpointBase;

    public CboeOptionsClient()
        : this(DefaultOptionsEndpointBase)
    {
    }

    public CboeOptionsClient(string optionsEndpointBase)
    {
        if (string.IsNullOrWhiteSpace(optionsEndpointBase))
            throw new ArgumentException("Options endpoint base is required.", nameof(optionsEndpointBase));

        _optionsEndpointBase = optionsEndpointBase.EndsWith("/", StringComparison.Ordinal)
            ? optionsEndpointBase
            : optionsEndpointBase + "/";
    }

    public Task<string> FetchOptionsAsync(string symbol, CancellationToken cancellationToken)
    {
        return GetStringAsync(AddCacheBuster(BuildOptionsEndpoint(_optionsEndpointBase, symbol)), cancellationToken);
    }

    /// <summary>
    /// Fetches all CBOE option expirations for the symbol; expirationUnixSeconds is reserved for provider compatibility and is currently ignored.
    /// </summary>
    public Task<string> FetchOptionsAsync(string symbol, long expirationUnixSeconds, CancellationToken cancellationToken)
    {
        return FetchOptionsAsync(symbol, cancellationToken);
    }

    public static string BuildOptionsEndpoint(string optionsEndpointBase, string symbol)
    {
        if (string.IsNullOrWhiteSpace(optionsEndpointBase))
            throw new ArgumentException("Options endpoint base is required.", nameof(optionsEndpointBase));
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        var normalizedOptionsEndpointBase = optionsEndpointBase.EndsWith("/", StringComparison.Ordinal)
            ? optionsEndpointBase
            : optionsEndpointBase + "/";
        return normalizedOptionsEndpointBase + Uri.EscapeDataString(symbol) + ".json";
    }

    private static async Task<string> GetStringAsync(string endpoint, CancellationToken cancellationToken)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, endpoint))
        {
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            request.Headers.Pragma.ParseAdd("no-cache");
            using (var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }

    private static string AddCacheBuster(string endpoint)
    {
        var separator = endpoint.IndexOf("?", StringComparison.Ordinal) >= 0 ? "&" : "?";
        return endpoint + separator + "_=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10.0)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GammaVannaCharm/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json,text/plain,*/*");
        return httpClient;
    }
}
