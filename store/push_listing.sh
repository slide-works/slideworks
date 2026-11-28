#!/usr/bin/env bash
# The only place fastlane is called.
#
#   store/push_listing.sh <ios|android> <push|verify|submit>
#
#   push    stage and check the listing, then upload it (deliver / supply). Never submits for review and never
#           releases (automatic_release false).
#   verify  dry run. Android: a Play edit with validate_only, which checks text and images and discards the edit.
#           iOS: deliver has no validate-only mode, so this only downloads the current listing; it proves the key,
#           the app record and the runner, not the text or screenshots.
#   submit  never touches the listing. Android: promotes the internal track's rolled-out release to PLAY_TRACK
#           (alpha|production) with PLAY_RELEASE_STATUS (completed|draft). iOS: attaches the newest processed
#           TestFlight build to the version on Prepare for Submission and submits it for review.
#
# Secrets come from the environment: ASC_API_KEY_ID, ASC_API_KEY_ISSUER_ID, ASC_API_KEY_P8 (iOS) and
# GOOGLE_PLAY_SERVICE_ACCOUNT_JSON (Android). iOS push also needs APP_VERSION (not a secret). APP_IDENTIFIER defaults to the app's bundle id and package name.
set -euo pipefail

store_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
app_identifier="${APP_IDENTIFIER:-app.slideworks.mobile}"

fail() {
  echo "push_listing: $*" >&2
  exit 1
}

require_secret() {
  local name="$1"
  [[ -n "${!name:-}" ]] || fail "Add the \`${name}\` repository secret first"
}

usage() {
  fail "usage: store/push_listing.sh <ios|android> <push|verify|submit>"
}

[[ $# -eq 2 ]] || usage
platform="$1"
mode="$2"
[[ "$platform" == ios || "$platform" == android ]] || usage
[[ "$mode" == push || "$mode" == verify || "$mode" == submit ]] || usage

# Not a secret: the version string deliver creates or renames the Prepare for Submission version to.
if [[ "$platform $mode" == "ios push" ]]; then
  [[ -n "${APP_VERSION:-}" ]] || fail "Set APP_VERSION first (the app version, as store/app_version.py --print gives it)"
fi

case "$platform" in
  ios)
    require_secret ASC_API_KEY_ID
    require_secret ASC_API_KEY_ISSUER_ID
    require_secret ASC_API_KEY_P8
    ;;
  android)
    require_secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON
    ;;
esac

if [[ "$platform $mode" == "android submit" ]]; then
  play_track="${PLAY_TRACK:-}"
  play_status="${PLAY_RELEASE_STATUS:-}"
  [[ "$play_track" == alpha || "$play_track" == production ]] || fail "PLAY_TRACK must be alpha or production"
  [[ "$play_status" == completed || "$play_status" == draft ]] || fail "PLAY_RELEASE_STATUS must be completed or draft"
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export BUNDLE_GEMFILE="$store_dir/Gemfile"
# A runner's system gem folder may not be writable, so fastlane installs under a folder of ours.
export BUNDLE_PATH="${RUNNER_TEMP:-$work}/store-bundle"
bundle install --quiet

stage_and_check() {
  python3 -I "$store_dir/stage_images.py" --store "$store_dir" --out "$work/staged"
  python3 -I "$store_dir/check_listing.py" "$work/staged"
}

ios_key() {
  # fastlane reads the App Store Connect key as a JSON file; the secret is the .p8 text.
  python3 -I - "$work/asc_key.json" <<'PY'
import json, os, sys
with open(sys.argv[1], "w") as f:
    json.dump({"key_id": os.environ["ASC_API_KEY_ID"], "issuer_id": os.environ["ASC_API_KEY_ISSUER_ID"],
               "key": os.environ["ASC_API_KEY_P8"], "in_house": False}, f)
PY
}

play_key() {
  printf '%s' "$GOOGLE_PLAY_SERVICE_ACCOUNT_JSON" > "$work/play_key.json"
}

play_skips=(--skip_upload_apk true --skip_upload_aab true --skip_upload_changelogs true)

if [[ "$platform" == ios ]]; then
  ios_key
  case "$mode" in
    push)
      stage_and_check
      bundle exec fastlane deliver --api_key_path "$work/asc_key.json" --app_identifier "$app_identifier" \
        --metadata_path "$work/staged/ios/metadata" --screenshots_path "$work/staged/ios/screenshots" \
        --app_rating_config_path "$work/staged/ios/age_rating.json" \
        --skip_binary_upload true --automatic_release false --submit_for_review false \
        --app_version "$APP_VERSION" \
        --overwrite_screenshots true --force true \
        --precheck_include_in_app_purchases false
      ;;
    verify)
      bundle exec fastlane deliver download_metadata --api_key_path "$work/asc_key.json" \
        --app_identifier "$app_identifier" --metadata_path "$work/current" --force true
      ;;
    submit)
      bundle exec fastlane deliver --api_key_path "$work/asc_key.json" --app_identifier "$app_identifier" \
        --skip_metadata true --skip_screenshots true --skip_binary_upload true \
        --build_number latest --submit_for_review true --automatic_release false --force true \
        --precheck_include_in_app_purchases false \
        --submission_information '{"export_compliance_uses_encryption": false, "add_id_info_uses_idfa": false}'
      ;;
  esac
else
  play_key
  case "$mode" in
    push)
      stage_and_check
      bundle exec fastlane supply --json_key "$work/play_key.json" --package_name "$app_identifier" \
        --track internal --metadata_path "$work/staged/android/metadata" "${play_skips[@]}"
      ;;
    verify)
      stage_and_check
      bundle exec fastlane supply --json_key "$work/play_key.json" --package_name "$app_identifier" \
        --track internal --metadata_path "$work/staged/android/metadata" "${play_skips[@]}" --validate_only true
      ;;
    submit)
      bundle exec fastlane supply --json_key "$work/play_key.json" --package_name "$app_identifier" \
        --track internal --track_promote_to "$play_track" --release_status completed --track_promote_release_status "$play_status" \
        --skip_upload_metadata true --skip_upload_images true --skip_upload_screenshots true "${play_skips[@]}"
      ;;
  esac
fi
