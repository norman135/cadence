# 0010. Fractional indexing for board ordering, and moves as one guarded UPDATE

- Status: Accepted
- Date: 2026-10-13

## Context

Cards on the board (and, from M6, issues in the backlog) have a manual order that people change all the time by dragging. The order must survive concurrent edits, and the M4 gate is that a move persists with p95 ≤ 50 ms on a small server.

The usual choices for storing a manual order:

- **Integer positions** (1, 2, 3…). Moving a card renumbers every card between its old and new places: up to hundreds of rows per drag, more locks, and every renumbered row changes under other people's feet.
- **Positions with gaps** (1000, 2000…). Cheaper until a gap runs out, then the same renumbering, at unpredictable moments.
- **A linked list** (previous and next card). One move touches three rows, and reading the order needs a recursive query.
- **Fractional indexing**: each card has a string key, and a key always fits between two others. A move writes one row.

Moves also need conflict handling: two people may drag the same card at once, and transition rules (ADR-0017) may depend on the card's current status and assignee.

## Decision

**Ranks are fractional-index keys** (`issues.rank`), following the widely used fractional-indexing scheme:

- A key is an integer part whose first character encodes its length (`a`–`z` for 2–27 characters, `A`–`Z` for negative integers), then a base-62 fraction that never ends in `0`. Keys compare byte by byte, so the column uses the `"C"` collation.
- Appending after the last card increments the integer, so keys stay short: 10,000 appends end at four characters.
- Inserting between two cards extends the fraction.
- The algorithm lives in the domain (`Rank`) and in the web client (`shared/lib/rank.ts`), checked against the same test vectors. The server validates every key it receives.

**The rank is project-wide**, not per column. A card that changes status keeps a meaningful place, the board reads each column by rank, and the backlog (M6) uses the same order.

**The client computes the new rank** from the neighbours it sees at the drop position:

- In a swimlane, it uses the lane neighbours, bounded by the next card in the whole column.
- At the end of a partly shown column, it uses the first hidden card's rank, which the server returns as `nextRank`.
- Equal ranks (two concurrent creates) are stepped over.

**A move is one UPDATE.** `POST /projects/{id}/board/moves` sends what the client saw: the status, the row version (PostgreSQL's `xmin`) and the assignee. The statement's WHERE clause checks all of it, and its RETURNING clause hands back the new version.

- Zero rows means something changed. Only then does a second query explain why: a missing issue, an archived project, or 409 `issues.changed`.
- On 409 the client puts the card back, reloads the board and says so.
- Transition rules come from the cached workflow, so a move with warm caches costs exactly one statement. Counters are recomputed only when a move opens or closes an issue.

**A background rebalancer keeps keys short.** Every five minutes it rewrites the ranks of any project with a key longer than 12 characters to evenly spaced keys in the same order. A partial index makes the check free when no project needs it.

## Consequences

- Moves are cheap and constant-cost regardless of column length. Measured at 10 users over 10,000 issues: see performance.md.
- Keys can grow when people keep dropping cards into the same spot. The rebalancer bounds that, at the cost of changing many rows' versions at once; clients holding older versions then get a 409 on their next move and reload. This is rare.
- Two clients computing a rank between the same neighbours at the same moment can produce equal ranks. Ordering then falls back to the issue id, and the next move between them separates them.
- The client must implement the same algorithm as the server. The shared test vectors catch drift.
