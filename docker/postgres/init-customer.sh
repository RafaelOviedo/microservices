#!/bin/bash
set -euo pipefail

psql --dbname postgres --set=ON_ERROR_STOP=1 \
    --set=customer_user=customer_user --set=customer_password="$CUSTOMER_DB_PASSWORD" \
    --set=customer_db=customer_db <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'customer_user', :'customer_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'customer_user')
\gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'customer_db', :'customer_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'customer_db')
\gexec
REVOKE ALL ON DATABASE :"customer_db" FROM PUBLIC;
SQL

PGPASSWORD="$CUSTOMER_DB_PASSWORD" psql --username customer_user --dbname customer_db \
    --set=ON_ERROR_STOP=1 --command 'SELECT 1' >/dev/null
