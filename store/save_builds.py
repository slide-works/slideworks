#!/usr/bin/env python3
"""Save the files of the builds EAS made.

    store/save_builds.py --builds builds.json --out app-builds

builds.json is the JSON `eas build --json` printed. With --auto-submit that JSON is each build as it was created,
with no file URL yet, so every build is looked up again with `eas build:view <id> --json` and its
`artifacts.buildUrl` is downloaded. Files are named slideworks-<platform>-<version>-<build>.<aab|ipa>.
A build that cannot be saved is reported and the others are still saved; the exit status is then 1.
"""
import argparse
import json
import subprocess
import sys
import urllib.request
from pathlib import Path

# expo.dev answers Python's default "Python-urllib" agent with 403.
USER_AGENT = "slideworks-save-builds/1.0 (+https://github.com/slide-works/slideworks)"
EXTENSIONS = {"android": "aab", "ios": "ipa"}


def build_ids(listing):
    if isinstance(listing, dict):
        listing = [listing]
    return [build["id"] for build in listing]


def look_up(eas, build_id):
    result = subprocess.run(
        [*eas, "build:view", build_id, "--json"], capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        raise RuntimeError(f"eas build:view {build_id} failed: {result.stderr.strip() or result.stdout.strip()}")
    return json.loads(result.stdout)


def download(url, target):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=300) as response, open(target, "wb") as handle:
        while chunk := response.read(1 << 20):
            handle.write(chunk)


def file_name(build):
    platform = build["platform"].lower()
    return f"slideworks-{platform}-{build['appVersion']}-{build['appBuildVersion']}.{EXTENSIONS[platform]}"


def save_builds(listing, out_dir, eas=("eas",), fetch=download):
    """Returns (saved file names, failure messages)."""
    out_dir.mkdir(parents=True, exist_ok=True)
    saved, failures = [], []
    for build_id in build_ids(listing):
        try:
            build = look_up(list(eas), build_id)
            url = (build.get("artifacts") or {}).get("buildUrl")
            if not url:
                raise RuntimeError(f"build {build_id} has no file (status {build.get('status')})")
            name = file_name(build)
            fetch(url, out_dir / name)
            saved.append(name)
        except Exception as error:  # one failed build must not lose the others
            failures.append(f"{build_id}: {error}")
    return saved, failures


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--builds", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--eas", default="eas", help="the eas command (default: eas)")
    args = parser.parse_args(argv)

    text = args.builds.read_text(encoding="utf-8").strip() if args.builds.exists() else ""
    if not text:
        print("No builds to save: EAS printed none.")
        args.out.mkdir(parents=True, exist_ok=True)
        return 0
    saved, failures = save_builds(json.loads(text), args.out, eas=args.eas.split())
    for name in saved:
        print(f"saved {name}")
    for failure in failures:
        print(f"FAILED {failure}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
