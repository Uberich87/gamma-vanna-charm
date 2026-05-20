#region Using declarations
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

public partial class GammaVannaCharmExposure
{
    // Keep this in sync with /version.txt for each release.
    private const string CurrentGammaVannaCharmVersion = "0.1.0";
    private const string VersionCheckUrl = "https://raw.githubusercontent.com/OrderRejected/gamma-vanna-charm/main/version.txt";
    private const string VersionReleaseUrl = "https://github.com/OrderRejected/gamma-vanna-charm/releases";
    private readonly object _versionCheckSyncRoot = new object();
    private CancellationTokenSource _versionCheckCancellationSource;
    private bool _isVersionUpdateAvailable;
    private string _latestAvailableVersion;

    private void StartVersionCheck()
    {
        StopVersionCheck();

        lock (_versionCheckSyncRoot)
        {
            _isVersionUpdateAvailable = false;
            _latestAvailableVersion = null;
        }

        var cancellationSource = new CancellationTokenSource();
        _versionCheckCancellationSource = cancellationSource;
        Task.Run(delegate { CheckForVersionUpdate(cancellationSource.Token); }, cancellationSource.Token);
    }

    private void StopVersionCheck()
    {
        var cancellationSource = _versionCheckCancellationSource;
        _versionCheckCancellationSource = null;

        if (cancellationSource == null)
            return;

        try
        {
            try
            {
                cancellationSource.Cancel();
            }
            finally
            {
                cancellationSource.Dispose();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CheckForVersionUpdate(CancellationToken cancellationToken)
    {
        try
        {
            var remoteVersionText = DownloadVersionText(VersionCheckUrl);
            if (cancellationToken.IsCancellationRequested || _isTerminated)
                return;

            var currentVersion = NormalizeVersionText(CurrentGammaVannaCharmVersion);
            var remoteVersion = NormalizeVersionText(remoteVersionText);
            if (string.IsNullOrWhiteSpace(currentVersion)
                || string.IsNullOrWhiteSpace(remoteVersion))
            {
                return;
            }

            // Only treat as an update if remote version is semantically newer
            if (!Version.TryParse(currentVersion, out var parsedCurrent)
                || !Version.TryParse(remoteVersion, out var parsedRemote)
                || parsedRemote.CompareTo(parsedCurrent) <= 0)
            {
                return;
            }

            lock (_versionCheckSyncRoot)
            {
                _isVersionUpdateAvailable = true;
                _latestAvailableVersion = remoteVersion;
            }

            RequestChartRenderRefresh();
        }
        catch (WebException)
        {
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private string GetVersionUpdateLinkText()
    {
        lock (_versionCheckSyncRoot)
        {
            if (!_isVersionUpdateAvailable)
                return string.Empty;

            return string.IsNullOrWhiteSpace(_latestAvailableVersion)
                ? "🔔\uFE0E Update"
                : "🔔\uFE0E v" + _latestAvailableVersion;
        }
    }

    private string GetVersionUpdateLinkUrl()
    {
        lock (_versionCheckSyncRoot)
            return _isVersionUpdateAvailable ? VersionReleaseUrl : string.Empty;
    }

    private static string GetCurrentVersionText()
    {
        var currentVersion = NormalizeVersionText(CurrentGammaVannaCharmVersion);
        return string.IsNullOrWhiteSpace(currentVersion) ? string.Empty : "v" + currentVersion;
    }

    private long BuildVersionUpdateSignature()
    {
        lock (_versionCheckSyncRoot)
        {
            if (!_isVersionUpdateAvailable)
                return 0L;

            unchecked
            {
                var signature = 41L;
                signature = (signature * 31L) + BuildVersionStringSignature(_latestAvailableVersion);
                signature = (signature * 31L) + BuildVersionStringSignature(VersionReleaseUrl);
                return signature;
            }
        }
    }

    private static string DownloadVersionText(string url)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
        request.UserAgent = "GammaVannaCharmExposure/" + CurrentGammaVannaCharmVersion;

        using (var response = (HttpWebResponse)request.GetResponse())
        using (var stream = response.GetResponseStream())
        {
            if (stream == null)
                return string.Empty;

            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }

    private static string NormalizeVersionText(string versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
            return string.Empty;

        var normalized = versionText.Trim();
        var lineBreakIndex = normalized.IndexOfAny(new[] { '\r', '\n' });
        if (lineBreakIndex >= 0)
            normalized = normalized.Substring(0, lineBreakIndex).Trim();
        if (normalized.StartsWith("v", true, CultureInfo.InvariantCulture))
            normalized = normalized.Substring(1).Trim();

        return normalized;
    }

    private static long BuildVersionStringSignature(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0L;

        unchecked
        {
            var signature = 43L;
            for (var index = 0; index < text.Length; index++)
                signature = (signature * 31L) + text[index];
            return signature;
        }
    }
}
