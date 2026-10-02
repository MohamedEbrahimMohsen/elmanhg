#!/bin/sh
# Renders the Alertmanager config from env at start, then runs alertmanager; "--check" prints it and runs amtool check-config.
# Email when ALERTMANAGER_EMAIL_TO, _FROM and a password are set, else the null receiver (docs/observability.md §9).
set -eu
umask 077

out_dir=/run/alertmanager
config=$out_dir/alertmanager.yml
password_out=$out_dir/smtp-password
secret_file=/run/secrets/alertmanager-smtp-password

# ":-" also turns an empty SMARTHOST or USERNAME into the Resend default.
to=${ALERTMANAGER_EMAIL_TO:-}
from=${ALERTMANAGER_EMAIL_FROM:-}
smarthost=${ALERTMANAGER_SMTP_SMARTHOST:-smtp.resend.com:587}
username=${ALERTMANAGER_SMTP_USERNAME:-resend}
environment=${ALERTMANAGER_ENVIRONMENT:-elmanhg}

rm -f "$password_out"
if [ -f "$secret_file" ] && [ -s "$secret_file" ]; then
  tr -d '\r\n' < "$secret_file" > "$password_out"
elif [ -n "${ALERTMANAGER_SMTP_PASSWORD:-}" ]; then
  printf '%s' "$ALERTMANAGER_SMTP_PASSWORD" | tr -d '\r\n' > "$password_out"
fi
has_password=0
if [ -s "$password_out" ]; then has_password=1; fi

nl=$(printf '\n_')
nl=${nl%_}
cr=$(printf '\r')
unsafe() {
  case $1 in
    *"'"* | *"$nl"* | *"$cr"*) return 0 ;;
  esac
  return 1
}

reason=
if [ -z "$to" ] && [ -z "$from" ] && [ "$has_password" = 0 ]; then
  mode=null
  echo "alertmanager-entrypoint: no alert email configured; alerts go to the null receiver" >&2
else
  if [ -z "$to" ]; then reason="ALERTMANAGER_EMAIL_TO is empty"
  elif [ -z "$from" ]; then reason="ALERTMANAGER_EMAIL_FROM is empty"
  elif [ "$has_password" = 0 ]; then reason="no SMTP password (ALERTMANAGER_SMTP_PASSWORD or ALERTMANAGER_SMTP_PASSWORD_FILE)"
  else
    case $to in *@*) ;; *) reason="ALERTMANAGER_EMAIL_TO is not an email address" ;; esac
    if [ -z "$reason" ]; then case $from in *@*) ;; *) reason="ALERTMANAGER_EMAIL_FROM is not an email address" ;; esac; fi
    if [ -z "$reason" ]; then case $smarthost in *:*) ;; *) reason="ALERTMANAGER_SMTP_SMARTHOST must be host:port" ;; esac; fi
    if [ -z "$reason" ]; then
      if unsafe "$to"; then reason="ALERTMANAGER_EMAIL_TO contains a quote or a line break"
      elif unsafe "$from"; then reason="ALERTMANAGER_EMAIL_FROM contains a quote or a line break"
      elif unsafe "$smarthost"; then reason="ALERTMANAGER_SMTP_SMARTHOST contains a quote or a line break"
      elif unsafe "$username"; then reason="ALERTMANAGER_SMTP_USERNAME contains a quote or a line break"
      elif unsafe "$environment"; then reason="ALERTMANAGER_ENVIRONMENT contains a quote or a line break"
      fi
    fi
  fi
  if [ -n "$reason" ]; then
    mode=null
    echo "alertmanager-entrypoint: alert email not used: $reason; alerts go to the null receiver" >&2
    rm -f "$password_out"
  else
    mode=email
    echo "alertmanager-entrypoint: alerts are emailed through $smarthost" >&2
  fi
fi

case $smarthost in
  *:465) require_tls=false ;;
  *) require_tls=true ;;
esac

if [ "$mode" = email ]; then
  cat > "$config" <<EOF
# Rendered by entrypoint.sh at container start; edit .env, not this file.
route:
  receiver: email
  group_by: [alertname, severity]
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  routes:
    - receiver: email
      matchers: ['severity="critical"']
      group_wait: 10s
      repeat_interval: 1h
receivers:
  - name: email
    email_configs:
      - to: '$to'
        from: '$from'
        smarthost: '$smarthost'
        auth_username: '$username'
        auth_password_file: $password_out
        require_tls: $require_tls
        send_resolved: true
        headers:
          Subject: '[$environment] {{ template "email.default.subject" . }}'
EOF
else
  cat > "$config" <<EOF
# Rendered by entrypoint.sh at container start; edit .env, not this file.
route:
  receiver: default
  group_by: [alertname, severity]
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
receivers:
  - name: default
EOF
fi

if [ "${1:-}" = --check ]; then
  cat "$config"
  exec /bin/amtool check-config "$config"
fi

exec /bin/alertmanager --config.file="$config" --storage.path=/alertmanager "$@"
