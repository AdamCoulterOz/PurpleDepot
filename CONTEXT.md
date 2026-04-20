# PurpleDepot Context

## Purpose and Current State

PurpleDepot is a Terraform private registry implementation. The current codebase exposes Terraform module and provider discovery endpoints through an Azure Functions host, with shared domain and controller logic in `Core` and Azure-specific hosting and storage in `Providers/Azure`.

Current work in flight:

- .NET projects have been upgraded to `net10.0`
- Azure provider and Terraform module provider versions have been refreshed
- The default branch has been renamed from `master` to `main`
- `PurpleDepot.slnx` has been added for solution-level builds
- Provider package metadata, checksum, and signature endpoints now exist for the Terraform provider registry protocol
- Provider package uploads are now platform-aware at `v1/providers/{namespace}/{name}/{version}/upload/{os}/{arch}`
- Mock-backed downloads now round-trip through a hosted archive proxy at `v1/archive/{fileKey}`
- `Tests/PurpleDepot.Tests` now covers mock storage behavior and provider package controller behavior
- `ARCHITECTURE_REVIEW.md` captures the current protocol and architecture assessment

## Architecture and Structure

- `Core/Interface/Model`: registry contracts, addresses, route metadata, and serialized response models
- `Core/Controller`: application logic for service discovery, module/provider lookup, ingest, and download response construction
- `Core/Controller/Data`: Entity Framework Core persistence abstractions and the shared `AppContext`
- `Core/Controller/IProviderPackageSigner.cs`: signing abstraction used to produce detached checksum signatures for provider packages
- `Core/Interface/Storage`: storage abstraction plus the in-memory mock implementation
- `Providers/Azure/DotNet`: Azure Functions isolated worker host, HTTP endpoints, DI setup, and Azure Blob-backed storage
- `Providers/Azure/DotNet/OpenPgpProviderPackageSigner.cs`: Azure-hosted OpenPGP detached signature implementation for provider checksum documents
- `Providers/Azure/DotNet/Host/ArtifactApi.cs`: host-side archive proxy used for dev/mock download URLs
- `Providers/Azure/Module`: Terraform module for deploying the Azure-hosted registry
- `Providers/Azure/Test/Module`: Terraform-based infrastructure smoke test inputs
- `Providers/Azure/Scripts/terraform-provider.yml`: Azure Pipelines helper to publish platform-specific provider packages
- `Tests/PurpleDepot.Tests`: xUnit coverage for storage and provider registry controller behavior

## Key Decisions and Invariants

- Service discovery is driven by `IRoutes` implementations and emitted at `/.well-known/terraform.json`.
- Module downloads currently follow the Terraform registry redirect pattern using `204` plus `X-Terraform-Get`.
- Terraform provider package installation now uses `GET v1/providers/{namespace}/{name}/{version}/download/{os}/{arch}` plus companion checksum and detached-signature endpoints.
- Terraform provider package publication now uses `POST v1/providers/{namespace}/{name}/{version}/upload/{os}/{arch}` with `X-Terraform-Protocols`.
- Provider package signing requires `PurpleDepot:ProviderSigning:PrivateKey` and optionally `PurpleDepot:ProviderSigning:Passphrase`.
- Provider package metadata is only valid when the stored provider version includes `protocols` and the requested `platforms` entry.
- Storage and persistence stay abstract behind `IStorageProvider<T>` and `IRepository<T>`.
- Mock-backed download links are normalized into hosted `v1/archive/...` URLs before they are exposed to Terraform clients.
- The Azure Functions host now treats missing `PurpleDepot` configuration as a startup error instead of continuing with a null configuration object.

## Outstanding Follow-Up

- Add protocol-level integration tests that run Terraform CLI against the hosted endpoints, not just controller-level tests.
- Consider adding an authenticated CLI or SDK for provider publishing so CI pipelines are not the only first-class publication path.
- Decide whether provider-side legacy module-style download endpoints should stay for compatibility or be retired.

## Technical Debt

- `ARCHITECTURE_REVIEW.md` is a useful snapshot, but it should be refreshed when protocol behavior changes so it stays trustworthy.
- The Azure provider host currently mixes modern isolated-worker packages with some older Azure storage dependencies; keep dependency drift under review during future upgrades.
