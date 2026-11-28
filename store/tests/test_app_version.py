import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

STORE = Path(__file__).resolve().parent.parent
FIXTURE = STORE / "tests" / "fixtures" / "release" / "app.json"


def run_script(mode, app_json, patch):
    env = {key: value for key, value in os.environ.items() if key != "APP_VERSION_PATCH"}
    if patch is not None:
        env["APP_VERSION_PATCH"] = patch
    return subprocess.run(
        [sys.executable, "-I", str(STORE / "app_version.py"), mode, "--app-json", str(app_json)],
        capture_output=True,
        text=True,
        env=env,
        check=False,
    )


class AppVersionTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.app_json = Path(self.directory.name) / "app.json"
        self.app_json.write_text(FIXTURE.read_text(encoding="utf-8"), encoding="utf-8")

    def written_version(self):
        return json.loads(self.app_json.read_text(encoding="utf-8"))["expo"]["version"]

    def test_write_sets_patch(self):
        result = run_script("--write", self.app_json, "57")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("1.4.57", result.stdout.strip())
        self.assertEqual("1.4.57", self.written_version())

    def test_refuses_non_integer(self):
        for bad in ("abc", "0", "-3", "1.5", "", None):
            with self.subTest(patch=bad):
                result = run_script("--write", self.app_json, bad)
                self.assertEqual(2, result.returncode)
                self.assertIn("APP_VERSION_PATCH must be a positive integer", result.stderr)
                self.assertEqual("1.4.0", self.written_version())

    def test_print_does_not_write(self):
        before = self.app_json.read_text(encoding="utf-8")
        result = run_script("--print", self.app_json, "9")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("1.4.9", result.stdout.strip())
        self.assertEqual(before, self.app_json.read_text(encoding="utf-8"))

    def test_keeps_major_minor(self):
        self.app_json.write_text(
            json.dumps({"expo": {"name": "SlideWorks", "version": "12.30.0"}}), encoding="utf-8"
        )
        result = run_script("--write", self.app_json, "100")
        self.assertEqual(0, result.returncode, result.stderr)
        config = json.loads(self.app_json.read_text(encoding="utf-8"))
        self.assertEqual("12.30.100", config["expo"]["version"])
        self.assertEqual("SlideWorks", config["expo"]["name"])


if __name__ == "__main__":
    unittest.main()
