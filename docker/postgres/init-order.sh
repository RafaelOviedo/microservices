#!/bin/bash
set -euo pipefail

psql --dbname postgres --set=ON_ERROR_STOP=1 \
    --set=order_user=order_user --set=order_password="$ORDER_DB_PASSWORD" \
    --set=order_db=order_db <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'order_user', :'order_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'order_user')
\gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'order_db', :'order_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'order_db')
\gexec
REVOKE ALL ON DATABASE :"order_db" FROM PUBLIC;
SQL

PGPASSWORD="$ORDER_DB_PASSWORD" psql --username order_user --dbname order_db \
    --set=ON_ERROR_STOP=1 --command 'SELECT 1' >/dev/null
