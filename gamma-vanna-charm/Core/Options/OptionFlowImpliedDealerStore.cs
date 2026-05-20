using System;
using System.Collections.Generic;
using GammaVannaCharm.Core.Exposure;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionFlowImpliedDealerStore
{
    private const double IvChangeThreshold = 0.0001;
    private const double IvRisingBuyScore = 0.4;
    private const double IvConfidenceIncrement = 0.25;
    private const double OpenInterestConfirmingConfidenceIncrement = 0.20;
    private const double OpenInterestFallingConfidencePenalty = 0.10;
    private const double SideScoreEpsilon = 0.0000001;
    private const double QuoteEdgeToleranceSpreadRatio = 0.10;
    private const double MinimumQuoteEdgeTolerance = 0.01;
    private const double AtQuoteConfidence = 0.55;
    private const double InsideMarketConfidence = 0.35;

    private readonly Dictionary<string, ContractObservation> _previousObservations = new Dictionary<string, ContractObservation>(StringComparer.Ordinal);
    private readonly Dictionary<string, MutableDealerState> _dealerStates = new Dictionary<string, MutableDealerState>(StringComparer.Ordinal);
    private readonly List<OptionFlowInterval> _lastIntervals = new List<OptionFlowInterval>();
    private DateTime _lastSnapshotTimestampUtc = DateTime.MinValue;
    private bool _hasSeededBaseline;
    private bool _hasFlowIntervals;

    public DateTime LastSnapshotTimestampUtc
    {
        get { return _lastSnapshotTimestampUtc; }
    }

    public IReadOnlyList<OptionFlowInterval> LastIntervals
    {
        get { return _lastIntervals.AsReadOnly(); }
    }

    public OptionImpliedDealerSnapshot Update(OptionChainSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Contracts == null)
            throw new ArgumentNullException(nameof(snapshot.Contracts));

        if (_lastSnapshotTimestampUtc != DateTime.MinValue && snapshot.TimestampUtc.Date != _lastSnapshotTimestampUtc.Date)
            Clear();

        _lastIntervals.Clear();

        var currentObservations = new Dictionary<string, ContractObservation>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.Contracts.Count; index++)
        {
            var contract = snapshot.Contracts[index];
            if (contract == null || string.IsNullOrWhiteSpace(contract.Symbol))
                continue;

            var current = ContractObservation.FromContract(contract, snapshot.TimestampUtc);
            currentObservations[contract.Symbol] = current;

            MutableDealerState state;
            var hasExistingDealerState = _dealerStates.TryGetValue(contract.Symbol, out state);
            if (!hasExistingDealerState)
            {
                state = new MutableDealerState(contract.Symbol);
                _dealerStates.Add(contract.Symbol, state);
            }

            ContractObservation previous;
            if (!_previousObservations.TryGetValue(contract.Symbol, out previous))
            {
                if (!hasExistingDealerState)
                    SeedDealerState(contract, state);
                continue;
            }

            var volumeDelta = GetVolumeDelta(previous, current);
            if (volumeDelta <= 0L)
                continue;

            double confidence;
            var side = EstimateCustomerSide(previous, current, out confidence);
            if (side == 0 || confidence <= 0.0)
                continue;

            ApplyDealerFlow(contract.Symbol, previous.TimestampUtc, current.TimestampUtc, volumeDelta, side, confidence, state);
        }

        _previousObservations.Clear();
        foreach (var pair in currentObservations)
            _previousObservations.Add(pair.Key, pair.Value);

        _lastSnapshotTimestampUtc = snapshot.TimestampUtc;
        return CreateSnapshot();
    }

    public OptionImpliedDealerSnapshot CreateSnapshot()
    {
        var states = new Dictionary<string, OptionImpliedDealerState>(StringComparer.Ordinal);
        foreach (var pair in _dealerStates)
        {
            var state = pair.Value;
            states[pair.Key] = new OptionImpliedDealerState(state.Symbol, state.DealerContracts, state.Confidence);
        }

        return new OptionImpliedDealerSnapshot(_lastSnapshotTimestampUtc, _hasSeededBaseline, _hasFlowIntervals, states);
    }

    public bool Hydrate(OptionImpliedDealerSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.HasDealerState)
            return false;

        Clear();
        foreach (var dealerState in snapshot.States)
        {
            if (dealerState == null || string.IsNullOrWhiteSpace(dealerState.Symbol))
                continue;

            var state = new MutableDealerState(dealerState.Symbol)
            {
                DealerContracts = dealerState.DealerContracts,
                Confidence = dealerState.Confidence,
                ConfidenceWeight = dealerState.Confidence > 0.0 ? 1.0 : 0.0
            };
            _dealerStates[state.Symbol] = state;
        }

        if (_dealerStates.Count == 0)
            return false;

        _lastSnapshotTimestampUtc = snapshot.TimestampUtc;
        _hasSeededBaseline = snapshot.HasSeededBaseline;
        _hasFlowIntervals = snapshot.HasFlowIntervals;
        return true;
    }

    public void Clear()
    {
        _previousObservations.Clear();
        _dealerStates.Clear();
        _lastIntervals.Clear();
        _lastSnapshotTimestampUtc = DateTime.MinValue;
        _hasSeededBaseline = false;
        _hasFlowIntervals = false;
    }

    private void SeedDealerState(OptionContractData contract, MutableDealerState state)
    {
        if (contract == null || state == null || contract.OpenInterest <= 0L)
            return;

        // Seeded FLOW starts with the same transparent OI assumption as the naive model
        // (dealer long calls, dealer short puts), then subsequent poll-to-poll classified
        // volume changes adjust this signed contract state.
        state.DealerContracts = contract.Right == OptionRight.Put ? -contract.OpenInterest : contract.OpenInterest;
        _hasSeededBaseline = true;
    }

    private static long GetVolumeDelta(ContractObservation previous, ContractObservation current)
    {
        // The store calls Clear() at the top of Update() when the snapshot date changes,
        // so cross-day deltas never reach here. A negative cumulative-volume delta on the
        // same day means the feed corrected itself; we ignore it rather than classify noise.
        if (current.Volume >= previous.Volume)
            return current.Volume - previous.Volume;

        return 0L;
    }

    private void ApplyDealerFlow(string symbol, DateTime startTimestampUtc, DateTime endTimestampUtc, long volumeDelta, int customerSide, double confidence, MutableDealerState state)
    {
        // Customer buying implies dealers sold options; customer selling implies dealers bought options.
        var dealerContractsDelta = -customerSide * volumeDelta * confidence;
        state.DealerContracts += dealerContractsDelta;
        // Track a volume-weighted average classifier confidence per symbol so the surfaced
        // value is meaningful instead of "whatever the last interval reported."
        var weight = (double)volumeDelta;
        var newWeight = state.ConfidenceWeight + weight;
        if (newWeight > 0.0)
        {
            state.Confidence = ((state.Confidence * state.ConfidenceWeight) + (confidence * weight)) / newWeight;
            state.ConfidenceWeight = newWeight;
        }
        _lastIntervals.Add(new OptionFlowInterval(
            symbol,
            startTimestampUtc,
            endTimestampUtc,
            volumeDelta,
            dealerContractsDelta,
            confidence));
        _hasFlowIntervals = true;
    }

    private static int EstimateCustomerSide(ContractObservation previous, ContractObservation current, out double confidence)
    {
        var score = (double)EstimateCustomerSideFromQuote(current, out confidence);

        var ivDelta = current.ImpliedVolatility - previous.ImpliedVolatility;
        if (Math.Abs(ivDelta) > IvChangeThreshold)
        {
            score += ivDelta > 0.0 ? IvRisingBuyScore : -IvRisingBuyScore;
            confidence += IvConfidenceIncrement;
        }

        var openInterestDelta = current.OpenInterest - previous.OpenInterest;
        if (openInterestDelta > 0L && Math.Abs(score) > 0.0)
            confidence += OpenInterestConfirmingConfidenceIncrement;
        else if (openInterestDelta < 0L)
            confidence -= OpenInterestFallingConfidencePenalty;

        confidence = Math.Max(0.0, Math.Min(1.0, confidence));
        if (Math.Abs(score) < SideScoreEpsilon)
            return 0;

        return score > 0.0 ? 1 : -1;
    }

    private static int EstimateCustomerSideFromQuote(ContractObservation current, out double confidence)
    {
        confidence = 0.0;
        if (current.Bid <= 0.0 || current.Ask <= 0.0 || current.Ask < current.Bid || current.LastPrice <= 0.0)
            return 0;

        var spread = current.Ask - current.Bid;
        var midpoint = (current.Bid + current.Ask) / 2.0;
        var edgeTolerance = Math.Max(spread * QuoteEdgeToleranceSpreadRatio, MinimumQuoteEdgeTolerance);
        if (current.LastPrice >= current.Ask - edgeTolerance)
        {
            confidence = AtQuoteConfidence;
            return 1;
        }
        if (current.LastPrice <= current.Bid + edgeTolerance)
        {
            confidence = AtQuoteConfidence;
            return -1;
        }
        if (current.LastPrice > midpoint)
        {
            confidence = InsideMarketConfidence;
            return 1;
        }
        if (current.LastPrice < midpoint)
        {
            confidence = InsideMarketConfidence;
            return -1;
        }

        return 0;
    }

    private sealed class ContractObservation
    {
        public static ContractObservation FromContract(OptionContractData contract, DateTime timestampUtc)
        {
            return new ContractObservation
            {
                TimestampUtc = timestampUtc,
                OpenInterest = contract.OpenInterest,
                Volume = contract.Volume,
                ImpliedVolatility = contract.ImpliedVolatility,
                Bid = contract.Bid,
                Ask = contract.Ask,
                LastPrice = contract.LastPrice
            };
        }

        public DateTime TimestampUtc { get; set; }

        public long OpenInterest { get; set; }

        public long Volume { get; set; }

        public double ImpliedVolatility { get; set; }

        public double Bid { get; set; }

        public double Ask { get; set; }

        public double LastPrice { get; set; }
    }

    private sealed class MutableDealerState
    {
        public MutableDealerState(string symbol)
        {
            Symbol = symbol;
        }

        public string Symbol { get; private set; }

        public double DealerContracts { get; set; }

        public double Confidence { get; set; }

        public double ConfidenceWeight { get; set; }
    }
}
