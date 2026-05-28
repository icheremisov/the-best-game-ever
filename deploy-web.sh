#!/usr/bin/env bash
#
# Деплой Unity WebGL-билда в S3.
#
# Билд по умолчанию берётся из Builds/web/ (туда компилит Unity).
# Brotli-файлы (*.br) заливаются с Content-Encoding: br и правильным Content-Type —
# иначе Unity не загрузится (см. CLAUDE.md / memory про brotli-заголовки).
#
# Использование:
#   ./deploy-web.sh                 # залить Builds/web в бакет по умолчанию
#   BUILD_DIR=/path/to/web ./deploy-web.sh
#   BUCKET=other-bucket ./deploy-web.sh
#
set -euo pipefail

# --- настройки (можно переопределить через env) ---
BUCKET="${BUCKET:-is-this-a-mimic}"
REGION="${REGION:-us-east-1}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${BUILD_DIR:-$SCRIPT_DIR/Builds/web}"

# --- проверки ---
command -v aws >/dev/null 2>&1 || { echo "ОШИБКА: aws cli не установлен." >&2; exit 1; }

if [[ ! -f "$BUILD_DIR/index.html" ]]; then
  echo "ОШИБКА: не найден билд в $BUILD_DIR (нет index.html)." >&2
  echo "Сначала собери WebGL-билд в эту папку или укажи BUILD_DIR=..." >&2
  exit 1
fi

echo ">> Бакет:  s3://$BUCKET ($REGION)"
echo ">> Билд:   $BUILD_DIR"
echo

# --- 1. всё кроме .br: обычная синхронизация с очисткой устаревших файлов ---
# index.html и *.js всегда revalidate, чтобы не залипал старый билд.
echo ">> Синхронизирую статику (html / js / TemplateData)..."
aws s3 sync "$BUILD_DIR" "s3://$BUCKET" \
  --region "$REGION" \
  --delete \
  --exclude "*.br" \
  --cache-control "no-cache"

# --- 2. brotli-файлы: с Content-Encoding: br и корректным Content-Type ---
upload_br() {
  local file="$1" ctype="$2"
  local key="Build/$file"
  if [[ ! -f "$BUILD_DIR/Build/$file" ]]; then
    echo "   пропуск: $key (нет локально)"
    return
  fi
  echo "   $key  ->  $ctype + Content-Encoding: br"
  aws s3 cp "$BUILD_DIR/Build/$file" "s3://$BUCKET/$key" \
    --region "$REGION" \
    --content-encoding br \
    --content-type "$ctype" \
    --cache-control "no-cache" \
    --quiet
}

echo ">> Заливаю brotli-файлы с правильными заголовками..."
upload_br "web.data.br"         "application/octet-stream"
upload_br "web.wasm.br"         "application/wasm"
upload_br "web.framework.js.br" "application/javascript"

echo
echo "ГОТОВО. Открывай по HTTPS (br работает только по https):"
echo "  https://$BUCKET.s3.$REGION.amazonaws.com/index.html"
