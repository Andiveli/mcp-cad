# CAD provider contracts

The existing `IMechanicalCadProvider` defines the desktop CAD operations. `ICadWebProvider` is a sibling interface extending `ICadProvider`, not a replacement for the mechanical interface. It declares cloud-provider operations for authentication, document workspaces, elements, versions, exports, features, and assemblies.

`CadElementAddress` identifies a web CAD element by document ID, workspace or version ID, optional microversion ID, and element ID. Its JSON representation uses camel-case keys and omits a null microversion ID.

Onshape failures are represented by `CadProviderException` subclasses. `OnshapeApiException.FromHttpStatus` maps 401/403 to `OnshapeAuthException`, 409 to `OnshapeMicroversionSkewException`, and 429 to `OnshapeQuotaException` (including an optional retry interval). Other status codes produce `OnshapeApiException` with the status and reason.

This slice defines contracts and error types only. It does not register an Onshape backend, configure credentials, or change the server's active provider. Future provider integration must implement the contract and document runtime configuration when available.
