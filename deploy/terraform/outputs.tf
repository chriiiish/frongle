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
