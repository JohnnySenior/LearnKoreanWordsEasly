#!/bin/sh
set -eu

# Railway mounts a root-owned volume. Prepare it once as root, then run the app without root privileges.
if [ "$(id -u)" = 0 ]; then
    mkdir -p /data/files /data/keys /data/backups
    for directory in /data/files /data/keys /data/backups; do
        if [ -L "$directory" ]; then
            echo 'Storage directory must not be a symbolic link.' >&2
            exit 1
        fi
    done
    chown -hR app:app /data/files /data/keys /data/backups
    exec gosu app "$@"
fi
exec "$@"
