variable "region" {
  description = "AWS region for all resources."
  type        = string
  default     = "us-east-1"
}

variable "name" {
  description = "Name prefix for resources."
  type        = string
  default     = "frongle"
}

variable "db_instance_class" {
  description = "RDS instance class."
  type        = string
  default     = "db.t4g.micro"
}

variable "admin_cidrs" {
  description = "CIDR ranges that can reach the public Kubernetes API endpoint, for example [\"203.0.113.4/32\"]."
  type        = list(string)
}
