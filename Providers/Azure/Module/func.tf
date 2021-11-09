resource "azurerm_storage_account" "app_storage" {
  name                     = "${var.instance_name}func"
  resource_group_name      = azurerm_resource_group.instance.name
  location                 = azurerm_resource_group.instance.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_app_service_plan" "app_plan" {
  name                = var.instance_name
  location            = azurerm_resource_group.instance.location
  resource_group_name = azurerm_resource_group.instance.name
  kind                = "functionapp"
  reserved            = true

  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

data "azuread_client_config" "current" {}

resource "azurerm_function_app" "app" {
  name                       = var.instance_name
  location                   = azurerm_resource_group.instance.location
  resource_group_name        = azurerm_resource_group.instance.name
  app_service_plan_id        = azurerm_app_service_plan.app_plan.id
  storage_account_name       = azurerm_storage_account.app_storage.name
  storage_account_access_key = azurerm_storage_account.app_storage.primary_access_key
  os_type                    = "linux"
  https_only                 = true
  version                    = "~3"

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME                                  = "dotnet-isolated"
    PurpleDepot__Provider                                     = "Azure"
    PurpleDepot__AzureStorageOptions__BlobEndpointUrl         = azurerm_storage_account.module_repo.primary_blob_endpoint
    PurpleDepot__AzureStorageOptions__ModuleContainer         = azurerm_storage_container.module_repo.name
    PurpleDepot__AzureStorageOptions__ProviderContainer       = azurerm_storage_container.provider_repo.name
    PurpleDepot__AzureDatabaseOptions__CosmosConnectionString = azurerm_cosmosdb_account.db.connection_strings[0]
  }

  site_config {
    always_on = false
    use_32_bit_worker_process = false
    min_tls_version           = "1.2"
  }

  identity {
    type = "SystemAssigned"
  }

  auth_settings {
    default_provider               = "AzureActiveDirectory"
    enabled                        = true
    issuer                         = "https://sts.windows.net/${data.azuread_client_config.current.tenant_id}/"
    runtime_version                = "~1"
    token_refresh_extension_hours  = 72
    token_store_enabled            = true
    unauthenticated_client_action  = "RedirectToLoginPage"
    active_directory {
      allowed_audiences = [var.url]
      client_id         = azuread_application.terraform.application_id
      client_secret     = null
    }
  }
}