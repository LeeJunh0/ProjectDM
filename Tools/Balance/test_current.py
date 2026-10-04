"""Tests for current-table model; separate from native Unity gameplay QA."""
import copy
import unittest

from simulate import Enemy, Progress
from simulate_current import CurrentRun, campaign, current_price, load_current, node_block, shop_current, summarize, resolve_grade, grade_base, currency_probabilities, resolve_currency_grade


class CurrentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = load_current()

    def model(self, ranks=None, **profile):
        return CurrentRun(copy.deepcopy(self.config), Progress(ranks=ranks or {}), 77, profile=profile)

    def test_first_prices_and_monotonic_bundles(self):
        for key, row in self.config["growth_rows"].items():
            prices = [current_price(self.config, key, rank) for rank in range(row["maxRank"])]
            self.assertEqual(prices[0], row["baseCost"])
            self.assertEqual(prices, sorted(prices))
        self.assertGreater(current_price(self.config, "damage", 1), 14)

    def test_actual_cross_node_prerequisite_is_not_optional(self):
        node = next(node for node in self.config["nodes"] if node.get("parentRanks", 0) > 0)
        parent = self.config["node_index"][node["parent"]]
        progress = Progress(stage=4, ranks={key: row["maxRank"] for key, row in self.config["growth_rows"].items()})
        progress.ranks[node["growthId"]] = node["startRank"]
        progress.ranks[parent["growthId"]] = parent["startRank"]
        self.assertEqual(node_block(self.config, progress, node), "parent")
        progress.ranks[parent["growthId"]] += node["parentRanks"]
        self.assertIsNone(node_block(self.config, progress, node))

    def test_graph_can_fill_with_full_budget(self):
        progress = Progress(wallet=10**9, stage=4)
        purchases = shop_current(self.config, progress, "balanced")
        self.assertEqual(len(purchases), sum(row["maxRank"] for row in self.config["growth_rows"].values()))
        self.assertEqual(progress.wallet + sum(p["cost"] for p in purchases), 10**9)
        for key, row in self.config["growth_rows"].items():
            self.assertEqual(progress.ranks[key], row["maxRank"])

    def test_independent_permanent_skill_chances(self):
        run = self.model({"chainChance": 3})
        self.assertAlmostEqual(run.chance("chain"), .14)
        self.assertAlmostEqual(run.chance("splash"), .12)
        self.assertAlmostEqual(run.chance("meteor"), .04)

    def test_temporary_and_permanent_attack_speed_multiply(self):
        run = self.model({"speed": 5})
        run.run_speed = 4
        self.assertAlmostEqual(run.attack_speed(), 1.2 * 1.4)

    def test_first_damage_purchase_is_one_point_sixty_six(self):
        self.assertAlmostEqual(self.model({"damage": 1}).main_damage(), 1.66)

    def test_unlocked_skills_still_require_run_choice(self):
        run = self.model({"chain": 1, "splash": 1, "meteor": 1})
        self.assertEqual(run.owned, [])
        for _ in range(3): run.level_up()
        self.assertEqual(set(run.owned), {"chain", "splash", "meteor"})
        for _ in range(30): run.level_up()
        self.assertLessEqual(run.run_proc, 4)

    def test_crit_applies_to_direct_hit_not_effect_power(self):
        run = self.model(accuracy=1)
        run.config["runtime_balance"]["criticalChance"] = 1
        run.owned = ["chain"]
        run.config["effects"]["chain"]["chance"] = 1
        run.enemies = {1: Enemy(1, 0, 100, 1, 1, 0, 1, 0), 2: Enemy(2, 0, 100, 2, 2, 0, 1, 0)}
        run.basic_hit(1, 1)
        self.assertEqual(run.enemies[1].hp, 98)
        self.assertAlmostEqual(run.enemies[2].hp, 99.35)
        self.assertEqual(run.critical_hits, 1)

    def test_stationary_contact_uses_max_not_sum_and_immunity(self):
        run = self.model()
        run.enemies = {1: Enemy(1, 0, 100, .1, 0, 0, 1, 0), 2: Enemy(2, 4, 100, .1, 0, 0, 1, 0)}
        run.extra_event("contact", None)
        self.assertEqual(run.player_health, 94)
        run.time = .1; run.extra_event("contact", None)
        self.assertEqual(run.player_health, 94)
        run.time = .6; run.extra_event("contact", None)
        self.assertEqual(run.player_health, 88)

    def test_death_prevents_boss_arrival(self):
        run = self.model()
        run.duration = 180; run.player_health = 1
        run.time = 10
        run.enemies = {1: Enemy(1, 0, 100, .1, 0, 0, 1, 0)}
        run.extra_event("contact", None)
        self.assertTrue(run.player_defeated)
        self.assertEqual(run.duration, 10)

    def test_elapsed_cap_not_deadline_alone(self):
        run = self.model(collection_rate=0)
        run.duration = 40
        self.assertFalse(run.play()["boss_arrived"])

    def test_campaign_seed_reproducible(self):
        config = copy.deepcopy(self.config); config["horizon_hours"] = .05
        self.assertEqual(campaign(config, 11), campaign(config, 11))

    def test_menu_speed_changes_wall_time_not_active_time(self):
        row = {"arrived": True, "arrival_seconds": 15000, "active_seconds": 10000, "runs": 100,
               "records": [{"level_ups": 20, "player_defeated": False}]}
        summary = summarize([row])
        self.assertEqual(summary["choice_seconds_sensitivity_same_campaigns"]["0.5"]["p50"], round(14960 / 3600, 2))
        self.assertEqual(summary["active_hours_conditional"]["p50"], round(10000 / 3600, 2))

    def test_grade_baseline_preserves_each_monster_reward(self):
        for kind, monster in enumerate(self.config["runtime_balance"]["monsters"]):
            self.assertEqual(grade_base(self.config, kind, monster["currencyGrade"], True), monster["coins"])
            self.assertEqual(grade_base(self.config, kind, monster["experienceGrade"], False), monster["xp"])

    def test_grade_is_limited_to_one_step_and_unlocked_ceiling(self):
        for stage in range(5):
            for grade in range(1, stage + 2):
                self.assertEqual(resolve_grade(self.config, grade, stage, 1, 0), min(stage + 1, grade + 1))
                self.assertEqual(resolve_grade(self.config, grade, stage, 0, 0), grade)

    def test_currency_promotion_does_not_promote_experience(self):
        run = self.model({"amethystDropChance": 1})
        run.config["upgrades"]["amethystDropChance"]["step"] = 1
        run.config["runtime_balance"]["monsters"][0]["bonusChance"] = 0
        run.progress.stage = 4
        coins, xp = run.monster_rewards(Enemy(1, 0, 1, 1, 1, 0, 1, 0))
        self.assertEqual((coins, xp), (9, 1))

    def test_latest_monster_cannot_promote_to_unreleased_grade(self):
        run = self.model({"amethystDropChance": 9, "silverDropChance": 1, "experienceGradeChance": 9})
        run.config["upgrades"]["amethystDropChance"]["step"] = 1
        run.config["upgrades"]["silverDropChance"]["step"] = 1
        run.config["upgrades"]["experienceGradeChance"]["step"] = 1
        run.config["runtime_balance"]["monsters"][1]["bonusChance"] = 0
        run.progress.stage = 1
        self.assertEqual(run.monster_rewards(Enemy(1, 1, 1, 1, 1, 0, 1, 0)), (2, 2))

    def test_currency_distribution_sum_and_locked_outcomes(self):
        for stage in range(5):
            for kind in range(stage + 1):
                for bonuses in ([0] * 5, [0, .18, .09, .054, .027], [0, 1, 1, 1, 1]):
                    p = currency_probabilities(self.config, kind, stage, bonuses)
                    self.assertAlmostEqual(sum(p), 1)
                    self.assertTrue(all(value >= 0 for value in p))
                    self.assertTrue(all(value == 0 for value in p[stage + 1:]))

    def test_single_roll_can_skip_currency_grades(self):
        self.assertEqual(resolve_currency_grade(self.config, 0, 4, [0, 0, 0, 0, 1], .999), 5)

    def test_currency_growth_does_not_take_from_higher_currency(self):
        base = currency_probabilities(self.config, 0, 4, [0] * 5)
        silver = currency_probabilities(self.config, 0, 4, [0, .18, 0, 0, 0])
        self.assertAlmostEqual(silver[1] - base[1], .18)
        self.assertEqual(silver[2:], base[2:])


if __name__ == "__main__":
    unittest.main(verbosity=2)
