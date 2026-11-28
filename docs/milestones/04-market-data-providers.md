# Choosing a daily price provider for Milestone 4

**Decision proposed 26 November 2026; approved by the owner 28 November 2026.** Adopt Massive Stocks Basic for a personal local trial with US stocks. One request can retrieve the completed daily bars for one ticker and date window. Viewing those stored bars costs no further requests. The initial integration needs no brokerage account.

| Provider and tier | Cost | Equity coverage | Daily history available on this tier | Published usage limit | Implication for this workflow |
| --- | --- | --- | --- | --- | --- |
| Massive Stocks Basic | $0/month, individual | US stocks | Two rolling years of end-of-day aggregates. Custom start and end dates. | 5 calls/minute | Fits the recent-window import and repeat test. No London-listed ordinary shares. |
| Alpha Vantage free | $0 | Global equities. Its daily endpoint documents US, London and Toronto examples. | Latest 100 observations from the raw daily series. Full history requires premium. | Standard free allowance of 25 requests/day | A London candidate if recent history suffices. Filtering the compact response cannot retrieve older dates. |
| Marketstack Free | $0/month, non-commercial | International end-of-day coverage. Check each required listing and exchange on the chosen tier. | Up to one year of end-of-day history | 100 requests/month | A candidate for international windows longer than 100 sessions. Repeated manual imports and paged requests consume the monthly allowance. |

Plan and endpoint references: [Massive Stocks pricing](https://massive.com/pricing?product=stocks) · [Massive Custom Bars](https://massive.com/docs/rest/stocks/aggregates/custom-bars) · [Alpha Vantage daily API](https://www.alphavantage.co/documentation/#daily) · [Alpha Vantage free allowance](https://www.alphavantage.co/support/api-key/) · [Marketstack coverage](https://marketstack.com/) · [Marketstack pricing](https://marketstack.com/pricing/)

## Why Massive for this milestone

The app needs date-range history and repeatable reads. Two years gives enough room to test distinct and overlapping windows. Five requests per minute accommodates a single user importing one stock at a time. The app should tell the user when to retry if that allowance is exhausted. The daily endpoint documents inclusive dates, an explicit split-adjustment switch and gaps when no trades qualify. Request unadjusted values and label them so a split does not look like a data-entry error. Back-adjusted research series can be specified later. [Endpoint and data semantics](https://massive.com/docs/rest/stocks/aggregates/custom-bars)

The individual market-data terms grant private downloading and storage for the subscriber’s own personal, non-business use, subject to eligibility and applicable third-party terms. They restrict sharing and provide deletion requirements when the entitlement ends. Keep live price rows local. Use invented data for committed examples, tests and screenshots shared with reviewers. Recheck rights before sharing a service or changing the account plan. [Market data terms, sections 1, 3 and 8](https://massive.com/legal/market-data-terms-of-service) · [Individual account terms](https://massive.com/legal/individuals-terms-of-service)

## Owner decision

**Accept the US pilot** if Apple and Microsoft are suitable first listings. No payment or broker credentials are required. Obtain an eligible personal Stocks Basic key for the live smoke and put it in the ignored backend configuration. The key can be added after Copilot has implemented and tested the offline paths. [Massive REST authentication](https://massive.com/docs/rest/quickstart)

**If London coverage is needed now**, choose the target London listings and required lookback before invoking the US implementation prompt. For a recent 100-session view, evaluate Alpha Vantage first. For a full year, validate a Marketstack Free sample for those exact instruments. Verify exchange, symbol, price units (GBp or GBP), daily dates and storage rights for the selected plan before amending the adapter specification. Exchange-directory entries alone do not establish price-history coverage.

No live credential or market-data request was used for this planning comparison.
