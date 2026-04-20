# Architecture Review (April 11, 2026)

## High-level assessment

PurpleDepot has a clean layered architecture:

- **Domain/contracts** in `Core/Interface/Model`
- **Application logic/controllers** in `Core/Controller`
- **Infrastructure adapters** in `Providers/<Cloud>/...`

The Azure implementation composes these layers through dependency injection and Azure Functions endpoints.

## Strengths

1. **Separation of concerns**
   - Transport (Azure Functions host), domain models, persistence, and blob storage are clearly separated.
2. **Reusable generic flow**
   - `ItemController<T>` centralizes ingest/get/download behavior for both modules and providers.
3. **Service discovery support**
   - `.well-known/terraform.json` is present and auto-discovers routable services via `IRoutes` implementations.
4. **Persistence abstraction**
   - `IRepository<T>` and `IStorageProvider<T>` abstractions keep controllers storage-agnostic.

## Architecture risks / improvement opportunities

1. **Protocol boundary mismatch for providers**
   - Provider routes currently mirror module-style `download` behavior (204 + `X-Terraform-Get`), but Terraform provider registry protocol requires package metadata JSON at `:namespace/:type/:version/download/:os/:arch`.
2. **Unimplemented provider package model path**
   - `ProviderPackage.NewFromProvider(...)` is not implemented.
3. **Error handling scope**
   - Response wrapper maps `ControllerResultException` but not unexpected exceptions; runtime failures may leak as generic 500s.
4. **In-memory storage download URL**
   - `MockStorageService.DownloadLink` is not implemented, limiting local end-to-end download testing.

## Terraform registry protocol contract evaluation

### Service discovery (`/.well-known/terraform.json`)
- **Status:** ✅ Likely compliant for modules and providers.
- Exposes `modules.v1` and `providers.v1` style roots from route metadata.

### Module registry protocol
- **Status:** ✅ Mostly compliant.
- Supports versions endpoint and download endpoint with `X-Terraform-Get` redirect semantics.
- Also includes convenience endpoints (`latest`, unversioned download, ingest upload) beyond Terraform CLI minimum.

### Provider registry protocol
- **Status:** ❌ Not compliant yet.
- Missing required provider package endpoint:
  - `GET :namespace/:type/:version/download/:os/:arch`
- Current provider download endpoint returns module-style redirect header rather than required package metadata object including:
  - `protocols`, `os`, `arch`, `filename`, `download_url`, `shasums_url`, `shasums_signature_url`, `shasum`, `signing_keys`.

## Recommended roadmap

1. Add provider package route and handler to return full provider package metadata contract.
2. Implement checksum/signing-key metadata pipeline and populate `ProviderPackage`.
3. Keep existing module-style provider download route only if needed for backward compatibility, but do not rely on it for Terraform provider installation.
4. Add contract tests that execute Terraform CLI against local endpoints for both module and provider install flows.
