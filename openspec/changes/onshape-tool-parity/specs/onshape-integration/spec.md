# Delta for onshape-integration

## ADDED Requirements

### Requirement: BT Payload Shape for FeatureAdd

`FeatureAdd` MUST POST a wrapped BT `feature` envelope to the Onshape Part Studio features endpoint. The envelope MUST contain a `feature` object with `featureType`, `parameters`, and optional BT feature fields (`suppressed`, `name`, etc.). The POST body MUST NOT be the flat `{ featureType, parameters }` structure.

The body shape SHALL be:

```json
{
  "feature": {
    "featureType": "<string>",
    "parameters": [ ... ],
    "suppressed": false
  },
  "rejectMicroversionSkew": true
}
```

#### Scenario: Extrude POST body shape

- GIVEN a Part Studio with a sketch
- WHEN `FeatureAdd` is called with `featureType: "extrude"` and parameters for depth, direction, boolean
- THEN the POST body MUST contain a top-level `feature` object wrapping `featureType` and `parameters`
- AND `rejectMicroversionSkew` MUST be `true` at the top level
- AND a flat `{ featureType, parameters, ... }` body MUST be rejected by the implementation (it MUST NOT be sent)

#### Scenario: Golden JSON conformance

- GIVEN a CSWP spike captures a real Onshape `FeatureAdd` request body
- WHEN `FeatureAdd` serializes its payload for the same operation
- THEN the serialized JSON MUST match the golden shape (microversion and IDs may differ)
- AND WireMock test MUST assert structural conformance

### Requirement: BTMSketch Payload for SketchReplace

`SketchReplace` MUST POST a valid BTMSketch JSON body to the Onshape Part Studio sketch endpoint. The body MUST contain sketch entities (lines, circles, rectangles) in the BTMSketch schema. The current stub (`SketchReplaceStub`) MUST be replaced with a real implementation that constructs and sends BTMSketch JSON.

#### Scenario: Rectangle sketch POST body

- GIVEN an empty sketch element
- WHEN `SketchReplace` is called with four line entities forming a rectangle
- THEN the POST body MUST be valid BTMSketch JSON containing entities with `entityType`, `startPoint`, `endPoint`, and geometry parameters
- AND the `Content-Type` header MUST be `application/json`
- AND `rejectMicroversionSkew` MUST be included per the existing microversion requirement

#### Scenario: Line sketch POST body

- GIVEN an empty sketch element
- WHEN `SketchReplace` is called with a single line entity
- THEN the POST body MUST be valid BTMSketch JSON
- AND the response MUST return success with an updated microversion

## MODIFIED Requirements

### Requirement: API Key Authentication (HTTP Basic)

(Previously: "Every request to cad.onshape.com MUST send Authorization: Basic")
Updated to reflect that the provider requires a **Basic overlay + HMAC config** approach: the `OnshapeClient` uses Basic auth for the `Authorization` header but SHALL also support HMAC request signing when configured by the Onshape API key pattern. Both approaches MUST resolve to the same `accessKey`/`secretKey` credential pair.

The requirement remains: **every request to `cad.onshape.com` MUST send `Authorization: Basic {Base64(UTF8(accessKey + ":" + secretKey))}`** per Onshape OpenAPI `BasicAuth`. HMAC signing is an additional layer for request integrity; it does not replace the Basic auth header.

#### Scenario: Basic auth header always present

- GIVEN any request to `cad.onshape.com`
- WHEN the request is inspected
- THEN `Authorization: Basic` header MUST be present with correct Base64 encoding
- AND HMAC-signed requests MUST also carry this header (HMAC does not replace Basic)
