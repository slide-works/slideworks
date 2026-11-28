import tempfile
import unittest
from pathlib import Path

import listing_support  # puts store/ on sys.path
import check_listing
import stage_images


class CheckListingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        stage_images.stage(listing_support.make_store(root), root / "out", [])
        self.staged = root / "out"
        self.ios = self.staged / "ios" / "metadata" / "en-US"
        self.android = self.staged / "android" / "metadata" / "en-US"

    def problems(self):
        return check_listing.check(self.staged)

    def test_valid_listing_passes(self):
        self.assertEqual([], self.problems())

    def test_text_with_trailing_newline_fails(self):
        (self.ios / "subtitle.txt").write_text("Fixture sub\n")
        self.assertEqual(["ios/metadata/en-US/subtitle.txt: has a trailing newline or space "
                          "(supply and deliver send the bytes as they are)"], self.problems())

    def test_text_with_trailing_space_fails(self):
        (self.android / "title.txt").write_text("Fixture ")
        self.assertIn("trailing newline or space", self.problems()[0])

    def test_over_limit_fails_naming_field(self):
        (self.ios / "keywords.txt").write_text("k" * 101)
        (self.android / "short_description.txt").write_text("s" * 81)
        problems = self.problems()
        self.assertEqual(2, len(problems))
        self.assertIn("ios/metadata/en-US/keywords.txt: 101 characters, limit 100", problems)
        self.assertIn("android/metadata/en-US/short_description.txt: 81 characters, limit 80", problems)

    def test_at_limit_passes(self):
        (self.ios / "name.txt").write_text("n" * 30)
        (self.android / "full_description.txt").write_text("f" * 4000)
        self.assertEqual([], self.problems())

    def test_wrong_screenshot_count_fails(self):
        (self.android / "images" / "phoneScreenshots" / "5-screen5.png").unlink()
        self.assertEqual(["android/metadata/en-US/images/phoneScreenshots: expected 5 screenshots, found 4"],
                         self.problems())

    def test_wrong_pixel_size_fails(self):
        shot = self.staged / "ios" / "screenshots" / "en-US" / "ios-6.5_2-screen2.png"
        shot.write_bytes(listing_support.png(1320, 2868))
        self.assertEqual(["ios/screenshots/en-US/ios-6.5_2-screen2.png: pixel size 1320x2868, expected 1284x2778"],
                         self.problems())

    def test_wrong_play_icon_and_feature_size_fail(self):
        (self.android / "images" / "icon.png").write_bytes(listing_support.png(511, 512))
        (self.android / "images" / "featureGraphic.png").write_bytes(listing_support.png(1024, 501))
        problems = self.problems()
        self.assertEqual(2, len(problems))
        self.assertIn("icon.png: pixel size 511x512, expected 512x512", problems[0])
        self.assertIn("featureGraphic.png: pixel size 1024x501, expected 1024x500", problems[1])

    def test_missing_text_file_fails(self):
        (self.ios / "description.txt").unlink()
        self.assertEqual(["ios/metadata/en-US/description.txt: missing"], self.problems())

    def test_badly_named_screenshot_fails(self):
        (self.android / "images" / "phoneScreenshots" / "5-screen5.png").rename(
            self.android / "images" / "phoneScreenshots" / "last.png")
        self.assertTrue(any("last.png: name must be" in p for p in self.problems()))

    def test_main_exits_non_zero_naming_violation(self):
        (self.ios / "name.txt").write_text("n" * 31)
        self.assertEqual(1, check_listing.main([str(self.staged)]))


if __name__ == "__main__":
    unittest.main()
