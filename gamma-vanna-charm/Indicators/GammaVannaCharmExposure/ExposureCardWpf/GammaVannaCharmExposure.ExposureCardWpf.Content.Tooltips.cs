#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GammaVannaCharm.Core.MarketData;
using GammaVannaCharm.Core.Options;
using GammaVannaCharm.Core.Presentation;
#endregion

namespace NinjaTrader.NinjaScript.Indicators;

internal sealed partial class ExposureCardContentView
{
    private static string GetMetricLensHelpTooltip(OptionExposureMetric metric)
    {
        if (metric == OptionExposureMetric.Vanna)
        {
            return "Vanna Tab\n"
                + "Question: will IV changes add hedge pressure here?\n\n"
                + "What Vanna Means\n"
                + "Vanna measures how option delta changes when implied volatility changes.\n"
                + "VEX shows where IV-driven hedge sensitivity is concentrated.\n"
                + "Use VEX with IV repricing; it is not a standalone direction signal.\n\n"
                + "VEX Context\n"
                + "VEX Skew: where IV-sensitive exposure is concentrated.\n"
                + "IV Sensitivity: current VEX size vs the recent 2-hour persisted trend window.\n"
                + "IV Condition: weighted chain IV is firming, fading, neutral, or not yet measured; VEX still needs volatility repricing to become active.\n\n"
                + "Zone Cards\n"
                + "Zone: top 5 nearby VEX strikes/clusters within ±5% of spot.\n"
                + "Notional: current VEX at the zone.\n"
                + "Δ VEX: recent 2-hour change in absolute VEX.\n"
                + "State: Building, Stable, Unwinding, or Conditional.\n"
                + "Trend: recent strike-level VEX history.";
        }

        if (metric == OptionExposureMetric.Charm)
        {
            return "Charm Tab\n"
                + "Question: will time decay matter here, and when?\n\n"
                + "What Charm Means\n"
                + "Charm measures how option delta changes as time passes.\n"
                + "CEX shows where time-decay hedge sensitivity is concentrated.\n"
                + "CEX is time-weighted: usually lower priority early and more relevant late-session/0DTE.\n\n"
                + "CEX Context\n"
                + "CEX Skew: where clock-sensitive exposure is concentrated.\n"
                + "Time Weight: CEX matters more later in the session.\n"
                + "Clock Sens.: current CEX size vs the recent 2-hour persisted trend window.\n\n"
                + "Zone Cards\n"
                + "Zone: top 5 nearby CEX strikes/clusters within ±5% of spot.\n"
                + "Notional: current CEX at the zone.\n"
                + "Δ CEX: recent 2-hour change in absolute CEX.\n"
                + "State: Building, Stable, Unwinding, or Conditional.\n"
                + "Trend: recent strike-level CEX history.";
        }

        return "Gamma Tab\n"
            + "Question: will dealers fight or chase price here?\n\n"
            + "What Gamma Means\n"
            + "Gamma measures how quickly option delta changes as price moves.\n"
            + "GEX shows the price-response layer: +GEX tends to suppress/pin, while -GEX can amplify accepted moves.\n\n"
            + "GEX Regime\n"
            + "Regime: suppression, expansion, or split response around spot.\n"
            + "Skew: which side has larger absolute net GEX.\n"
            + "Intensity: current absolute GEX size vs the recent 2-hour persisted trend window.\n\n"
            + "Zone Cards\n"
            + "Zone: top 5 nearby GEX strikes/clusters within ±5% of spot.\n"
            + "Notional: current GEX at the zone.\n"
            + "Δ GEX: recent 2-hour change in absolute GEX.\n"
            + "State: Building, Stable, Unwinding, or Conditional.\n"
            + "Migration: Toward, Away, Flat, Higher, or Lower relative to spot.\n"
            + "Trend: recent strike-level GEX history.";
    }

    private static string GetOverallTradingZonesHelpTooltip()
    {
        return "GEX Regime Score\n"
            + "Regime: net GEX setup around spot.\n"
            + "  Why it matters: it frames whether accepted moves are more likely to suppress or amplify.\n"
            + "  Expansion = -GEX dominates.\n"
            + "  Suppression = +GEX dominates.\n"
            + "  Split = different response above vs below.\n\n"
            + "Skew: which side has larger absolute net GEX.\n"
            + "  Why it matters: the larger side is the response that can dominate if price accepts into it.\n"
            + "Ratio: larger side divided by smaller side.\n"
            + "Above/Below net values: sum all mapped GEX levels on that side of spot, not just the visible top 5 cards.\n\n"
            + "Intensity: current total absolute GEX vs the recent 2-hour persisted trend window.\n"
            + "  Why it matters: higher intensity means the dealer-response map is more likely to influence price behavior.\n\n"
            + "Zone Cards\n"
            + "Zone: one of the top 5 nearby GEX strikes/clusters within ±5% of spot, mapped to chart price.\n"
            + "  Why it matters: these are the levels most likely to change hedge response if price interacts with them.\n"
            + "Stack: GEX base plus same/nearby VEX and CEX.\n"
            + "  Why it matters: VEX can add conditional pressure if IV reprices; CEX can add conditional pressure later in the session.\n"
            + "Notional: current exposure at the zone.\n"
            + "Δ Notional: change in absolute exposure over the recent 2-hour trend window. + means building; - means fading.\n\n"
            + "State\n"
            + "State is inferred from notional trend/slope, top-strike rotation, center migration, spot distance, and same/nearby-strike VEX/CEX context.\n"
            + "Building: exposure/relevance increasing.\n"
            + "Stable: active, but not accelerating.\n"
            + "Unwinding: exposure/relevance fading.\n"
            + "Conditional: matters only if spot, IV, or time activates it.\n\n"
            + "Migration: Toward, Away, Flat, Higher, or Lower relative to spot.\n"
            + "Trend: strike-level exposure history over roughly the last 2 hours.";
    }

}
