using System;
using System.Collections.Generic;

namespace GammaVannaCharm.Core.Options;

public sealed class OptionImpliedDealerSnapshot
{
    private readonly Dictionary<string, OptionImpliedDealerState> _statesBySymbol;

    public OptionImpliedDealerSnapshot(DateTime timestampUtc, bool hasFlowIntervals, IDictionary<string, OptionImpliedDealerState> statesBySymbol)
        : this(timestampUtc, false, hasFlowIntervals, statesBySymbol)
    {
    }

    public OptionImpliedDealerSnapshot(DateTime timestampUtc, bool hasSeededBaseline, bool hasFlowIntervals, IDictionary<string, OptionImpliedDealerState> statesBySymbol)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc && timestampUtc != DateTime.MinValue)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));

        TimestampUtc = timestampUtc;
        HasSeededBaseline = hasSeededBaseline;
        HasFlowIntervals = hasFlowIntervals;
        _statesBySymbol = new Dictionary<string, OptionImpliedDealerState>(StringComparer.Ordinal);
        if (statesBySymbol == null)
            return;

        foreach (var pair in statesBySymbol)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                _statesBySymbol[pair.Key] = pair.Value;
        }
    }

    public DateTime TimestampUtc { get; private set; }

    public bool HasSeededBaseline { get; private set; }

    public bool HasFlowIntervals { get; private set; }

    public bool HasDealerState
    {
        get
        {
            if (_statesBySymbol.Count == 0)
                return false;

            foreach (var state in _statesBySymbol.Values)
            {
                if (state != null && (Math.Abs(state.DealerContracts) > 0.0000001 || state.Confidence > 0.0))
                    return true;
            }

            return false;
        }
    }

    public IReadOnlyCollection<OptionImpliedDealerState> States
    {
        get { return _statesBySymbol.Values; }
    }

    public bool TryGetDealerContracts(string contractSymbol, out double dealerContracts)
    {
        dealerContracts = 0.0;
        if (string.IsNullOrWhiteSpace(contractSymbol))
            return false;

        OptionImpliedDealerState state;
        if (!_statesBySymbol.TryGetValue(contractSymbol, out state) || state == null)
            return false;

        dealerContracts = state.DealerContracts;
        return Math.Abs(dealerContracts) > 0.0000001;
    }
}
