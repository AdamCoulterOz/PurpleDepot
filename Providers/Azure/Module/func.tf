resource "azurerm_storage_account" "app_storage" {
  name                     = "${var.instance_name}func"
  resource_group_name      = azurerm_resource_group.instance.name
  location                 = azurerm_resource_group.instance.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_storage_container" "app_storage_deployments" {
  name                  = "deployments"
  storage_account_id    = azurerm_storage_account.app_storage.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "func_app_access_to_storage" {
  scope                = azurerm_storage_account.app_storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_function_app_flex_consumption.app.identity[0].principal_id
}

resource "azurerm_service_plan" "app_plan" {
  name                = var.instance_name
  location            = azurerm_resource_group.instance.location
  resource_group_name = azurerm_resource_group.instance.name
  os_type             = "Linux"
  sku_name            = "FC1"
}

resource "azurerm_application_insights" "monitor" {
  name                = var.instance_name
  location            = azurerm_resource_group.instance.location
  resource_group_name = azurerm_resource_group.instance.name
  application_type    = "web"
}

resource "azurerm_function_app_flex_consumption" "app" {
  name                        = var.instance_name
  location                    = azurerm_resource_group.instance.location
  resource_group_name         = azurerm_resource_group.instance.name
  service_plan_id             = azurerm_service_plan.app_plan.id
  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.app_storage.primary_blob_endpoint}${azurerm_storage_container.app_storage_deployments.name}"
  storage_authentication_type = "StorageAccountConnectionString"
  storage_access_key          = azurerm_storage_account.app_storage.primary_access_key
  runtime_name                = "dotnet-isolated"
  runtime_version             = "10.0"
  https_only                  = true
  maximum_instance_count      = 40
  instance_memory_in_mb       = 2048

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME          = "dotnet-isolated"
    PurpleDepot__Storage__Account     = azurerm_storage_account.repo.name
    PurpleDepot__Storage__Container   = azurerm_storage_container.registry.name
    PurpleDepot__Database__Connection = azurerm_cosmosdb_account.db.connection_strings[0]
    PurpleDepot__Database__Name       = "PurpleDepot"
  }

  application_insights_key               = azurerm_application_insights.monitor.instrumentation_key
  application_insights_connection_string = azurerm_application_insights.monitor.connection_string

  site_config {}

  identity {
    type = "SystemAssigned"
  }

  auth_settings {
    enabled                       = true
    token_store_enabled           = true
    issuer                        = "https://sts.windows.net/${data.azuread_client_config.current.tenant_id}/"
    unauthenticated_client_action = "RedirectToLoginPage"
    active_directory {
      client_id         = azuread_application.terraform.application_id
      allowed_audiences = var.url != null ? [var.url] : [local.token_audience]
    }
  }
}
