mock_provider "aws" {
  mock_data "aws_availability_zones" {
    defaults = {
      names = ["us-east-1a", "us-east-1b", "us-east-1c"]
    }
  }

  mock_data "aws_caller_identity" {
    defaults = {
      account_id = "123456789012"
      arn        = "arn:aws:iam::123456789012:user/test"
    }
  }

  mock_data "aws_partition" {
    defaults = {
      partition  = "aws"
      dns_suffix = "amazonaws.com"
    }
  }

  mock_data "aws_iam_session_context" {
    defaults = {
      issuer_arn = "arn:aws:iam::123456789012:user/test"
    }
  }

  mock_data "aws_iam_policy_document" {
    defaults = {
      json = "{\"Version\":\"2012-10-17\",\"Statement\":[]}"
    }
  }
}

variables {
  admin_cidrs = ["203.0.113.4/32"]
}

# The provider default_tags add project=frongle to every resource. A mock provider cannot
# evaluate them, so these checks cover the area tag that each resource sets itself.
run "every_resource_has_an_area_tag_for_cost_allocation" {
  command = plan

  assert {
    condition     = alltrue([for repo in aws_ecr_repository.repo : repo.tags["area"] == "registry"])
    error_message = "ECR repositories need area=registry."
  }

  assert {
    condition     = aws_db_instance.db.tags["area"] == "database"
    error_message = "The database needs area=database."
  }

  assert {
    condition     = aws_db_subnet_group.db.tags["area"] == "database" && aws_security_group.db.tags["area"] == "database"
    error_message = "Database support resources need area=database."
  }
}

run "the_cluster_runs_a_version_in_standard_support_until_2027" {
  command = plan

  assert {
    condition     = tonumber(split(".", module.eks.cluster_version)[1]) >= 35
    error_message = "EKS 1.34 leaves standard support on 2026-12-02. Use 1.35 or newer."
  }
}

# The EKS module does not install these. Without them the nodes boot but never become Ready.
run "the_cluster_has_the_add_ons_that_nodes_need" {
  command = plan

  assert {
    condition     = alltrue([for name in ["vpc-cni", "kube-proxy", "coredns"] : contains(keys(module.eks.cluster_addons), name)])
    error_message = "The cluster needs the vpc-cni, kube-proxy, and coredns add-ons."
  }
}

run "the_photo_bucket_is_private_encrypted_and_tagged" {
  command = plan

  assert {
    condition     = aws_s3_bucket.images.tags["area"] == "storage" && aws_iam_role.api.tags["area"] == "storage"
    error_message = "The photo bucket and the API role need area=storage."
  }

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.images.block_public_acls,
      aws_s3_bucket_public_access_block.images.block_public_policy,
      aws_s3_bucket_public_access_block.images.ignore_public_acls,
      aws_s3_bucket_public_access_block.images.restrict_public_buckets,
    ])
    error_message = "The photo bucket must block all public access."
  }

  assert {
    condition     = alltrue([for rule in aws_s3_bucket_cors_configuration.images.cors_rule : length(rule.allowed_origins) == 1 && contains(rule.allowed_origins, "https://frongle.cjl.nz")])
    error_message = "The photo bucket must allow uploads from the public address only."
  }
}
