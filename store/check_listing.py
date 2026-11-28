#!/usr/bin/env python3
"""Checks a staged listing (the output of stage_images.py) against the stores' limits.

    python3 store/check_listing.py <staged dir>

Prints one line per violation and exits 1 when there is any.
"""
import argparse
import re
import struct
import sys
from pathlib import Path

# Screenshots every set must hold. Apple takes 1-10 per set and Play 2-8.
SCREENSHOTS_PER_SET = 5

IOS_SIZES = {"ios-6.9": (1320, 2868), "ios-6.5": (1284, 2778), "ios-6.3": (1206, 2622)}
PLAY_ICON = (512, 512)
PLAY_FEATURE = (1024, 500)

IOS_LIMITS = {"name": 30, "subtitle": 30, "keywords": 100, "promotional_text": 170, "description": 4000}
ANDROID_LIMITS = {"title": 30, "short_description": 80, "full_description": 4000, "changelogs/default": 500}
IOS_REQUIRED = ("name", "subtitle", "description", "keywords", "promotional_text",
                "support_url", "privacy_url", "marketing_url")
ANDROID_REQUIRED = ("title", "short_description", "full_description", "changelogs/default")

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
SCREENSHOT_NAME = re.compile(r"^(?:(?P<set>ios-6\.9|ios-6\.5|ios-6\.3)_)?(?P<n>\d+)-[a-z0-9]+(?:-[a-z0-9]+)*\.png$")


def png_size(path: Path) -> tuple[int, int] | None:
    header = path.read_bytes()[:24]
    if len(header) < 24 or header[:8] != PNG_SIGNATURE or header[12:16] != b"IHDR":
        return None
    return struct.unpack(">II", header[16:24])


def _text(path: Path, rel: str, limit: int | None, problems: list[str]) -> None:
    if not path.is_file():
        problems.append(f"{rel}: missing")
        return
    text = path.read_text(encoding="utf-8")
    if text != text.rstrip():
        problems.append(f"{rel}: has a trailing newline or space (supply and deliver send the bytes as they are)")
    elif any(line != line.rstrip() for line in text.split("\n")):
        problems.append(f"{rel}: a line ends with a space")
    if limit is not None and len(text) > limit:
        problems.append(f"{rel}: {len(text)} characters, limit {limit}")


def _locales(folder: Path) -> list[Path]:
    return sorted(p for p in folder.iterdir() if p.is_dir() and re.match(r"^[a-z]{2,3}(-[A-Za-z0-9]+)*$", p.name)) \
        if folder.is_dir() else []


def _screenshots(folder: Path, rel: str, expected: int, sizes: dict[str, tuple[int, int]] | None,
                 single: tuple[int, int] | None, problems: list[str]) -> None:
    files = sorted(p for p in folder.iterdir() if p.is_file()) if folder.is_dir() else []
    groups: dict[str, list[Path]] = {}
    for f in files:
        match = SCREENSHOT_NAME.match(f.name)
        if not match:
            problems.append(f"{rel}/{f.name}: name must be <n>-<screen>.png (iOS: <set>_<n>-<screen>.png)")
            continue
        groups.setdefault(match.group("set") or "", []).append(f)
    expected_groups = list(sizes) if sizes else [""]
    for group in expected_groups:
        label = f"{rel}/{group}" if group else rel
        got = len(groups.get(group, []))
        if got != expected:
            problems.append(f"{label}: expected {expected} screenshots, found {got}")
    for group, members in groups.items():
        want = sizes[group] if sizes and group in sizes else single
        for f in members:
            size = png_size(f)
            if size is None:
                problems.append(f"{rel}/{f.name}: not a PNG")
            elif want and size != want:
                problems.append(f"{rel}/{f.name}: pixel size {size[0]}x{size[1]}, expected {want[0]}x{want[1]}")


def _single_image(path: Path, rel: str, want: tuple[int, int], problems: list[str]) -> None:
    if not path.is_file():
        problems.append(f"{rel}: missing")
        return
    size = png_size(path)
    if size is None:
        problems.append(f"{rel}: not a PNG")
    elif size != want:
        problems.append(f"{rel}: pixel size {size[0]}x{size[1]}, expected {want[0]}x{want[1]}")


def check(staged: Path, screenshots: int = SCREENSHOTS_PER_SET) -> list[str]:
    problems: list[str] = []
    ios = staged / "ios"
    for name in ("primary_category", "secondary_category"):
        _text(ios / "metadata" / f"{name}.txt", f"ios/metadata/{name}.txt", None, problems)
    _text(ios / "metadata" / "review_information" / "notes.txt", "ios/metadata/review_information/notes.txt", None, problems)
    if not (ios / "age_rating.json").is_file():
        problems.append("ios/age_rating.json: missing")
    ios_locales = _locales(ios / "metadata")
    if not ios_locales:
        problems.append("ios/metadata: no locale folder")
    for locale in ios_locales:
        for name in IOS_REQUIRED:
            _text(locale / f"{name}.txt", f"ios/metadata/{locale.name}/{name}.txt", IOS_LIMITS.get(name), problems)
        _screenshots(ios / "screenshots" / locale.name, f"ios/screenshots/{locale.name}", screenshots,
                     IOS_SIZES, None, problems)

    android_locales = _locales(staged / "android" / "metadata")
    if not android_locales:
        problems.append("android/metadata: no locale folder")
    for locale in android_locales:
        base = f"android/metadata/{locale.name}"
        for name in ANDROID_REQUIRED:
            _text(locale / f"{name}.txt", f"{base}/{name}.txt", ANDROID_LIMITS.get(name), problems)
        _screenshots(locale / "images" / "phoneScreenshots", f"{base}/images/phoneScreenshots", screenshots,
                     None, None, problems)
        _single_image(locale / "images" / "icon.png", f"{base}/images/icon.png", PLAY_ICON, problems)
        _single_image(locale / "images" / "featureGraphic.png", f"{base}/images/featureGraphic.png",
                      PLAY_FEATURE, problems)
    return problems


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("staged", type=Path, help="the folder stage_images.py wrote")
    args = parser.parse_args(argv)
    problems = check(args.staged)
    for problem in problems:
        print(problem)
    if problems:
        print(f"check_listing: {len(problems)} problem(s)", file=sys.stderr)
        return 1
    print("check_listing: ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
