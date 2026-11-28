import json
import unittest

import listing_support

# The keys fastlane 2.242 (spaceship AgeRatingDeclaration) accepts: a rating level each, and a boolean each.
RATINGS = {"alcoholTobaccoOrDrugUseOrReferences", "contests", "gamblingSimulated", "gunsOrOtherWeapons",
           "horrorOrFearThemes", "matureOrSuggestiveThemes", "medicalOrTreatmentInformation",
           "profanityOrCrudeHumor", "sexualContentGraphicAndNudity", "sexualContentOrNudity",
           "violenceCartoonOrFantasy", "violenceRealisticProlongedGraphicOrSadistic", "violenceRealistic"}
BOOLEANS = {"advertising", "ageAssurance", "gambling", "healthOrWellnessTopics", "lootBox", "messagingAndChat",
            "parentalControls", "socialMedia", "socialMediaAgeRestricted", "unrestrictedWebAccess",
            "userGeneratedContent"}


class AgeRatingTests(unittest.TestCase):
    def setUp(self):
        self.rating = json.loads((listing_support.STORE / "ios" / "age_rating.json").read_text())

    def test_declares_every_key(self):
        self.assertEqual(RATINGS | BOOLEANS, set(self.rating))

    def test_every_answer_is_none_or_false(self):
        for key in RATINGS:
            self.assertEqual("NONE", self.rating[key], key)
        for key in BOOLEANS:
            self.assertIs(False, self.rating[key], key)
