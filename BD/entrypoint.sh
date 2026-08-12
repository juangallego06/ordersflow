#!/bin/bash
# entrypoint.sh — Espera a que SQL Server esté listo y ejecuta init.sql
# Corre en un servicio "db-init" de vida corta dentro del docker-compose.
# Variables de entorno esperadas: SA_PASSWORD, DB_HOST (nombre del servicio SQL Server en el compose)

set -e

MAX_RETRIES=50
RETRY_INTERVAL=2
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

echo "Esperando a que SQL Server esté disponible en $DB_HOST..."

for i in $(seq 1 $MAX_RETRIES); do
    if $SQLCMD -S "$DB_HOST" -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; then
        echo "SQL Server está listo."
        break
    fi
    echo "Intento $i/$MAX_RETRIES — aún no responde, esperando ${RETRY_INTERVAL}s..."
    sleep $RETRY_INTERVAL
    if [ "$i" -eq "$MAX_RETRIES" ]; then
        echo "SQL Server no respondió después de $MAX_RETRIES intentos. Abortando."
        exit 1
    fi
done

echo "Ejecutando init.sql..."
$SQLCMD -S "$DB_HOST" -U sa -P "$SA_PASSWORD" -C -i /scripts/init.sql

echo "Inicialización de bases de datos completada."
