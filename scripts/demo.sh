#!/usr/bin/env bash
# Local demo stack (README.md, Run the demo locally): every provider fake, http://localhost:8080.
# Usage: scripts/demo.sh up | down | reset | logs [service]
set -euo pipefail
cd "$(dirname "$0")/../deploy"
# Git Bash would otherwise rewrite container paths in docker arguments.
export MSYS_NO_PATHCONV=1

port=${DEMO_PORT:-8080}
base="http://localhost:$port"
teacher_email=demo-teacher@loadtest.example.com
teacher_password=demo-teacher-1

compose() {
  docker compose -f docker-compose.demo.yml "$@"
}

fail() {
  echo "error: $*" >&2
  exit 1
}

wait_healthy() {
  local service=$1 timeout=${2:-300} waited=0 id status=starting
  until id=$(compose ps -q "$service") && [ -n "$id" ] && status=$(docker inspect -f '{{.State.Health.Status}}' "$id") && [ "$status" = healthy ]; do
    [ "$waited" -ge "$timeout" ] && fail "$service not healthy after ${timeout}s (status: $status); see scripts/demo.sh logs $service"
    sleep 3
    waited=$((waited + 3))
  done
  echo "$service healthy"
}

json_field() {
  grep -o "\"$1\":\"[^\"]*\"" | head -1 | cut -d'"' -f4
}

post() {
  curl -sS -X POST -H 'Content-Type: application/json' -d "$2" "$base$1"
}

# The seeded teacher has no password yet (a pending invitation): accept it once through the normal email-code flow.
activate_teacher() {
  local login verification code
  login=$(post /api/auth/login/email "{\"email\":\"$teacher_email\",\"password\":\"$teacher_password\"}")
  [[ $login == *accessToken* ]] && return 0
  verification=$(post /api/auth/otp/send "{\"email\":\"$teacher_email\"}" | json_field verificationId)
  [ -n "$verification" ] || fail "could not request the teacher's invitation code"
  sleep 1
  code=$(compose logs --no-color api | grep -oE "OTP for \"?$teacher_email\"?: \"?[0-9]{6}" | tail -1 | grep -oE '[0-9]{6}$')
  [ -n "$code" ] || fail "no OTP for $teacher_email in the api logs"
  post /api/auth/otp/verify "{\"code\":\"$code\",\"verificationId\":\"$verification\"}" > /dev/null
  login=$(post /api/auth/invitations/accept "{\"verificationId\":\"$verification\",\"password\":\"$teacher_password\"}")
  [[ $login == *accessToken* ]] || fail "teacher invitation accept failed: $login"
  echo "teacher account activated"
}

case "${1:-}" in
  up)
    compose up -d --build
    wait_healthy postgres
    wait_healthy ai
    wait_healthy api
    wait_healthy web
    activate_teacher
    cat <<EOF

Elmanhg demo is running: $base

  Admin    admin@elmanhg.local / demo-admin-1
  Teacher  $teacher_email / $teacher_password
  Students demo-student-001@loadtest.example.com / demo-student-1  (also -002, -003; subscribed to the "Load test demo" subject)

  New students sign up with any phone (010/011/012/015 + 8 digits) or email; the OTP code is in the API log:
    scripts/demo.sh logs api | grep "OTP for"
  Payments use the fake checkout; every AI feature answers from the ai service's fakes.

  Stop (keep data): scripts/demo.sh down    Wipe everything: scripts/demo.sh reset
EOF
    ;;
  down)
    compose down
    ;;
  reset)
    compose down -v --remove-orphans
    ;;
  logs)
    shift
    compose logs --no-color "$@"
    ;;
  *)
    echo "usage: scripts/demo.sh up | down | reset | logs [service]" >&2
    exit 2
    ;;
esac
