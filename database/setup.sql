-- WaterWatch database setup (PostgreSQL 18 + PostGIS 3.6).
-- Run as a PostgreSQL superuser:
--   psql -U postgres -f database/setup.sql
--
-- The application role is intentionally NOT a superuser. It owns the application
-- database and can create PostGIS objects in it, but cannot create databases or roles.

CREATE ROLE waterwatch_app WITH LOGIN PASSWORD 'changeme-local';
CREATE DATABASE waterwatch OWNER waterwatch_app;

\connect waterwatch
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_raster;

-- The legacy Cape Town demonstration database (cptwater) is left untouched.
-- To use the original demo endpoints against it, override the connection string.
