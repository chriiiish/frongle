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

run "the_cluster_runs_a_version_in_standard_support" {
  command = plan

  assert {
    condition     = tonumber(split(".", module.eks.cluster_version)[1]) >= 34
    error_message = "EKS 1.33 and older are in extended support, which costs more. Use 1.34 or newer."
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
