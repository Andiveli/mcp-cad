# Onshape parity review chain

This tracker is a draft, no-merge review index for [issue #11](https://github.com/Andiveli/mcp-cad/issues/11). Each child PR targets its predecessor, keeps its tests and documentation with the behavior, and states its own verification limits. Do not merge this tracker until the children are reviewed and integrated.

## Review order

| Slice | Starts with | Ends with | Status |
| --- | --- | --- | --- |
| 1. Core web-provider contract | Existing `main` contracts | Addresses, typed errors, contract tests and provider guide | Local candidate; Linux Core tests 9/9 and Core build passed |
| 2. HTTP client and authentication | Verified Core contract | Outgoing Basic-auth requests and focused wire tests | Not reconstructed |
| 3. Provider and operations | Verified client | Provider, managers, builders, tools and matching tests/docs in smaller dependent slices | Not reconstructed |

Each child PR will identify its exact parent, next dependency, rollback boundary, additions plus deletions, and skipped or unavailable checks. The historical 16,214-line diff is not an integrated-base PR budget. Windows/Inventor tests and live Onshape behavior have not been verified in the isolated reconstruction; no full-parity claim follows from the Core slice.

## Dependency diagram

```text
main → [draft tracker] → [📍 Core contract] → [client/auth] → [provider and operation slices]
```

The active marker moves to the child currently under review. Issues #8–#10 describe later work that depends on this base and are not closed by this chain.
