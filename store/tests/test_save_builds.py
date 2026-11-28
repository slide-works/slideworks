import contextlib
import http.server
import io
import json
import os
import stat
import sys
import tempfile
import threading
import unittest
from pathlib import Path

STORE = Path(__file__).resolve().parent.parent
FIXTURES = STORE / "tests" / "fixtures" / "mobile-release"
sys.path.insert(0, str(STORE))

import save_builds  # noqa: E402


class FileServer(http.server.ThreadingHTTPServer):
    """Serves /files/<name> and remembers the User-Agent of each request."""

    def __init__(self):
        self.user_agents = []
        super().__init__(("127.0.0.1", 0), self.handler())

    def handler(self):
        server = self

        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):  # noqa: N802 (name fixed by BaseHTTPRequestHandler)
                agent = self.headers.get("User-Agent", "")
                server.user_agents.append(agent)
                if agent.startswith("Python-urllib"):
                    self.send_response(403)
                    self.end_headers()
                    return
                body = f"bytes of {self.path}".encode()
                self.send_response(200)
                # /truncated/ promises more than it sends, then hangs up
                self.send_header("Content-Length", str(len(body) + (100 if "/truncated/" in self.path else 0)))
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *args):
                pass

        return Handler

    @property
    def base(self):
        return f"http://127.0.0.1:{self.server_address[1]}"


class SaveBuildsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.server = FileServer()
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.addCleanup(self.cleanup)
        self.out = self.directory / "app-builds"
        self.eas_calls = self.directory / "eas-calls.txt"

    def cleanup(self):
        self.server.shutdown()
        self.server.server_close()

    def fake_eas(self, views):
        """A stand-in `eas` that logs its arguments and answers build:view from a view fixture per build id."""
        folder = self.directory / "views"
        folder.mkdir(exist_ok=True)
        for build_id, fixture in views.items():
            text = (FIXTURES / fixture).read_text(encoding="utf-8").replace("__BASE__", self.server.base)
            (folder / f"{build_id}.json").write_text(text, encoding="utf-8")
        script = self.directory / "eas"
        script.write_text(
            "#!/bin/sh\n"
            f'echo "$@" >> "{self.eas_calls}"\n'
            f'[ "$1" = build:view ] && [ "$3" = --json ] && cat "{folder}/$2.json"\n',
            encoding="utf-8",
        )
        script.chmod(script.stat().st_mode | stat.S_IEXEC)
        return str(script)

    def listing(self):
        return json.loads((FIXTURES / "builds.json").read_text(encoding="utf-8"))

    def test_sends_user_agent(self):
        eas = self.fake_eas({"b-android": "build_view_android.json", "b-ios": "build_view_ios.json"})
        saved, failures = save_builds.save_builds(self.listing(), self.out, eas=[eas])
        self.assertEqual([], failures)
        self.assertEqual(2, len(saved))
        self.assertEqual(2, len(self.server.user_agents))
        for agent in self.server.user_agents:
            self.assertEqual(save_builds.USER_AGENT, agent)
            self.assertFalse(agent.startswith("Python-urllib"))

    def test_names_files_by_platform_version_build(self):
        eas = self.fake_eas({"b-android": "build_view_android.json", "b-ios": "build_view_ios.json"})
        save_builds.save_builds(self.listing(), self.out, eas=[eas])
        self.assertEqual(
            ["slideworks-android-1.4.57-23.aab", "slideworks-ios-1.4.57-8.ipa"],
            sorted(path.name for path in self.out.iterdir()),
        )
        self.assertEqual(
            "bytes of /files/android.aab", (self.out / "slideworks-android-1.4.57-23.aab").read_text()
        )

    def test_relooks_up_each_build(self):
        eas = self.fake_eas({"b-android": "build_view_android.json", "b-ios": "build_view_ios.json"})
        save_builds.save_builds(self.listing(), self.out, eas=[eas])
        self.assertEqual(
            ["build:view b-android --json", "build:view b-ios --json"],
            self.eas_calls.read_text().splitlines(),
        )

    def test_partial_failure_still_saves(self):
        eas = self.fake_eas({"b-android": "build_view_android.json", "b-ios": "build_view_errored.json"})
        saved, failures = save_builds.save_builds(self.listing(), self.out, eas=[eas])
        self.assertEqual(["slideworks-android-1.4.57-23.aab"], saved)
        self.assertEqual(1, len(failures))
        self.assertIn("b-ios", failures[0])
        self.assertEqual(["slideworks-android-1.4.57-23.aab"], [path.name for path in self.out.iterdir()])

    def test_failed_download_leaves_no_file(self):
        target = self.out / "slideworks-android-1.4.57-23.aab"
        self.out.mkdir()
        with self.assertRaisesRegex(OSError, 'ended after'):
            save_builds.download(f"{self.server.base}/truncated/android.aab", target)
        self.assertEqual([], list(self.out.iterdir()))

    def test_successful_download_leaves_only_the_final_name(self):
        target = self.out / "slideworks-android-1.4.57-23.aab"
        self.out.mkdir()
        save_builds.download(f"{self.server.base}/files/android.aab", target)
        self.assertEqual(["slideworks-android-1.4.57-23.aab"], [path.name for path in self.out.iterdir()])

    def test_cli_exits_one_on_partial_failure(self):
        eas = self.fake_eas({"b-android": "build_view_android.json", "b-ios": "build_view_errored.json"})
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            status = save_builds.main(["--builds", str(FIXTURES / "builds.json"), "--out", str(self.out), "--eas", eas])
        self.assertEqual(1, status)
        self.assertTrue((self.out / "slideworks-android-1.4.57-23.aab").exists())


if __name__ == "__main__":
    unittest.main()
