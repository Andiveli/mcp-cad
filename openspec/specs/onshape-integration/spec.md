# onshape-integration Specification

## Purpose

Onshape REST integration: HTTP Basic auth with API keys (OpenAPI `BasicAuth`), `did/wvm/wvmid/eid` addressing, microversion skew protection, STEP export polling, unit conversion.

## Requirements

### Requirement: BT Payload Shape for FeatureAdd

`FeatureAdd` MUST POST a wrapped BT `feature` envelope to the Onshape Part Studio features endpoint. The envelope MUST contain a `feature` object with `featureType`, `parameters`, and optional BT feature fields (`suppressed`, `name`, etc.). The POST body MUST NOT be the flat `{ featureType, parameters }` structure.

#### Scenario: Extrude POST body shape

- GIVEN a Part Studio with a sketch
- WHEN `FeatureAdd` is called with `featureType: "extrude"` and parameters for depth, direction, boolean
- THEN the POST body MUST contain a top-level `feature` object wrapping `featureType` and `parameters`
- AND `rejectMicroversionSkew` MUST be `true` at the top level
- AND a flat `{ featureType, parameters, ... }` body MUST NOT be sent

#### Scenario: Golden JSON conformance

- GIVEN a CSWP spike captures a real Onshape `FeatureAdd` request body
- WHEN `FeatureAdd` serializes its payload for the same operation
- THEN the serialized JSON MUST match the golden shape (microversion and IDs may differ)
- AND WireMock test MUST assert structural conformance

### Requirement: BTMSketch Payload for SketchReplace

`SketchReplace` MUST POST a valid BTMSketch JSON body to the Onshape Part Studio sketch endpoint. The body MUST contain sketch entities in the BTMSketch schema, and the current stub MUST be replaced with a real implementation.

#### Scenario: Rectangle sketch POST body

- GIVEN an empty sketch element
- WHEN `SketchReplace` is called with four line entities forming a rectangle
- THEN the POST body MUST be valid BTMSketch JSON
- AND the `Content-Type` header MUST be `application/json`
- AND `rejectMicroversionSkew` MUST be included per the existing microversion requirement

#### Scenario: Line sketch POST body

- GIVEN an empty sketch element
- WHEN `SketchReplace` is called with a single line entity
- THEN the POST body MUST be valid BTMSketch JSON
- AND the response MUST return success with an updated microversion

### Requirement: API Key Authentication (HTTP Basic)

Every request to `cad.onshape.com` MUST send `Authorization: Basic {Base64(UTF8(accessKey + ":" + secretKey))}` per Onshape OpenAPI `components.securitySchemes.BasicAuth`. When configured, HMAC request signing is an additional integrity layer and MUST NOT replace the Basic header.

#### Scenario: GET and POST

- GIVEN `GET /api/v6/documents`, the request MUST use HTTP Basic with the configured access key and secret
- GIVEN a POST with JSON body, the same Basic credentials MUST authenticate the request
- AND HMAC-signed requests MUST also carry the Basic header

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
