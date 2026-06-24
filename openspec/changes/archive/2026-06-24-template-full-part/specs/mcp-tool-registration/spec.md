# Delta for mcp-tool-registration

## ADDED Requirements

### Requirement: template_capture Feature Tree Capture

The system MUST document that template_capture now captures the full feature tree alongside sketch entities. The capture result MUST include feature_reader_warnings when unsupported feature types are encountered.

#### Scenario: Captured features in template JSON

- GIVEN a part with 3 features (extrude, fillet, hole)
- WHEN template_capture runs
- THEN the template JSON includes a features[] array
- AND each entry contains feature_type and typed parameters

#### Scenario: FeatureReader warnings in capture result

- GIVEN a part containing an unsupported iFeature
- WHEN template_capture runs
- THEN the result includes feature_reader_warnings
- AND capture completes with partial success

### Requirement: template_run Full Part Replay

The system MUST document that template_run replays the features[] array via macro_god_part dispatch. Old templates without features[] MUST replay identically to pre-change behavior.

#### Scenario: Full part template replays correctly

- GIVEN a template with sketches[] and features[]
- WHEN template_run is called with parameter overrides
- THEN all features are recreated in creation order
- AND geometry matches the original

#### Scenario: Old template without features[] unchanged

- GIVEN a pre-change template without features[]
- WHEN template_run is called
- THEN replay uses the single-feature path
- AND result is identical to pre-change output
