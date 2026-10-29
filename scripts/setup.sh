#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
if [ -f .env ]; then
    echo '.env findes allerede og er bevaret.'
    exit 0
fi
umask 077
printf 'JWT_SECRET=%s\nADMIN_USERNAME=admin\nADMIN_PASSWORD=%s\nWEB_BIND_ADDRESS=127.0.0.1\n' \
    "$(openssl rand -hex 48)" "$(openssl rand -hex 16)" > .env
echo 'Oprettet .env. Find admin-login og ret WEB_BIND_ADDRESS med nano .env.'
