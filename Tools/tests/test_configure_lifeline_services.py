"""Pure plan helpers; never contact App Store Connect or apply provider changes."""
import importlib.util
from pathlib import Path
import sys
import unittest
from datetime import datetime, timezone

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
SPEC = importlib.util.spec_from_file_location('lifeline_config', Path(__file__).resolve().parents[1] / 'configure_lifeline_services.py')
CONFIG = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONFIG)


class ConfigurationPlanTests(unittest.TestCase):
    def test_first_opening_is_future_monday_utc(self):
        now = datetime(2026, 9, 12, 15, tzinfo=timezone.utc)
        self.assertEqual(CONFIG.next_monday(now), datetime(2026, 9, 14, tzinfo=timezone.utc))
        monday = datetime(2026, 9, 14, 1, tzinfo=timezone.utc)
        self.assertEqual(CONFIG.next_monday(monday), datetime(2026, 9, 21, tzinfo=timezone.utc))

    def test_only_exact_monday_midnight_is_valid(self):
        self.assertEqual(CONFIG.parse_start('2026-09-14T00:00:00Z').weekday(), 0)
        for value in ('2026-09-15T00:00:00Z', '2026-09-14T01:00:00Z', '2026-09-14T00:00:00+01:00'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                CONFIG.parse_start(value)

    def test_patch_only_includes_changed_attributes(self):
        actions = []
        current = {'id': 'fixture', 'attributes': {'name': 'Old', 'familySharable': True}}
        CONFIG.patch(actions, 'inAppPurchases', current, '/fixture', {'name': 'New'}, 'copy')
        self.assertEqual(actions[0]['data']['attributes'], {'name': 'New'})
        self.assertNotIn('familySharable', actions[0]['data']['attributes'])

    def test_matching_metadata_produces_no_patch(self):
        actions = []
        current = {'id': 'fixture', 'attributes': {'name': 'New'}}
        CONFIG.patch(actions, 'inAppPurchases', current, '/fixture', {'name': 'New'}, 'copy')
        self.assertEqual(actions, [])

    def test_created_id_resolves_only_symbolic_relation(self):
        value = CONFIG.relation('gameCenterLeaderboards', '$weekly')
        self.assertEqual(CONFIG.resolve(value, {'weekly': 'new-id'}), CONFIG.relation('gameCenterLeaderboards', 'new-id'))

    def test_localization_limits_and_existing_entitlement_id(self):
        self.assertLessEqual(len(CONFIG.APP_NAME), 30)
        self.assertLessEqual(len(CONFIG.SUBTITLE), 30)
        self.assertLessEqual(len(CONFIG.COLLECTION_NAME), 30)
        self.assertLessEqual(len(CONFIG.COLLECTION_DESCRIPTION), 55)
        self.assertEqual(CONFIG.PLUS_ID, 'com.flutterly.gravitile.plus')


if __name__ == '__main__':
    unittest.main()
