#!/bin/bash
set -euo pipefail

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=ON_ERROR_STOP=1 \
    --set=product_user="$PRODUCT_DB_USER" \
    --set=product_password="$PRODUCT_DB_PASSWORD" \
    --set=product_db="$PRODUCT_DB_NAME" <<'SQL'
CREATE ROLE :"product_user" LOGIN PASSWORD :'product_password';
CREATE DATABASE :"product_db" OWNER :"product_user";
REVOKE ALL ON DATABASE :"product_db" FROM PUBLIC;
SQL
