#!/usr/bin/env python3
"""The app's version: major.minor come from app.json (changed by hand), the patch is the release run number.

    APP_VERSION_PATCH=42 store/app_version.py --write    rewrites expo.version to M.m.42 and prints it
    APP_VERSION_PATCH=42 store/app_version.py --print    prints M.m.42 without writing
"""
import argparse
import json
import os
import re
import sys
from pathlib import Path

DEFAULT_APP_JSON = Path(__file__).resolve().parent.parent / "src" / "SlideWorks.Mobile" / "app.json"
VERSION = re.compile(r"^(\d+)\.(\d+)\.\d+$")


def parse_patch(raw):
    if raw is None or not re.fullmatch(r"[0-9]+", raw) or int(raw) < 1:
        raise ValueError("APP_VERSION_PATCH must be a positive integer")
    return int(raw)


def next_version(current, patch):
    match = VERSION.match(current)
    if not match:
        raise ValueError(f"expo.version must look like M.m.x, found {current!r}")
    return f"{match.group(1)}.{match.group(2)}.{patch}"


def main(argv=None, environ=None):
    environ = os.environ if environ is None else environ
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="rewrite expo.version in app.json and print it")
    mode.add_argument("--print", action="store_true", dest="print_only", help="print the version without writing")
    parser.add_argument("--app-json", type=Path, default=DEFAULT_APP_JSON)
    args = parser.parse_args(argv)

    try:
        patch = parse_patch(environ.get("APP_VERSION_PATCH"))
    except ValueError as error:
        print(error, file=sys.stderr)
        return 2

    config = json.loads(args.app_json.read_text(encoding="utf-8"))
    try:
        version = next_version(config["expo"]["version"], patch)
    except (KeyError, ValueError) as error:
        print(f"{args.app_json}: {error}", file=sys.stderr)
        return 1
    if args.write:
        config["expo"]["version"] = version
        args.app_json.write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")
    print(version)
    return 0


if __name__ == "__main__":
    sys.exit(main())
