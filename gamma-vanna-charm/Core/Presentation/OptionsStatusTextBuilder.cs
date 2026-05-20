using System;

namespace GammaVannaCharm.Core.Presentation;

public static class OptionsStatusTextBuilder
{
    public static string Build(string spotPriceStatus, string dataSourceError, bool useZeroDteOptions, string dataStatus, bool hasHistogramRenderLevels)
    {
        if (!string.IsNullOrWhiteSpace(spotPriceStatus))
            return "GVC: " + spotPriceStatus;

        if (!string.IsNullOrWhiteSpace(dataSourceError))
        {
            if (dataSourceError.IndexOf("response received", StringComparison.OrdinalIgnoreCase) >= 0)
                return "GVC: " + dataSourceError;

            return "GVC: options data error: " + dataSourceError;
        }

        if (!useZeroDteOptions
            && !string.IsNullOrWhiteSpace(dataStatus)
            && dataStatus.IndexOf("0DTE", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "GVC: Use 0DTE is off; waiting for the latest options response to reparse.";
        }

        if (hasHistogramRenderLevels)
            return null;

        if (!string.IsNullOrWhiteSpace(dataStatus))
            return "GVC: " + dataStatus;

        return "GVC: waiting for options data.";
    }
}
