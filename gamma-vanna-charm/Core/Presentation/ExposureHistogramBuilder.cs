using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;

namespace GammaVannaCharm.Core.Presentation;

public static class ExposureHistogramBuilder
{
    public static List<HistogramRenderLevel> Build(
        IList<OptionStrikeExposure> strikeExposures,
        FuturesFamily futuresFamily,
        SpotProxySymbol proxySymbol,
        OptionExposureMetric exposureMetric,
        double currentProxyPrice,
        double currentIndexPrice,
        double currentFuturesPrice,
        SpotProxyConversionMultipliers multipliers,
        double tickSize,
        double heatmapBandTicks,
        int heatmapMaxOpacity,
        out string status)
    {
        status = null;

        if (strikeExposures == null || strikeExposures.Count == 0)
            return new List<HistogramRenderLevel>();

        var rankedExposures = GetRankedStrikeExposures(strikeExposures, proxySymbol, exposureMetric);
        var maxAbsMetric = GetMaxAbsMetric(rankedExposures, exposureMetric);
        if (maxAbsMetric <= 0.0)
        {
            status = "Options data loaded, but selected exposure values are zero.";
            return new List<HistogramRenderLevel>();
        }

        var renderLevels = new List<HistogramRenderLevel>();
        for (var index = 0; index < rankedExposures.Count; index++)
        {
            var exposure = rankedExposures[index];
            double futuresLevel;
            if (!SpotProxyLevelMapper.TryMapToFuturesLevel(
                    futuresFamily,
                    proxySymbol,
                    exposure.Strike,
                    currentProxyPrice,
                    currentIndexPrice,
                    currentFuturesPrice,
                    multipliers,
                    out futuresLevel))
            {
                continue;
            }

            var metricValue = GetMetricValue(exposure, exposureMetric);
            var intensity = Math.Abs(metricValue) / maxAbsMetric;
            var opacity = Math.Max(0.0, Math.Min(1.0, heatmapMaxOpacity / 100.0));
            renderLevels.Add(new HistogramRenderLevel(
                exposure.Strike,
                futuresLevel,
                GetMappedStrikeBinPriceBand(exposure.Strike, proxySymbol, futuresFamily, currentProxyPrice, currentIndexPrice, currentFuturesPrice, multipliers, tickSize, heatmapBandTicks),
                metricValue,
                intensity,
                opacity));
        }

        if (renderLevels.Count == 0)
            status = "Options data loaded, but no option levels mapped to this chart. Check chart instrument and proxy symbol.";

        return renderLevels;
    }

    public static List<OptionStrikeExposure> GetRankedStrikeExposures(IList<OptionStrikeExposure> strikeExposures, SpotProxySymbol proxySymbol, OptionExposureMetric exposureMetric)
    {
        var rankedExposures = GetBinnedStrikeExposures(strikeExposures, proxySymbol);
        rankedExposures.Sort((left, right) =>
        {
            var metricComparison = Math.Abs(GetMetricValue(right, exposureMetric)).CompareTo(Math.Abs(GetMetricValue(left, exposureMetric)));
            return metricComparison != 0 ? metricComparison : left.Strike.CompareTo(right.Strike);
        });
        return rankedExposures;
    }

    public static List<OptionStrikeExposure> GetBinnedStrikeExposures(IList<OptionStrikeExposure> strikeExposures, SpotProxySymbol proxySymbol)
    {
        if (strikeExposures == null || strikeExposures.Count == 0)
            return new List<OptionStrikeExposure>();

        var strikeBinWidth = GetStrikeBinWidth(proxySymbol);
        if (strikeBinWidth <= 0.0)
            return new List<OptionStrikeExposure>(strikeExposures);

        var buckets = new Dictionary<long, StrikeExposureBucket>();
        for (var index = 0; index < strikeExposures.Count; index++)
        {
            var exposure = strikeExposures[index];
            var key = (long)Math.Round(exposure.Strike / strikeBinWidth, MidpointRounding.AwayFromZero);
            StrikeExposureBucket bucket;
            if (!buckets.TryGetValue(key, out bucket))
            {
                bucket = new StrikeExposureBucket(key * strikeBinWidth);
                buckets[key] = bucket;
            }

            bucket.GammaExposure += exposure.GammaExposure;
            bucket.VannaExposure += exposure.VannaExposure;
            bucket.CharmExposure += exposure.CharmExposure;
            bucket.OpenInterest += exposure.OpenInterest;
            bucket.ContractCount += exposure.ContractCount;
        }

        var binnedExposures = new List<OptionStrikeExposure>(buckets.Count);
        foreach (var bucket in buckets.Values)
        {
            binnedExposures.Add(new OptionStrikeExposure(
                bucket.Strike,
                bucket.GammaExposure,
                bucket.VannaExposure,
                bucket.CharmExposure,
                bucket.OpenInterest,
                bucket.ContractCount));
        }

        return binnedExposures;
    }

    public static double GetStrikeBinWidth(SpotProxySymbol proxySymbol)
    {
        if (proxySymbol == SpotProxySymbol.SPX)
            return 5.0;
        if (proxySymbol == SpotProxySymbol.SPY)
            return 1.0;
        if (proxySymbol == SpotProxySymbol.QQQ)
            return 1.0;

        return 0.0;
    }

    public static double GetMetricValue(OptionStrikeExposure exposure, OptionExposureMetric exposureMetric)
    {
        var primaryMetric = ExposureMetricPresentation.GetPrimaryMetric(exposureMetric);
        if (primaryMetric == OptionExposureMetric.Vanna)
            return exposure.VannaExposure;
        if (primaryMetric == OptionExposureMetric.Charm)
            return exposure.CharmExposure;

        return exposure.GammaExposure;
    }

    private static double GetMappedStrikeBinPriceBand(
        double strike,
        SpotProxySymbol proxySymbol,
        FuturesFamily futuresFamily,
        double currentProxyPrice,
        double currentIndexPrice,
        double currentFuturesPrice,
        SpotProxyConversionMultipliers multipliers,
        double tickSize,
        double heatmapBandTicks)
    {
        var fallbackPriceBand = Math.Max(tickSize, heatmapBandTicks * tickSize);
        var strikeBinWidth = GetStrikeBinWidth(proxySymbol);
        if (strikeBinWidth <= 0.0)
            return fallbackPriceBand;

        double mappedStrike;
        double mappedUpperStrike;
        if (!SpotProxyLevelMapper.TryMapToFuturesLevel(
                futuresFamily,
                proxySymbol,
                strike,
                currentProxyPrice,
                currentIndexPrice,
                currentFuturesPrice,
                multipliers,
                out mappedStrike)
            || !SpotProxyLevelMapper.TryMapToFuturesLevel(
                futuresFamily,
                proxySymbol,
                strike + strikeBinWidth,
                currentProxyPrice,
                currentIndexPrice,
                currentFuturesPrice,
                multipliers,
                out mappedUpperStrike))
        {
            return fallbackPriceBand;
        }

        return Math.Max(fallbackPriceBand, Math.Abs(mappedUpperStrike - mappedStrike));
    }

    private static double GetMaxAbsMetric(IList<OptionStrikeExposure> exposures, OptionExposureMetric exposureMetric)
    {
        var maxAbsMetric = 0.0;
        for (var index = 0; index < exposures.Count; index++)
            maxAbsMetric = Math.Max(maxAbsMetric, Math.Abs(GetMetricValue(exposures[index], exposureMetric)));

        return maxAbsMetric;
    }

    private sealed class StrikeExposureBucket
    {
        public StrikeExposureBucket(double strike)
        {
            Strike = strike;
        }

        public double Strike { get; private set; }

        public double GammaExposure { get; set; }

        public double VannaExposure { get; set; }

        public double CharmExposure { get; set; }

        public long OpenInterest { get; set; }

        public int ContractCount { get; set; }
    }
}
