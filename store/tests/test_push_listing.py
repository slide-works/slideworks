import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

import listing_support

FAKE_BUNDLE = """#!/usr/bin/env python3
import json, os, sys
args = sys.argv[1:]
record = {"args": args, "gemfile": os.environ.get("BUNDLE_GEMFILE"), "dirs": {}}
for flag in ("--metadata_path", "--screenshots_path"):
    if flag in args:
        path = args[args.index(flag) + 1]
        record["dirs"][flag] = os.path.isdir(path)
if "--json_key" in args:
    record["json_key"] = open(args[args.index("--json_key") + 1]).read()
if "--api_key_path" in args:
    record["api_key"] = json.load(open(args[args.index("--api_key_path") + 1]))
with open(os.environ["FAKE_BUNDLE_LOG"], "a") as log:
    log.write(json.dumps(record) + "\\n")
"""

SECRETS = {
    "ASC_API_KEY_ID": "KEYID",
    "ASC_API_KEY_ISSUER_ID": "ISSUER",
    "ASC_API_KEY_P8": "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----",
    "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON": '{"type": "service_account"}',
}


class PushListingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.store = listing_support.make_store(root)
        bin_dir = root / "bin"
        bin_dir.mkdir()
        bundle = bin_dir / "bundle"
        bundle.write_text(FAKE_BUNDLE)
        bundle.chmod(0o755)
        self.log = root / "bundle.log"
        self.env_base = {"PATH": f"{bin_dir}:/usr/bin:/bin", "FAKE_BUNDLE_LOG": str(self.log),
                         "RUNNER_TEMP": str(root)}

    def run_script(self, platform, mode, **env):
        merged = {**self.env_base, **env}
        return subprocess.run(["bash", str(self.store / "push_listing.sh"), platform, mode], env=merged,
                              capture_output=True, text=True, check=False)

    def calls(self):
        if not self.log.exists():
            return []
        return [json.loads(line) for line in self.log.read_text().splitlines()]

    def fastlane_calls(self):
        return [c for c in self.calls() if c["args"][:2] == ["exec", "fastlane"]]

    def test_push_never_submits(self):
        for platform in ("ios", "android"):
            result = self.run_script(platform, "push", **SECRETS)
            self.assertEqual(0, result.returncode, result.stderr)
        ios, android = self.fastlane_calls()
        self.assertEqual(["deliver"], ios["args"][2:3])
        self.assertIn("--submit_for_review false", " ".join(ios["args"]))
        self.assertIn("--automatic_release false", " ".join(ios["args"]))
        self.assertNotIn("true", ios["args"][ios["args"].index("--submit_for_review") + 1])
        self.assertEqual({"--metadata_path": True, "--screenshots_path": True}, ios["dirs"])
        self.assertEqual("KEYID", ios["api_key"]["key_id"])
        self.assertEqual(["supply"], android["args"][2:3])
        self.assertNotIn("--track_promote_to", android["args"])
        self.assertNotIn("--validate_only", android["args"])
        self.assertEqual({"--metadata_path": True}, android["dirs"])
        self.assertEqual(SECRETS["GOOGLE_PLAY_SERVICE_ACCOUNT_JSON"], android["json_key"])
        self.assertTrue(android["gemfile"].endswith("store/Gemfile"))

    def test_push_refuses_a_listing_that_fails_the_checks(self):
        (self.store / "android" / "metadata" / "en-US" / "title.txt").write_text("t" * 31)
        result = self.run_script("android", "push", **SECRETS)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("title.txt: 31 characters, limit 30", result.stdout)
        self.assertEqual([], self.fastlane_calls())

    def test_verify_android_is_validate_only(self):
        result = self.run_script("android", "verify", **SECRETS)
        self.assertEqual(0, result.returncode, result.stderr)
        (call,) = self.fastlane_calls()
        self.assertEqual("--validate_only true", " ".join(call["args"][call["args"].index("--validate_only"):]))
        self.assertNotIn("--track_promote_to", call["args"])

    def test_verify_ios_only_downloads(self):
        result = self.run_script("ios", "verify", **SECRETS)
        self.assertEqual(0, result.returncode, result.stderr)
        (call,) = self.fastlane_calls()
        self.assertEqual(["deliver", "download_metadata"], call["args"][2:4])
        self.assertNotIn("--submit_for_review", call["args"])
        self.assertNotIn("--screenshots_path", call["args"])

    def test_submit_play_uses_chosen_track_and_status(self):
        result = self.run_script("android", "submit", PLAY_TRACK="production", PLAY_RELEASE_STATUS="draft", **SECRETS)
        self.assertEqual(0, result.returncode, result.stderr)
        (call,) = self.fastlane_calls()
        args = call["args"]
        self.assertEqual("internal", args[args.index("--track") + 1])
        self.assertEqual("production", args[args.index("--track_promote_to") + 1])
        # --release_status only picks which internal release to promote; the promoted status is its own flag.
        self.assertEqual("completed", args[args.index("--release_status") + 1])
        self.assertEqual("draft", args[args.index("--track_promote_release_status") + 1])
        self.assertNotIn("--metadata_path", args)
        self.assertEqual("true", args[args.index("--skip_upload_metadata") + 1])
        self.assertEqual("true", args[args.index("--skip_upload_images") + 1])

    def test_submit_play_refuses_unknown_track_or_status(self):
        for track, status in (("beta", "completed"), ("alpha", "halted"), ("", "")):
            result = self.run_script("android", "submit", PLAY_TRACK=track, PLAY_RELEASE_STATUS=status, **SECRETS)
            self.assertNotEqual(0, result.returncode)
        self.assertEqual([], self.fastlane_calls())

    def test_submit_ios_attaches_newest_build_and_submits(self):
        result = self.run_script("ios", "submit", **SECRETS)
        self.assertEqual(0, result.returncode, result.stderr)
        (call,) = self.fastlane_calls()
        args = call["args"]
        self.assertEqual("latest", args[args.index("--build_number") + 1])
        self.assertEqual("true", args[args.index("--submit_for_review") + 1])
        self.assertEqual("false", args[args.index("--automatic_release") + 1])
        self.assertEqual("true", args[args.index("--skip_metadata") + 1])
        self.assertEqual("true", args[args.index("--skip_screenshots") + 1])

    def test_missing_secret_names_it(self):
        cases = (("ios", "push", "ASC_API_KEY_ID"), ("ios", "submit", "ASC_API_KEY_P8"),
                 ("android", "verify", "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON"))
        for platform, mode, missing in cases:
            with self.subTest(missing=missing):
                secrets = {k: v for k, v in SECRETS.items() if k != missing}
                result = self.run_script(platform, mode, **secrets)
                self.assertEqual(1, result.returncode)
                self.assertIn(f"Add the `{missing}` repository secret first", result.stderr)
        self.assertEqual([], self.calls())

    def test_rejects_unknown_platform_or_mode(self):
        self.assertEqual(1, self.run_script("web", "push", **SECRETS).returncode)
        self.assertEqual(1, self.run_script("ios", "release", **SECRETS).returncode)


if __name__ == "__main__":
    unittest.main()
