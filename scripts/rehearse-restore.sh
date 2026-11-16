#!/usr/bin/env bash
set -euo pipefail

# pg_dump custom-format file and a NEW scratch database on the rehearsal server.
# Credentials come from PGHOST/PGPORT/PGUSER/PGPASSWORD or .pgpass, never command arguments.
if [[ $# != 2 || ! -f "$1" || ! "$2" =~ ^lugat_rehearsal_[a-z0-9_]+$ ]]; then
    echo 'Usage: rehearse-restore.sh database.dump lugat_rehearsal_NAME' >&2
    exit 2
fi
dump_path=$1
rehearsal_database=$2

# createdb refuses an existing database: this script never overwrites or drops anything.
createdb "$rehearsal_database"
pg_restore --no-owner --no-privileges --exit-on-error --single-transaction --dbname "$rehearsal_database" "$dump_path"
psql --no-psqlrc --set ON_ERROR_STOP=1 --dbname "$rehearsal_database" --command \
    'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;'
echo "Restored into $rehearsal_database. Start the matching app with this database and run the browser smoke test."
echo 'Record health, sign-in, saved progress, audio, worker configuration, elapsed time and any missing secrets/files.'
