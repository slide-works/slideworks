#!/usr/bin/env python3
"""Builds fastlane's folder layout from the listing text and the exported store images.

    python3 store/stage_images.py --out <dir>

Layout written under <dir>:
    ios/metadata/<locale>/*.txt, ios/metadata/{primary,secondary}_category.txt,
    ios/metadata/review_information/notes.txt, ios/age_rating.json,
    ios/screenshots/<locale>/<set>_<n>-<screen>.png   (deliver tells device sets apart by pixel size)
    android/metadata/<locale>/{title,short_description,full_description}.txt, changelogs/default.txt,
    android/metadata/<locale>/images/{icon.png,featureGraphic.png,phoneScreenshots/<n>-<screen>.png}

The listing text lives once under SOURCE_LOCALE. Every locale in MIRROR_LOCALES gets a copy at staging, and a
committed copy of a mirrored locale is refused.
"""
import argparse
import re
import shutil
import sys
from pathlib import Path

SOURCE_LOCALE = "en-US"
# Locales the listing publishes in besides SOURCE_LOCALE; each gets a copy of the source text and images.
MIRROR_LOCALES: list[str] = []

IOS_SETS = ("ios-6.9", "ios-6.5", "ios-6.3")
IOS_TEXT_FILES = ("name", "subtitle", "description", "keywords", "promotional_text",
                  "support_url", "privacy_url", "marketing_url")
IOS_ROOT_FILES = ("primary_category.txt", "secondary_category.txt")
ANDROID_TEXT_FILES = ("title.txt", "short_description.txt", "full_description.txt", "changelogs/default.txt")
IMAGE_NAME = re.compile(r"^\d+-[a-z0-9]+(?:-[a-z0-9]+)*\.png$")


class StagingError(Exception):
    pass


def _copy(src: Path, dst: Path) -> None:
    if not src.is_file():
        raise StagingError(f"missing {src}")
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)


def _export_images(store: Path, set_name: str) -> list[Path]:
    folder = store / "assets" / "export" / set_name
    if not folder.is_dir():
        raise StagingError(f"missing image folder {folder}")
    files = sorted(p for p in folder.iterdir() if p.is_file())
    bad = [p.name for p in files if not IMAGE_NAME.match(p.name)]
    if bad:
        raise StagingError(f"{folder}: names must look like <n>-<screen>.png, got {', '.join(bad)}")
    return sorted(files, key=lambda p: int(p.name.split("-", 1)[0]))


def _refuse_committed_mirrors(store: Path, mirror_locales: list[str]) -> None:
    for locale in mirror_locales:
        if locale == SOURCE_LOCALE:
            raise StagingError(f"MIRROR_LOCALES must not contain the source locale {SOURCE_LOCALE}")
        for committed in (store / "ios" / "metadata" / locale, store / "android" / "metadata" / locale):
            if committed.exists():
                raise StagingError(
                    f"{committed} is committed, but {locale} is mirrored from {SOURCE_LOCALE} at staging; delete it")


def stage(store: Path, out: Path, mirror_locales: list[str] | None = None) -> None:
    mirror_locales = MIRROR_LOCALES if mirror_locales is None else mirror_locales
    _refuse_committed_mirrors(store, mirror_locales)
    locales = [SOURCE_LOCALE, *mirror_locales]
    if out.exists() and any(out.iterdir()):
        raise StagingError(f"{out} is not empty")

    ios_src, ios_out = store / "ios", out / "ios"
    for name in IOS_ROOT_FILES:
        _copy(ios_src / "metadata" / name, ios_out / "metadata" / name)
    _copy(ios_src / "metadata" / "review_information" / "notes.txt",
          ios_out / "metadata" / "review_information" / "notes.txt")
    _copy(ios_src / "age_rating.json", ios_out / "age_rating.json")

    android_src, android_out = store / "android", out / "android"
    for locale in locales:
        for name in IOS_TEXT_FILES:
            _copy(ios_src / "metadata" / SOURCE_LOCALE / f"{name}.txt", ios_out / "metadata" / locale / f"{name}.txt")
        for set_name in IOS_SETS:
            for image in _export_images(store, set_name):
                _copy(image, ios_out / "screenshots" / locale / f"{set_name}_{image.name}")
        for name in ANDROID_TEXT_FILES:
            _copy(android_src / "metadata" / SOURCE_LOCALE / name, android_out / "metadata" / locale / name)
        images = android_out / "metadata" / locale / "images"
        for image in _export_images(store, "android-phone"):
            _copy(image, images / "phoneScreenshots" / image.name)
        (icon,) = _single(_export_images(store, "android-icon"), "android-icon")
        _copy(icon, images / "icon.png")
        (feature,) = _single(_export_images(store, "android-feature"), "android-feature")
        _copy(feature, images / "featureGraphic.png")


def _single(files: list[Path], set_name: str) -> list[Path]:
    if len(files) != 1:
        raise StagingError(f"{set_name} must hold exactly one image, found {len(files)}")
    return files


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", required=True, type=Path, help="empty or missing folder to stage into")
    parser.add_argument("--store", type=Path, default=Path(__file__).resolve().parent, help="the store/ folder")
    args = parser.parse_args(argv)
    try:
        stage(args.store, args.out)
    except StagingError as error:
        print(f"stage_images: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
