terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "2.36.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "3.0.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

locals {
  deployment_name = "eth-wallets"
  location        = "eastus"
}

data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.deployment_name}-${local.location}"
  location = local.location
}

resource "azurerm_key_vault" "this" {
  name                = "kv-${local.deployment_name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  soft_delete_enabled = true
  sku_name            = "premium"
}

resource "random_string" "this" {
  length  = 16
  special = false
  upper   = false
}

resource "azurerm_storage_account" "this" {
  name                     = random_string.this.result
  resource_group_name      = azurerm_resource_group.this.name
  location                 = azurerm_resource_group.this.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_app_service_plan" "this" {
  name                = "plan-${local.deployment_name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  kind                = "FunctionApp"

  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

resource "azurerm_function_app" "this" {
  name                       = "func-${local.deployment_name}"
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  app_service_plan_id        = azurerm_app_service_plan.this.id
  storage_account_name       = azurerm_storage_account.this.name
  storage_account_access_key = azurerm_storage_account.this.primary_access_key
  version                    = "~3"

  app_settings = {
    "KEY_VAULT_URL" = azurerm_key_vault.this.vault_uri
  }

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_key_vault_access_policy" "this" {
  key_vault_id = azurerm_key_vault.this.id

  tenant_id = azurerm_key_vault.this.tenant_id
  object_id = azurerm_function_app.this.identity[0].principal_id

  key_permissions = [
    "create", "sign", "get"
  ]
}
