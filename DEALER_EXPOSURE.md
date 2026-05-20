### Options and Their Influence on the Underlying

Even if you do not trade options, options positioning can influence the underlying price. The key idea is that market makers/dealers often hedge option risk with the underlying. As price, implied volatility, and time change, those hedges may need to be adjusted. Depending on the dealer's net exposure, hedging can either suppress moves or amplify them.

**How Options Move the Markets:** https://youtu.be/0oJqC9QK-I0?si=YM_tOxYeLYYIEnuY

#### Important Caveats

- Dealer exposure is inferred, not directly observed. It is a useful framework, not a guaranteed prediction.
- The sign depends on the model and data source. Naive open-interest models make assumptions about who owns calls/puts; flow-implied models try to infer the dealer side from trading activity.
- Hedging is not automatic tick-for-tick. Dealers may be partially hedged, hedged with other options/futures, constrained by liquidity, or offset by other inventory.
- Gamma is usually the primary price-response layer. Vanna and charm are best used as context, especially when IV is repricing or expiration is near.

#### Key Terms

**Gamma:** Measures how quickly an option's delta changes as the underlying price moves. High gamma means delta changes rapidly.

**Vanna:** Measures how an option's delta changes when implied volatility changes. High vanna means delta is very sensitive to volatility repricing.

**Charm:** Measures how an option's delta changes as time passes. Charm is often called delta decay; its sign and effect can vary by option type, strike, expiration, and convention.

#### Hedging Options

**Initial Delta Hedge:** When dealers trade options, they may buy or sell the underlying to offset the option delta created by the trade.

**Dynamic Hedging:** As the underlying price, time, and implied volatility change, dealers may adjust their underlying hedge based on gamma, vanna, and charm.

#### Dealer Gamma Positions

Gamma affects how much dealers buy or sell the underlying as price changes.

**Positive Gamma (Long Gamma):**

**Effect on Market:** Tends to dampen large moves because hedging activity leans against the direction of price changes.

**Hedging Flows:**

- When the underlying rises, a long-gamma dealer sells the underlying to stay delta-neutral.
- When the underlying falls, a long-gamma dealer buys the underlying to stay delta-neutral.

**Negative Gamma (Short Gamma):**

**Effect on Market:** Tends to exacerbate moves because hedging activity follows the direction of price changes.

**Hedging Flows:**

- When the underlying rises, a short-gamma dealer buys the underlying to stay delta-neutral.
- When the underlying falls, a short-gamma dealer sells the underlying to stay delta-neutral.

#### Dealer Vanna Positions

Vanna affects how dealers respond to changes in implied volatility. Whether this stabilizes or destabilizes price depends on how IV is moving relative to spot. For example, in equity indices, IV often rises when price falls, so the same vanna flow can have a different market effect than in a rally where IV is falling.

**Positive Vanna (Long Vanna):**

**Hedging Flows:**

- When implied volatility rises, dealer option delta tends to increase, so the dealer may sell the underlying to stay delta-neutral.
- When implied volatility falls, dealer option delta tends to decrease, so the dealer may buy the underlying to stay delta-neutral.

**Negative Vanna (Short Vanna):**

**Hedging Flows:**

- When implied volatility rises, dealer option delta tends to decrease, so the dealer may buy the underlying to stay delta-neutral.
- When implied volatility falls, dealer option delta tends to increase, so the dealer may sell the underlying to stay delta-neutral.

#### Dealer Charm Positions

Charm captures how dealers may need to adjust hedges as time passes. Charm is most relevant near expiration, especially for 0DTE options and late-session trading.

Because charm sign conventions can differ, the safest read is:

- If dealer option delta increases as time passes, the dealer may sell the underlying to stay delta-neutral.
- If dealer option delta decreases as time passes, the dealer may buy the underlying to stay delta-neutral.

**Positive Charm (Long Charm):**

**Effect on Market:** Can create steady, time-driven hedge pressure. The direction depends on the sign convention used by the exposure model.

**Negative Charm (Short Charm):**

**Effect on Market:** Can contribute to intraday or overnight volatility when a large position requires continuous rebalancing as expiration approaches.

#### Standard Dealer Hedging Logic

This is a simplified table. Hedge positions can be reduced, offset, or changed by other inventory, spreads, futures hedges, or later option trades.

| Investor Action  | Dealer Reaction  | Dealer Initial Hedge              | Position Closed or Expired     |
| ---------------- | ---------------- | --------------------------------- | ------------------------------ |
| Buy Call Option  | Sell Call Option | Buy Underlying (hedge short call) | Sell Underlying (remove hedge) |
| Buy Put Option   | Sell Put Option  | Sell Underlying (hedge short put) | Buy Underlying (remove hedge)  |
| Sell Call Option | Buy Call Option  | Sell Underlying (hedge long call) | Buy Underlying (remove hedge)  |
| Sell Put Option  | Buy Put Option   | Buy Underlying (hedge long put)   | Sell Underlying (remove hedge) |

#### Practical Suggestions

- Treat positive gamma zones as potential pin/mean-reversion areas and negative gamma zones as potential expansion/continuation areas.
- Use vanna only when implied volatility is actively repricing; without IV movement, vanna pressure may not matter much.
- Use charm mostly near expiration or late in the session, when time decay can meaningfully change delta.
- Do not trade exposure levels alone. Wait for price acceptance/rejection, volume, volatility context, and risk-defined entries.
