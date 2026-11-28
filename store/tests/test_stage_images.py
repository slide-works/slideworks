import tempfile
import unittest
from pathlib import Path

import listing_support
import stage_images


class StageImagesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.store = listing_support.make_store(self.root)
        self.out = self.root / "out"

    def test_stages_fastlane_layout(self):
        stage_images.stage(self.store, self.out, [])
        ios = self.out / "ios"
        self.assertEqual("Fixture", (ios / "metadata" / "en-US" / "name.txt").read_text())
        self.assertTrue((ios / "metadata" / "review_information" / "notes.txt").is_file())
        self.assertTrue((ios / "metadata" / "primary_category.txt").is_file())
        self.assertTrue((ios / "age_rating.json").is_file())
        self.assertTrue((ios / "screenshots" / "en-US" / "ios-6.9_1-screen1.png").is_file())
        self.assertTrue((ios / "screenshots" / "en-US" / "ios-6.3_5-screen5.png").is_file())
        android = self.out / "android" / "metadata" / "en-US"
        self.assertTrue((android / "changelogs" / "default.txt").is_file())
        self.assertTrue((android / "images" / "icon.png").is_file())
        self.assertTrue((android / "images" / "featureGraphic.png").is_file())
        self.assertTrue((android / "images" / "phoneScreenshots" / "2-screen2.png").is_file())

    def test_mirrors_locales_at_staging(self):
        stage_images.stage(self.store, self.out, ["de-DE"])
        self.assertEqual("Fixture", (self.out / "ios" / "metadata" / "de-DE" / "name.txt").read_text())
        self.assertTrue((self.out / "ios" / "screenshots" / "de-DE" / "ios-6.5_1-screen1.png").is_file())
        self.assertTrue((self.out / "android" / "metadata" / "de-DE" / "title.txt").is_file())
        self.assertTrue((self.out / "android" / "metadata" / "de-DE" / "images" / "icon.png").is_file())

    def test_refuses_committed_mirrored_locale(self):
        (self.store / "android" / "metadata" / "de-DE").mkdir()
        with self.assertRaisesRegex(stage_images.StagingError, "de-DE is mirrored"):
            stage_images.stage(self.store, self.out, ["de-DE"])

    def test_refuses_badly_named_image(self):
        (self.store / "assets" / "export" / "ios-6.9" / "shot.png").write_bytes(b"")
        with self.assertRaisesRegex(stage_images.StagingError, r"<n>-<screen>\.png"):
            stage_images.stage(self.store, self.out, [])

    def test_refuses_missing_text_file(self):
        (self.store / "ios" / "metadata" / "en-US" / "name.txt").unlink()
        with self.assertRaisesRegex(stage_images.StagingError, "missing"):
            stage_images.stage(self.store, self.out, [])


if __name__ == "__main__":
    unittest.main()
