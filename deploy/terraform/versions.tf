terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # TODO: choose a remote state backend (for example S3 with locking) before the first apply.
}

provider "aws" {
  region = var.region
}
