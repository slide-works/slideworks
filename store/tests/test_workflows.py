import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class WorkflowAndGemfileTests(unittest.TestCase):
    def test_submit_has_a_guard_job_that_fails_off_main(self):
        text = (ROOT / ".github" / "workflows" / "store-submit.yml").read_text()
        self.assertIn("Run Store submit from main", text)
        self.assertIn("if: ${{ github.ref != 'refs/heads/main' }}", text)
        self.assertEqual(2, text.count("needs: guard"))
        self.assertNotIn("github.ref == 'refs/heads/main'", text)

    def test_listing_emits_one_notice_on_a_fork(self):
        text = (ROOT / ".github" / "workflows" / "store-listing.yml").read_text()
        self.assertEqual(1, text.count("::notice::"))
        self.assertEqual(2, text.count("if: ${{ !github.event.pull_request.head.repo.fork }}"))

    def test_fastlane_floor_knows_the_new_iphone_sizes(self):
        match = re.search(r'gem "fastlane", "~> (\d+)\.(\d+)"', (ROOT / "store" / "Gemfile").read_text())
        self.assertIsNotNone(match)
        self.assertGreaterEqual((int(match[1]), int(match[2])), (2, 242))

    def test_listing_ios_job_exports_the_build_jobs_version(self):
        text = (ROOT / ".github" / "workflows" / "mobile-release.yml").read_text()
        job = text[text.index("  listing-ios:"):text.index("  listing-android:")]
        self.assertIn("APP_VERSION_PATCH: ${{ github.run_number }}", job)
        self.assertIn('APP_VERSION=$(python3 store/app_version.py --print)', job)


if __name__ == "__main__":
    unittest.main()
