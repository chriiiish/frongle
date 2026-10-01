terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # The bucket comes from -backend-config=bucket=<name>, so no account detail sits in the repository.
  backend "s3" {
    key          = "frongle/terraform.tfstate"
    region       = "us-east-1"
    use_lockfile = true
    encrypt      = true
  }
}

provider "aws" {
  region = var.region

  # Cost allocation: every resource gets project. Each resource or module adds its own area.
  default_tags {
    tags = {
      project = "frongle"
    }
  }
}
