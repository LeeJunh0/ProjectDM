"""Rules for first boss arrival; deliberately no boss HP or boss combat."""
import copy
from pathlib import Path
import unittest

from simulate import Enemy, Progress
from simulate_progression import FrontierRun, load_config, price, shop_v2


class FrontierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = load_config(Path(__file__).with_name("progression_v2.json"))

    def model(self, ranks=None):
        return FrontierRun(copy.deepcopy(self.config), Progress(ranks=ranks or {}), 77)

    def target(self, uid=1, hp=1):
        return Enemy(uid, 0, hp, 1, 1, 0, 1, 0)

    def test_time_reward_is_available_without_unlock_or_skill(self):
        run = self.model()
        self.assertEqual(run.owned, [])
        self.assertEqual(run.progress.ranks, {})
        self.assertAlmostEqual(run.time_chance, .08)

    def test_time_added_to_deadline_not_elapsed_time(self):
        run = self.model()
        run.time = 5
        run.time_chance = 1
        run.enemies[1] = self.target()
        run.hurt(1, 1)
        self.assertEqual(run.duration, 41)
        self.assertEqual(run.time, 5)
        self.assertEqual(run.extensions, 1)

    def test_nonlethal_hit_does_not_replenish_time(self):
        run = self.model()
        run.time_chance = 1
        run.enemies[1] = self.target(hp=3)
        run.hurt(1, 1)
        self.assertEqual(run.duration, 40)
        self.assertEqual(run.extensions, 0)

    def test_effect_kill_replenishes_time_but_does_not_trigger_attack_effects(self):
        run = self.model()
        run.time_chance = 1
        run.enemies[1] = self.target()
        run.hurt(1, 1, "chain")
        self.assertEqual(run.duration, 41)
        self.assertEqual(sum(run.procs.values()), 0)

    def test_dead_enemy_cannot_extend_again(self):
        run = self.model()
        run.time_chance = 1
        run.enemies[1] = self.target()
        run.hurt(1, 1)
        run.hurt(1, 1)
        self.assertEqual(run.extensions, 1)

    def test_deadline_never_exceeds_boss_limit(self):
        run = self.model()
        run.duration = 179.6
        run.time_chance = 1
        run.enemies[1] = self.target()
        run.hurt(1, 1)
        self.assertEqual(run.duration, 180)
        self.assertAlmostEqual(run.extension_seconds, .4)

    def test_hitting_deadline_cap_early_is_not_early_boss_arrival(self):
        run = self.model()
        run.duration = 180
        row = run.play()
        self.assertTrue(row["boss_arrived"])
        self.assertEqual(row["seconds"], 180)
        self.assertGreater(run.time, 179)

    def test_expired_run_is_not_boss_arrival(self):
        run = self.model()
        run.time_chance = 0
        row = run.play()
        self.assertFalse(row["boss_arrived"])
        self.assertEqual(row["seconds"], 40)

    def test_population_upgrade_increases_target_and_initial_and_cap(self):
        run = self.model({"population": 4})
        original = self.config["stages"][0]
        changed = run.config["stages"][0]
        self.assertEqual(changed["target"], original["target"] + 4)
        self.assertEqual(changed["cap"], original["cap"] + 4)
        self.assertGreater(changed["initial"], original["initial"])

    def test_respawn_upgrade_changes_interval_not_elapsed_boss_limit(self):
        run = self.model({"respawn": 5})
        self.assertAlmostEqual(run.config["stages"][0]["refill_seconds"], .7 / 1.06)
        self.assertEqual(run.design["boss_elapsed_seconds"], 180)

    def test_xp_upgrade_applies_to_all_monster_rewards(self):
        run = self.model({"xp": 5})
        self.assertAlmostEqual(run.config["monsters"][0]["xp"], 1.06)
        self.assertAlmostEqual(run.config["monsters"][4]["xp"], 9 * 1.06)

    def test_first_damage_purchase_has_visible_breakpoint(self):
        run = self.model({"damage": 1})
        self.assertAlmostEqual(run.main_damage(), 1.5)

    def test_two_time_growth_systems_are_separate(self):
        run = self.model({"time": 5, "extension": 5})
        self.assertEqual(run.initial_seconds, 42)
        self.assertAlmostEqual(run.time_chance, .082)
        self.assertEqual(run.design["boss_elapsed_seconds"], 180)

    def test_first_prices_stay_unchanged_when_late_price_scale_changes(self):
        config = copy.deepcopy(self.config)
        config["late_price_scale"] = 50
        self.assertEqual(price(config, "damage", 0), 14)
        self.assertGreater(price(config, "damage", 1), price(self.config, "damage", 1))

    def test_shop_budget_and_rank_constraints(self):
        for policy in self.config["policies"]:
            progress = Progress(stage=4, wallet=1000)
            purchases = shop_v2(self.config, progress, policy)
            self.assertEqual(sum(p["cost"] for p in purchases) + progress.wallet, 1000)
            self.assertGreaterEqual(progress.wallet, 0)
            for key, rank in progress.ranks.items():
                self.assertLessEqual(rank, self.config["upgrades"][key]["max_rank"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
