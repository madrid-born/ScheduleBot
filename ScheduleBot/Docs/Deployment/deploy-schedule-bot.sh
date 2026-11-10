#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

parent="$HOME/dotnet"
app="$parent/telegramBot"
unit=schedule-bot.service
domain=schedule.kofi.rest
port=5087
status_path=/api/bot/status
curl_args=(--noproxy '*' --silent --show-error --max-time 8)

fail() {
    printf '%s\n' "$*" >&2
    exit 1
}

health() {
    curl "${curl_args[@]}" --fail "$@" |
        python3 -c '
import json, sys
try:
    data = json.load(sys.stdin)
    sys.exit(0 if data.get("status") == "Online" and data.get("mode") == "Polling" else 1)
except (ValueError, AttributeError):
    sys.exit(1)
'
}

check_listener() {
    local pid
    pid="$(systemctl show "$unit" -p MainPID --value)"
    [[ "$pid" =~ ^[1-9][0-9]*$ ]] &&
        sudo -n ss -H -ltnp "src 127.0.0.1:$port" | grep -F "pid=$pid," >/dev/null
}

loopback_ready() {
    systemctl is-active --quiet "$unit" &&
        check_listener &&
        health -H 'X-Forwarded-Proto: https' "http://127.0.0.1:$port$status_path" 2>/dev/null
}

check_routes() {
    local redirect
    health -H 'X-Forwarded-Proto: https' "http://127.0.0.1:$port$status_path" ||
        fail 'The loopback app health check failed.'
    echo "Loopback port $port returned Online / Polling."
    health --resolve "$domain:5443:127.0.0.1" "https://$domain:5443$status_path" ||
        fail 'The local Nginx TLS health check failed.'
    echo 'Nginx TLS returned Online / Polling.'
    health "https://$domain$status_path" || fail 'The public HTTPS health check failed.'
    echo "https://$domain$status_path returned Online / Polling."
    redirect="$(curl "${curl_args[@]}" --output /dev/null --write-out '%{http_code} %{redirect_url}' \
        "http://$domain$status_path")"
    [[ "$redirect" == "308 https://$domain$status_path" ]] || fail "Unexpected HTTP redirect: $redirect"
    echo 'Public HTTP returned the expected 308 HTTPS redirect.'
}

preflight() {
    [[ "$(uname -m)" == x86_64 ]] || fail 'The server must be Linux x86-64.'
    local runtimes
    runtimes="$(/usr/bin/dotnet --list-runtimes)"
    grep -E '^Microsoft\.NETCore\.App 8\.0\.' <<<"$runtimes"
    grep -E '^Microsoft\.AspNetCore\.App 8\.0\.' <<<"$runtimes"
    [[ -d "$app" && ! -L "$app" && -O "$app" && -f "$app/appsettings.json" ]] ||
        fail "The existing deployment at $app is missing or has unexpected ownership or layout."
    [[ "$(systemctl show "$unit" -p WorkingDirectory --value)" == "$app" &&
       "$(systemctl show "$unit" -p User --value)" == "$(id -un)" &&
       "$(systemctl show "$unit" -p ExecStart --value)" == *"argv[]=/usr/bin/dotnet $app/ScheduleBot.dll ;"* ]] ||
        fail "The systemd unit differs from the expected deployment layout. Review $unit before deploying."
    systemctl is-enabled --quiet "$unit"
    systemctl is-active --quiet "$unit"
    sudo -n systemd-analyze verify "$unit"
    sudo -n nginx -t
    check_listener || fail "The service does not own loopback port $port."
    check_routes
}

if [[ "${1:-}" == --check ]]; then
    preflight
    exit 0
fi

[[ $# == 2 ]] || fail 'Expected a staging path and base Git commit.'
stage="$1"
base_commit="$2"
[[ "$stage" == "$parent"/.schedule-bot-stage.* &&
   "${stage##*/}" =~ ^\.schedule-bot-stage\.[a-zA-Z0-9]{8}$ &&
   -d "$stage" && ! -L "$stage" && -O "$stage" &&
   "$base_commit" =~ ^[a-f0-9]{40,64}$ ]] ||
    fail 'Invalid deployment arguments.'

exec 9>"$parent/.schedule-bot-deploy.lock"
flock -n 9 || fail 'Another ScheduleBot deployment is in progress.'
preflight

incoming="$stage/incoming"
backup="$parent/telegramBot.backup-$(date -u +%Y%m%dT%H%M%S.%NZ)"
[[ ! -e "$backup" && ! -L "$backup" ]] || fail "Backup already exists: $backup"
python3 "$stage/schedule-bot-release-files.py" assemble "$stage" "$app"
for path in ScheduleBot.dll ScheduleBot.deps.json ScheduleBot.runtimeconfig.json Fonts/Vazirmatn-Regular.ttf; do
    [[ -s "$incoming/$path" ]] || fail "Missing publish output: $path"
done
chmod --reference="$app" "$incoming"
chgrp --reference="$app" "$incoming"

diagnostics() {
    sudo -n systemctl status "$unit" --no-pager --full --lines=0
    sudo -n ss -H -ltnp "sport = :$port"
    sudo -n journalctl -u "$unit" --since "$cutover_time" --no-pager -o cat -n 100 |
        sed -E 's@([0-9]{5,}:|/bot[0-9]+:)[A-Za-z0-9_-]{20,}@[telegram-token]@g; s@/downloads/[^ ?"\x27]+@/downloads/[redacted]@g; s@([Pp]assword|PWD)=[^;]+@\1=[redacted]@g'
}

rollback() {
    local result=$?
    trap - EXIT
    trap '' HUP INT TERM
    if (( result == 0 )); then
        return 0
    fi

    set +e
    echo "Deployment failed with exit code $result. Checking service status, listener, and startup logs." >&2
    diagnostics
    if [[ -d "$backup" ]]; then
        echo "Restoring $backup ..."
        if ! sudo -n systemctl stop "$unit" ||
           { [[ -e "$app" ]] && ! mv -T -- "$app" "$stage/failed"; } ||
           ! mv -T -- "$backup" "$app"; then
            echo "Rollback could not restore the directory. Inspect $backup and $stage." >&2
            exit "$result"
        fi
    fi
    sudo -n systemctl reset-failed "$unit"
    if sudo -n systemctl start "$unit"; then
        local attempt
        for (( attempt = 1; attempt <= 45; attempt++ )); do
            if loopback_ready; then
                if (check_routes); then
                    echo 'Previous deployment restored and verified.'
                    exit "$result"
                fi
                break
            fi
            sleep 2
        done
    fi
    echo 'Previous deployment restored. Its service or route check failed.' >&2
    diagnostics
    exit "$result"
}

cutover_time="$(date '+%F %T')"
trap rollback EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

echo "Installing the published working-tree build (base commit $base_commit) ..."
sudo -n systemctl stop "$unit"
for config in "$app"/appsettings*.json; do
    cp --dereference --preserve=mode,ownership,timestamps -- "$config" "$incoming/"
done
if [[ -d "$app/Data/Telegram" ]]; then
    mkdir -p -- "$incoming/Data"
    cp -a -- "$app/Data/Telegram" "$incoming/Data/"
fi
mv -T -- "$app" "$backup"
mv -T -- "$incoming" "$app"
sudo -n systemctl start "$unit"

initial_invocation="$(systemctl show "$unit" -p InvocationID --value)"
[[ "$initial_invocation" =~ ^[a-f0-9]{32}$ && "$initial_invocation" != 00000000000000000000000000000000 ]] ||
    fail 'The service did not start a new invocation.'

ready=false
for (( attempt = 1; attempt <= 45; attempt++ )); do
    if loopback_ready &&
       sudo -n journalctl "_SYSTEMD_INVOCATION_ID=$initial_invocation" --no-pager -o cat \
           -g '^ *Bot started:' -n 1 | grep -E '^ *Bot started:' >/dev/null; then
        ready=true
        break
    fi
    sleep 2
done
[[ "$ready" == true ]] || fail 'The new service did not become ready or log a successful Telegram startup.'
sleep 12
[[ "$(systemctl show "$unit" -p InvocationID --value)" == "$initial_invocation" ]] ||
    fail 'The service restarted during verification.'
systemctl is-active --quiet "$unit"
check_listener || fail "The new service does not own loopback port $port."
if sudo -n journalctl "_SYSTEMD_INVOCATION_ID=$initial_invocation" --no-pager -o cat \
    -g '^ *Bot error occurred' -n 1 | grep -E '^ *Bot error occurred' >/dev/null; then
    fail 'Telegram polling logged an error during startup.'
fi
check_routes
diagnostics
echo "Deployment verified. Previous release: $backup"
