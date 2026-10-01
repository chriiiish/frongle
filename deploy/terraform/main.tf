data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, 2)
}

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 6.0"

  name            = var.name
  cidr            = "10.0.0.0/16"
  azs             = local.azs
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

  enable_nat_gateway = true
  single_nat_gateway = true

  tags = { area = "network" }
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 21.0"

  name               = var.name
  kubernetes_version = "1.34"
  vpc_id             = module.vpc.vpc_id
  subnet_ids         = module.vpc.private_subnets

  # Provider default_tags do not reach the node instances, so set project here too.
  tags = { project = "frongle", area = "compute" }

  # The module does not install the add-ons that nodes need. The network plugin must exist before the nodes.
  addons = {
    vpc-cni    = { before_compute = true }
    kube-proxy = {}
    coredns    = {}
  }

  endpoint_public_access                   = true
  endpoint_public_access_cidrs             = var.admin_cidrs
  enable_cluster_creator_admin_permissions = true

  eks_managed_node_groups = {
    default = {
      instance_types = ["t3.medium"]
      min_size       = 1
      max_size       = 3
      desired_size   = 2
    }
  }
}

resource "aws_ecr_repository" "repo" {
  for_each = toset(["api", "web"])

  name                 = "${var.name}/${each.key}"
  image_tag_mutability = "IMMUTABLE"

  tags = { area = "registry" }

  image_scanning_configuration {
    scan_on_push = true
  }
}
