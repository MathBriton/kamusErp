#!/bin/sh
# Papel usado pela aplicação: sem superusuário, para que o Row Level Security seja aplicado.
# O usuário POSTGRES_USER (superusuário) fica reservado para migrations e administração.
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-SQL
  CREATE ROLE kamus_app LOGIN PASSWORD '${KAMUS_APP_PASSWORD}' NOSUPERUSER NOBYPASSRLS;
  GRANT CONNECT ON DATABASE "${POSTGRES_DB}" TO kamus_app;
SQL
