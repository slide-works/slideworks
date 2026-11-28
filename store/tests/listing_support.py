"""Shared helpers for the store listing tests: a valid fixture store tree built in a temp folder."""
import functools
import shutil
import struct
import sys
import zlib
from pathlib import Path

STORE = Path(__file__).resolve().parents[1]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "listing" / "store"
sys.path.insert(0, str(STORE))

import check_listing  # noqa: E402

SETS = {**check_listing.IOS_SIZES, "android-phone": (1080, 1920),
        "android-icon": check_listing.PLAY_ICON, "android-feature": check_listing.PLAY_FEATURE}


@functools.cache
def png(width: int, height: int) -> bytes:
    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    row = b"\x00" + b"\x00\x00\x00" * width
    return (check_listing.PNG_SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(row * height)) + chunk(b"IEND", b""))


def make_store(root: Path, count: int = check_listing.SCREENSHOTS_PER_SET) -> Path:
    """Copies the text fixtures to root/store and writes the exported images (one blank PNG each)."""
    store = root / "store"
    shutil.copytree(FIXTURE, store)
    shutil.copy(STORE / "push_listing.sh", store / "push_listing.sh")
    shutil.copy(STORE / "stage_images.py", store / "stage_images.py")
    shutil.copy(STORE / "check_listing.py", store / "check_listing.py")
    (store / "Gemfile").write_text("source 'https://rubygems.org'\n")
    for name, (width, height) in SETS.items():
        folder = store / "assets" / "export" / name
        folder.mkdir(parents=True)
        shots = count if name in (*check_listing.IOS_SIZES, "android-phone") else 1
        for n in range(1, shots + 1):
            (folder / f"{n}-screen{n}.png").write_bytes(png(width, height))
    return store
