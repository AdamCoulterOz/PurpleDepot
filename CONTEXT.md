# PurpleDepot Context

## Purpose and Current State

PurpleDepot is a Terraform private registry implementation. The current codebase exposes Terraform module and provider discovery endpoints through an Azure Functions host, with shared domain and controller logic in `Core` and Azure-specific hosting and storage in `Providers/Azure`.

Current work in flight:

- .NET projects have been upgraded to `net10.0`
- Azure provider and Terraform module provider versions have been refreshed
- `PurpleDepot.slnx` has been added for solution-level builds
- `ARCHITECTURE_REVIEW.md` captures the current protocol and architecture assessment

## Architecture and Structure

- `Core/Interface/Model`: registry contracts, addresses, route metadata, and serialized response models
- `Core/Controller`: application logic for service discovery, module/provider lookup, ingest, and download response construction
- `Core/Controller/Data`: Entity Framework Core persistence abstractions and the shared `AppContext`
- `Core/Interface/Storage`: storage abstraction plus the in-memory mock implementation
- `Providers/Azure/DotNet`: Azure Functions isolated worker host, HTTP endpoints, DI setup, and Azure Blob-backed storage
- `Providers/Azure/Module`: Terraform module for deploying the Azure-hosted registry
- `Providers/Azure/Test/Module`: Terraform-based infrastructure smoke test inputs

## Key Decisions and Invariants

- Service discovery is driven by `IRoutes` implementations and emitted at `/.well-known/terraform.json`.
- Module downloads currently follow the Terraform registry redirect pattern using `204` plus `X-Terraform-Get`.
- Provider endpoints currently reuse the generic item controller flow, so provider downloads still behave like module downloads rather than the provider package metadata contract Terraform expects.
- Storage and persistence stay abstract behind `IStorageProvider<T>` and `IRepository<T>`.
- The Azure Functions host now treats missing `PurpleDepot` configuration as a startup error instead of continuing with a null configuration object.

## Outstanding Follow-Up

- Implement the Terraform provider package download contract at `v1/providers/{namespace}/{name}/{version}/download/{os}/{arch}`.
- Implement `ProviderPackage.NewFromProvider(...)` and the checksum/signing metadata pipeline.
- Decide whether the repo should stay on `master` or be renamed to `main` to match the preferred default branch policy.
- Add automated verification beyond `dotnet build`, especially protocol-level tests against Terraform CLI flows.

## Technical Debt

- `MockStorageService<T>.DownloadLink` is still not implemented, which blocks local end-to-end download testing.
- `ARCHITECTURE_REVIEW.md` is a useful snapshot, but it should be refreshed when protocol behavior changes so it stays trustworthy.
- The Azure provider host currently mixes modern isolated-worker packages with some older Azure storage dependencies; keep dependency drift under review during future upgrades.
