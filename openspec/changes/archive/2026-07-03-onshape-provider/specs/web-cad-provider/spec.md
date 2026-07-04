# web-cad-provider Specification

## Purpose

`ICadWebProvider` contract for cloud-native CAD backends (Onshape, future Fusion 360 / 3DExperience). Sibling of `IMechanicalCadProvider` — keeps REST, HMAC, microversion, and export concerns out of the COM-bound surface.

## Requirements

### Requirement: Sibling Interface Contract

`ICadWebProvider` MUST extend `ICadProvider` (NOT `IMechanicalCadProvider`) and add: element addressing, microversion negotiation, async export polling, unit-conversion hooks.

#### Scenario: Sibling cast fails, base succeeds

- GIVEN `OnshapeProvider : ICadWebProvider`
- WHEN cast to `IMechanicalCadProvider`
- THEN it MUST fail (siblings, not parent/child); cast to `ICadProvider` MUST succeed

### Requirement: Exception Hierarchy

`CadProviderException` MUST be the base. Subclasses: `OnshapeApiException`, `OnshapeAuthException` (401/403), `OnshapeQuotaException` (429, carries `Retry-After`).

#### Scenario: 429 and 401 typed correctly

- GIVEN a 429 response, the exception MUST be `OnshapeQuotaException`
- GIVEN a 401, the exception MUST be `OnshapeAuthException` (never `HttpRequestException`)

### Requirement: Element Address Value Type

`CadElementAddress` MUST carry `DocumentId`, `WorkspaceOrVersionId`, `MicroversionId` (optional), `ElementId`. Providers MUST accept/return this — never raw strings.

#### Scenario: Round-trip with optional microversion

- GIVEN all four fields populated, serialized, deserialized
- THEN all MUST round-trip exactly; missing `MicroversionId` on a read MUST omit `If-Match` (not send empty string)
