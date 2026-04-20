# PurpleDepot Context

## Purpose and Current State

PurpleDepot is aiming to become an easy-to-deploy open source Terraform Enterprise-style platform. The current codebase already covers a substantial part of the private registry surface area: it exposes Terraform module and provider discovery endpoints through an Azure Functions host, with shared domain and controller logic in `Core` and Azure-specific hosting and storage in `Providers/Azure`.

Current work in flight:

- .NET projects have been upgraded to `net10.0`
- Azure provider and Terraform module provider versions have been refreshed
- The default branch has been renamed from `master` to `main`
- `PurpleDepot.slnx` has been added for solution-level builds
- Provider package metadata, checksum, and signature endpoints now exist for the Terraform provider registry protocol
- Provider package uploads are now platform-aware at `v1/providers/{namespace}/{name}/{version}/upload/{os}/{arch}`
- Mock-backed downloads now round-trip through a hosted archive proxy at `v1/archive/{fileKey}`
- `Tests/PurpleDepot.Tests` now covers mock storage behavior and provider package controller behavior
- The next major product step is a first-class Terraform `http` backend so PurpleDepot becomes the public state service boundary, rather than pushing users toward storage-provider-native backends such as `azurerm`
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

## Product Direction

- PurpleDepot should present a PurpleDepot-native Terraform API surface wherever practical, rather than exposing cloud-vendor-specific implementation details.
- Registry support is no longer the whole product; it is the first major subsystem of a broader Terraform platform.
- The preferred backend direction is Terraform's built-in `http` backend as the initial state service entry point. This is materially simpler and cleaner than trying to jump straight to `remote` backend parity.
- Full `remote` backend compatibility remains a possible long-term ambition, but it should be treated as a later platform phase because it implies HCP Terraform-style workspaces, runs, config uploads, remote execution, and run lifecycle management.

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
- Any future backend/state implementation should preserve the same product boundary principle: Terraform users should talk to PurpleDepot, not directly to Azure Blob or other storage internals.

## Outstanding Follow-Up

- Add protocol-level integration tests that run Terraform CLI against the hosted endpoints, not just controller-level tests.
- Consider adding an authenticated CLI or SDK for provider publishing so CI pipelines are not the only first-class publication path.
- Decide whether provider-side legacy module-style download endpoints should stay for compatibility or be retired.
- Document and implement the next state-service milestone around Terraform's `http` backend.

## Roadmap

### Near-Term

- Add Terraform CLI integration tests for:
  - module registry install flows
  - provider registry install flows
  - provider package publication flows
- Improve registry docs so operator setup, signing key configuration, and publish workflows are easy to follow.
- Decide on the compatibility posture for older provider routes and remove or clearly deprecate anything that should not remain public long-term.

### Next Major Milestone: PurpleDepot HTTP Backend

- Implement a Terraform `http` backend-compatible API for remote state.
- Support state read/write/delete and locking/unlocking semantics expected by Terraform CLI.
- Define a workspace-to-state path model that fits PurpleDepot's future multi-workspace platform direction.
- Add state version history and audit-friendly metadata from the start, even if the first CLI contract only needs current state plus locks.
- Add Terraform CLI integration tests for:
  - `terraform init`
  - `terraform plan`
  - `terraform apply`
  - `terraform workspace`
  - `terraform state` subcommands against the PurpleDepot backend
- Document recommended Terraform configuration for using PurpleDepot as the backend, and position it as the preferred public interface over cloud-provider-native backends.

### Mid-Term Platform Layer

- Add richer workspace metadata and management APIs.
- Add auth and authorization boundaries that can distinguish registry read access, package publication, and state mutation.
- Add state history browsing, rollback/restore workflow, and audit trails.
- Add policy hooks and compatibility/version checks around state mutation where useful.

### Long-Term Enterprise-Style Direction

- Evaluate what subset of Terraform Enterprise / HCP Terraform behavior PurpleDepot should emulate directly.
- If the project moves toward `remote` backend parity, plan that as a dedicated subsystem including:
  - organizations and workspaces
  - configuration uploads
  - run creation and status lifecycle
  - remote plan/apply execution workers
  - streamed logs and approvals
  - version compatibility rules between local CLI and remote execution
- Treat this as a large later-phase effort, not a prerequisite for the `http` backend milestone.

## Technical Debt

- `ARCHITECTURE_REVIEW.md` is a useful snapshot, but it should be refreshed when protocol behavior changes so it stays trustworthy.
- The Azure provider host currently mixes modern isolated-worker packages with some older Azure storage dependencies; keep dependency drift under review during future upgrades.
- The repo currently has stronger controller-level coverage than end-to-end Terraform CLI coverage; the next confidence boost should come from real integration tests, not just more unit tests.
