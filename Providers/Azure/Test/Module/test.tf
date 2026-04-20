
provider "azurerm" {
  features {}
}

terraform {
  backend "azurerm" {}
  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~>3.8"
    }
  }
}

module "infra" {
  source              = "../../Module"
  app_name            = "Test Terraform"
  instance_name       = "testpurpledepot"
  resource_group_name = "testpurpledepot"
  location            = "australiaeast"
}

data "azuread_client_config" "current" {}

resource "azuread_application" "tester" {
  display_name = "Tester - PurpleDepot Contributor"
  owners       = [data.azuread_client_config.current.object_id]

  required_resource_access {
    resource_app_id = module.infra.terraform_app_application_id

    resource_access {
      id   = module.infra.api_contributor_role_id
      type = "Role"
    }
  }

  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000" # microsoft graph

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
  }
}

resource "azuread_application_password" "tester_password" {
  application_object_id = azuread_application.tester.object_id
}

resource "azuread_service_principal" "tester" {
  application_id = azuread_application.tester.application_id
  owners         = [data.azuread_client_config.current.object_id]
}

resource "azuread_app_role_assignment" "tester_tfcontibutor" {
  app_role_id         = module.infra.api_contributor_role_id
  principal_object_id = azuread_service_principal.tester.object_id
  resource_object_id  = module.infra.terraform_sp_object_id
}
