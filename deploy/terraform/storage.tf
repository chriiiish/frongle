# Event photos. The browser sends each photo straight to this bucket through a pre-signed link that the API makes.
resource "aws_s3_bucket" "images" {
  bucket_prefix = "${var.name}-images-"

  tags = { area = "storage" }
}

resource "aws_s3_bucket_public_access_block" "images" {
  bucket = aws_s3_bucket.images.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "images" {
  bucket = aws_s3_bucket.images.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# The browser at the public address uploads and shows photos, so S3 must allow that origin.
resource "aws_s3_bucket_cors_configuration" "images" {
  bucket = aws_s3_bucket.images.id

  cors_rule {
    allowed_origins = [var.public_url]
    allowed_methods = ["GET", "PUT"]
    allowed_headers = ["*"]
    max_age_seconds = 3000
  }
}

# The API pod signs its links with this role (IAM roles for service accounts), so no access key sits in the cluster.
data "aws_iam_policy_document" "api_trust" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [module.eks.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${module.eks.oidc_provider}:sub"
      values   = ["system:serviceaccount:frongle:frongle-api"]
    }

    condition {
      test     = "StringEquals"
      variable = "${module.eks.oidc_provider}:aud"
      values   = ["sts.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "api" {
  name               = "${var.name}-api"
  assume_role_policy = data.aws_iam_policy_document.api_trust.json

  tags = { area = "storage" }
}

data "aws_iam_policy_document" "api_images" {
  statement {
    actions   = ["s3:GetObject", "s3:PutObject"]
    resources = ["${aws_s3_bucket.images.arn}/*"]
  }
}

resource "aws_iam_role_policy" "api_images" {
  name   = "images"
  role   = aws_iam_role.api.id
  policy = data.aws_iam_policy_document.api_images.json
}
