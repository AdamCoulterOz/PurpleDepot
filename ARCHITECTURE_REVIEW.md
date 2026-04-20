# Architecture Review (April 20, 2026)

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

1. **Provider publication workflow remains thin**
   - Provider installation endpoints are now implemented, but the publication side still lacks a first-class platform/package publishing workflow.
2. **Error handling scope**
   - Response wrapper maps `ControllerResultException` but not unexpected exceptions; runtime failures may leak as generic 500s.
3. **Mock download routing**
   - `MockStorageService.DownloadLink` now exists, but it still points at a non-routable mock host rather than a dev-hosted download endpoint.

## Terraform registry protocol contract evaluation

### Service discovery (`/.well-known/terraform.json`)
- **Status:** ✅ Likely compliant for modules and providers.
- Exposes `modules.v1` and `providers.v1` style roots from route metadata.

### Module registry protocol
- **Status:** ✅ Mostly compliant.
- Supports versions endpoint and download endpoint with `X-Terraform-Get` redirect semantics.
- Also includes convenience endpoints (`latest`, unversioned download, ingest upload) beyond Terraform CLI minimum.

### Provider registry protocol
- **Status:** ✅ Implemented at the controller and Azure host layers, with one operational prerequisite.
- The required package metadata endpoint now exists:
  - `GET :namespace/:type/:version/download/:os/:arch`
- Companion checksum and detached-signature endpoints are also available and used to populate:
  - `download_url`, `shasums_url`, `shasums_signature_url`, `shasum`, `signing_keys`
- Operational prerequisite:
  - provider signing requires configured OpenPGP private key material, and provider version records must contain `protocols` plus the requested `platforms` entry.

## Recommended roadmap

1. Add provider package route and handler to return full provider package metadata contract.
2. Add a first-class provider publication pipeline that persists platform packages and version metadata together.
3. Keep existing module-style provider download route only if needed for backward compatibility, but do not rely on it for Terraform provider installation.
4. Add contract tests that execute Terraform CLI against local endpoints for both module and provider install flows.
