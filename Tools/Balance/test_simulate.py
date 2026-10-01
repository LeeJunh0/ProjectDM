"""Regression tests for the design calculator, not Unity gameplay tests."""
import copy
import json
from pathlib import Path
import unittest

from simulate import Enemy, Progress, Run, campaign, cost, shop


class BalanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = json.loads(Path(__file__).with_name("early_game_v1.json").read_text(encoding="utf-8"))

    def run_model(self, stage=0, ranks=None, **profile):
        return Run(copy.deepcopy(self.config), Progress(stage=stage, ranks=ranks or {}), 123,
                   profile={"accuracy": 1, **profile})

    def enemy(self, uid, kind=0, hp=1000, radius=1):
        return Enemy(uid, kind, hp, radius, radius, 0, 1, 0)

    def test_tables_have_valid_weights_and_population(self):
        for index, stage in enumerate(self.config["stages"]):
            self.assertEqual(sum(stage["weights"]), 100)
            self.assertTrue(0 < stage["initial"] <= stage["target"] <= stage["cap"])
            self.assertTrue(0 < stage["batch"] <= stage["cap"])
            self.assertGreater(stage["refill_seconds"], 0)
            self.assertTrue(all(weight == 0 for weight in stage["weights"][index + 1:]))

    def test_unlocked_mix_retains_old_monsters(self):
        for index, stage in enumerate(self.config["stages"]):
            self.assertTrue(all(weight > 0 for weight in stage["weights"][:index + 1]))

    def test_new_stage_unlocked_by_correct_species_not_global_kills(self):
        run = self.run_model()
        run.progress.kills[1] = 10000
        run.progress.kills[0] = 78
        run.enemies[1] = self.enemy(1, hp=1)
        run.hurt(1, 1)
        self.assertEqual(run.progress.stage, 0)
        run.progress.kills[1] = 0
        run.enemies[2] = self.enemy(2, hp=1)
        run.hurt(2, 1)
        self.assertEqual(run.progress.stage, 1)
        self.assertEqual(run.unlocks, [{"stage": 1, "seconds": 0.0}])

    def test_kills_persist_without_collecting_rewards(self):
        run = self.run_model(collection_rate=0)
        run.enemies[1] = self.enemy(1, hp=1)
        run.hurt(1, 1)
        self.assertEqual(run.progress.kills[0], 1)
        self.assertEqual(run.coins, 0)
        self.assertEqual(run.total_xp, 0)
        self.assertGreaterEqual(run.dropped_coins, 1)

    def test_dead_enemy_not_counted_twice(self):
        run = self.run_model()
        run.enemies[1] = self.enemy(1, hp=1)
        run.hurt(1, 10)
        run.hurt(1, 10)
        self.assertEqual(run.kills[0], 1)

    def test_each_hit_rolls_all_effects_even_on_killing_hit(self):
        run = self.run_model(stage=4)
        run.owned = ["chain", "splash", "meteor"]
        for effect in run.config["effects"].values():
            effect["chance"] = 1
            effect["damage_ratio"] = 0
        for uid in range(1, 4):
            run.enemies[uid] = self.enemy(uid, hp=1)
        for uid in range(1, 4):
            run.basic_hit(uid, 1)
        self.assertEqual(run.hits, 3)
        self.assertEqual(run.procs, {"chain": 3, "splash": 3, "meteor": 3})

    def test_effect_damage_does_not_recurse(self):
        run = self.run_model(stage=4)
        run.owned = ["chain", "splash", "meteor"]
        for effect in run.config["effects"].values():
            effect["chance"] = 1
        run.enemies[1] = self.enemy(1)
        run.hurt(1, 5, "chain")
        run.hurt(1, 5, "splash")
        run.hurt(1, 5, "meteor")
        self.assertEqual(sum(run.procs.values()), 0)
        self.assertEqual(run.hits, 0)

    def test_probability_distribution_matches_per_hit_rate(self):
        run = self.run_model()
        run.owned = ["chain"]
        run.enemies[1] = self.enemy(1, hp=1e9)
        for _ in range(10000):
            run.basic_hit(1, 1)
        self.assertTrue(1100 <= run.procs["chain"] <= 1300)
        self.assertEqual(run.hits, 10000)

    def test_multishot_increases_independent_hit_attempts(self):
        single = self.run_model(stage=4)
        multi = self.run_model(stage=4, ranks={"multishot": 1})
        for run in (single, multi):
            for monster in run.config["monsters"]:
                monster["hp"] = 1e6
        a, b = single.play(), multi.play()
        self.assertEqual(b["fired"], 2 * a["fired"])
        self.assertEqual(b["basic_hits"], 2 * a["basic_hits"])

    def test_population_caps_and_locked_species(self):
        for stage in range(5):
            run = self.run_model(stage=stage, progression_enabled=False)
            row = run.play()
            self.assertLessEqual(row["peak_alive"], self.config["stages"][stage]["cap"])
            self.assertTrue(all(count == 0 for count in row["kills_by_kind"][stage + 1:]))
            self.assertGreaterEqual(row["mean_alive"], 0)

    def test_no_collection_means_no_coins_or_levels(self):
        row = self.run_model(collection_rate=0).play()
        self.assertGreater(row["kills"], 0)
        self.assertEqual(row["coins"], 0)
        self.assertEqual(row["level_ups"], 0)

    def test_time_upgrade_changes_duration_not_base_hp(self):
        run = self.run_model(ranks={"time": 2})
        self.assertEqual(run.duration, 56)
        run.spawn(1)
        self.assertEqual(next(iter(run.enemies.values())).hp, 3)

    def test_permanent_skill_unlock_not_automatic_run_acquisition(self):
        run = self.run_model(ranks={"chain": 1})
        self.assertEqual(run.owned, [])
        run.level_up()
        self.assertEqual(run.owned, ["chain"])

    def test_shop_budget_rank_and_unlock_constraints(self):
        for policy in self.config["policies"]:
            progress = Progress(wallet=500, stage=0)
            purchases = shop(self.config, progress, policy)
            self.assertGreaterEqual(progress.wallet, 0)
            self.assertEqual(sum(row["cost"] for row in purchases) + progress.wallet, 500)
            self.assertNotIn("multishot", progress.ranks)
            self.assertNotIn("chain", progress.ranks)
            for key, rank in progress.ranks.items():
                self.assertLessEqual(rank, self.config["upgrades"][key]["max_rank"])

    def test_prices_are_rounded_up_and_non_decreasing(self):
        self.assertEqual([cost(self.config, "damage", rank) for rank in range(3)], [14, 26, 48])
        for key, row in self.config["upgrades"].items():
            prices = [cost(self.config, key, rank) for rank in range(row["max_rank"])]
            self.assertEqual(prices, sorted(prices))

    def test_campaign_is_reproducible_and_uses_fresh_saves(self):
        first = campaign(self.config, 7, "balanced", runs=3)
        second = campaign(self.config, 7, "balanced", runs=3)
        self.assertEqual(first, second)
        self.assertEqual(first["runs"][0]["starting_ranks"], {})
        self.assertGreater(sum(first["final_stacks"]), first["runs"][0]["kills"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
