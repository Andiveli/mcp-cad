# onshape-integration Specification

## Purpose

Onshape REST integration: HMAC-SHA256 signing, `did/wvm/wvmid/eid` addressing, microversion skew protection, STEP export polling, unit conversion.

## Requirements

### Requirement: HMAC-SHA256 Request Signing

Every request to `cad.onshape.com` MUST send `Authorization: OnshapeKey {AccessKey}:{Base64HmacSha256(...)}`, `On-Nonce`, `On-Date`. Canonical: `METHOD\nURI\nnonce\ndate\ncontentType\nbody`.

#### Scenario: GET and POST signing

- GIVEN `GET /api/v6/documents`, headers MUST contain valid `Authorization`, `On-Nonce`, `On-Date`
- GIVEN a POST with 240-byte body, the body MUST appear in the canonical string; signing with empty body MUST fail server check

### Requirement: Microversion Skew Protection

Writes MUST include `rejectMicroversionSkew=true`. A 409 `wrongMicroversion` MUST surface as `OnshapeMicroversionSkewException` carrying `ServerMicroversion`. Reads MUST NOT include the flag.

#### Scenario: Stale write, read opt-out

- GIVEN a write with stale microversion returns 409, exception MUST carry `ServerMicroversion`
- GIVEN `GET /api/v6/elements/...`, `rejectMicroversionSkew` MUST be absent

### Requirement: STEP Export Polling

STEP exports MUST be polled via `GET /translations/{tid}` every 2s up to 120s.

#### Scenario: Completion or timeout

- GIVEN translation starts `PENDING`, when state becomes `DONE` the download URL and STEP bytes MUST be returned
- WHEN state stays `PENDING` past 120s it MUST throw `OnshapeApiException` reason `export_timeout`; no partial download

### Requirement: Unit Conversion at Provider Boundary

Host = cm; Onshape API = m. Inputs MUST convert cm→m on write, m→cm on read.

#### Scenario: Write cm, read m

- GIVEN a 5.0 cm edge, the request body MUST contain `0.05`
- GIVEN response contains `0.030` m, the returned value MUST be `3.0` cm at host precision
