-- The API serves requests as this role. It is not a superuser and cannot bypass row-level security.
CREATE ROLE frongle_app LOGIN PASSWORD 'local-only-app-password' NOSUPERUSER NOBYPASSRLS;
ALTER DEFAULT PRIVILEGES FOR ROLE frongle IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO frongle_app;
ALTER DEFAULT PRIVILEGES FOR ROLE frongle IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO frongle_app;
