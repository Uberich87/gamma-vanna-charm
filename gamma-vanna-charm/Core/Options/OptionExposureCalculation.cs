using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionExposureCalculation
{
    public OptionExposureCalculation(
        string underlyingSymbol,
        double underlyingPrice,
        DateTime snapshotTimestampUtc,
        OptionExposureSummary summary,
        List<OptionStrikeExposure> strikeExposures)
        : this(
            underlyingSymbol,
            underlyingPrice,
            snapshotTimestampUtc,
            summary,
            strikeExposures,
            null,
            new List<OptionStrikeExposure>(),
            false)
    {
    }

    public OptionExposureCalculation(
        string underlyingSymbol,
        double underlyingPrice,
        DateTime snapshotTimestampUtc,
        OptionExposureSummary naiveSummary,
        List<OptionStrikeExposure> naiveStrikeExposures,
        OptionExposureSummary flowImpliedDealerSummary,
        List<OptionStrikeExposure> flowImpliedDealerStrikeExposures,
        bool hasFlowImpliedDealerExposure)
    {
        if (naiveSummary == null)
            throw new ArgumentNullException(nameof(naiveSummary));
        if (naiveStrikeExposures == null)
            throw new ArgumentNullException(nameof(naiveStrikeExposures));
        if (flowImpliedDealerStrikeExposures == null)
            throw new ArgumentNullException(nameof(flowImpliedDealerStrikeExposures));

        UnderlyingSymbol = underlyingSymbol;
        UnderlyingPrice = underlyingPrice;
        SnapshotTimestampUtc = snapshotTimestampUtc;
        NaiveSummary = naiveSummary;
        NaiveStrikeExposures = naiveStrikeExposures;
        FlowImpliedDealerSummary = flowImpliedDealerSummary ?? new OptionExposureSummary(0.0, 0.0, 0.0, 0);
        FlowImpliedDealerStrikeExposures = flowImpliedDealerStrikeExposures;
        HasFlowImpliedDealerExposure = hasFlowImpliedDealerExposure;

    }

    public string UnderlyingSymbol { get; private set; }

    public double UnderlyingPrice { get; private set; }

    public DateTime SnapshotTimestampUtc { get; private set; }

    [Obsolete("Use NaiveSummary or GetSummary(OptionPositionModel). Summary is a compatibility alias for NaiveSummary.")]
    public OptionExposureSummary Summary { get { return NaiveSummary; } }

    [Obsolete("Use NaiveStrikeExposures or GetStrikeExposures(OptionPositionModel). StrikeExposures is a compatibility alias for NaiveStrikeExposures.")]
    public List<OptionStrikeExposure> StrikeExposures { get { return NaiveStrikeExposures; } }

    public OptionExposureSummary NaiveSummary { get; private set; }

    public List<OptionStrikeExposure> NaiveStrikeExposures { get; private set; }

    public OptionExposureSummary FlowImpliedDealerSummary { get; private set; }

    public List<OptionStrikeExposure> FlowImpliedDealerStrikeExposures { get; private set; }

    public bool HasFlowImpliedDealerExposure { get; private set; }

    public OptionExposureSummary GetSummary(OptionPositionModel positionModel)
    {
        return positionModel == OptionPositionModel.FlowImpliedDealer
            ? FlowImpliedDealerSummary
            : NaiveSummary;
    }

    public List<OptionStrikeExposure> GetStrikeExposures(OptionPositionModel positionModel)
    {
        return positionModel == OptionPositionModel.FlowImpliedDealer
            ? FlowImpliedDealerStrikeExposures
            : NaiveStrikeExposures;
    }
}
