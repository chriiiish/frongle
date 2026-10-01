output "cluster_name" {
  value = module.eks.cluster_name
}

output "ecr_repository_urls" {
  value = { for name, repo in aws_ecr_repository.repo : name => repo.repository_url }
}

output "database_host" {
  value = aws_db_instance.db.address
}

output "database_secret_arn" {
  description = "Secrets Manager secret that holds the database username and password."
  value       = aws_db_instance.db.master_user_secret[0].secret_arn
}

output "images_bucket" {
  description = "S3 bucket that holds the Event photos."
  value       = aws_s3_bucket.images.id
}

output "api_role_arn" {
  description = "IAM role that the API pod assumes to sign links to the photo bucket."
  value       = aws_iam_role.api.arn
}
