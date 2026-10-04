"""Current Unity tables + radial encounter approximation. Never reads PlayerPrefs.

Purchases use compressed prices and actual cross-node/skill/stage prerequisites.
Movement, projectile interception and pickup efficiency remain approximations.
The default profile assumes successful kiting (no contact); stationary is stress-only.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path
import struct

from simulate import Progress, stats
from simulate_progression import FrontierRun, load_config

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/Resources/ProjectDM/Data"


def f32(value):
    return struct.unpack("f", struct.pack("f", value))[0]


def load_current():
    config = load_config(Path(__file__).with_name("progression_v2.json"))
    balance = json.loads((DATA / "PrototypeBalance.json").read_text(encoding="utf-8"))
    growth = json.loads((DATA / "PrototypeGrowth.json").read_text(encoding="utf-8"))
    skills = json.loads((DATA / "PrototypeSkills.json").read_text(encoding="utf-8"))
    # Match JsonUtility's float fields, important at ceil() price boundaries.
    for row in growth["rows"]:
        for key in ("costGrowth", "step", "costRampRanks", "damageRatio"):
            if key in row:
                row[key] = f32(row[key])
    config["runtime_balance"] = balance
    for monster in balance["monsters"]:
        if monster.get("currencyChances") is not None:
            monster["currencyChances"] = [f32(chance) for chance in monster["currencyChances"]]
    config["growth_rows"] = {row["id"]: row for row in growth["rows"]}
    config["nodes"] = growth["nodes"]
    config["node_index"] = {node["id"]: node for node in growth["nodes"]}
    config["skill_rows"] = {row["id"]: row for row in skills["rows"]}
    config["boss_elapsed_seconds"] = balance["bossElapsedSeconds"]
    config["first_damage_bonus"] = balance["baseDamage"] * balance["firstDamageBonus"]
    config["late_price_scale"] = balance["latePriceScale"]
    config["player"] = {"damage": balance["baseDamage"], "attack_interval": balance["attackInterval"],
                        "projectile_speed": balance["projectileSpeed"], "projectile_lifetime": balance["projectileLifetime"],
                        "run_seconds": balance["initialSeconds"]}
    config["time_reward"] = {"base_chance": balance["timeRewardChance"], "seconds": balance["timeRewardSeconds"]}
    config["simulation"].update({"xp_first": balance["xpFirst"], "xp_growth": balance["xpGrowth"],
        "initial_radius": [balance["initialSpawnMin"], balance["initialSpawnMax"]],
        "spawn_radius": [balance["refillSpawnMin"], balance["refillSpawnMax"]],
        "run_damage_step": config["skill_rows"]["damage"]["step"],
        "run_speed_step": config["skill_rows"]["speed"]["step"],
        "run_proc_step": config["skill_rows"]["proc"]["step"],
        "max_run_proc_rank": config["skill_rows"]["proc"]["maxRank"]})
    config["monsters"] = [{"id": m["id"], "hp": m["hp"], "speed": m["speed"], "xp": m["xp"],
        "coins": m["coins"], "bonus_chance": m["bonusChance"], "bonus_coins": m["bonusCoins"],
        "next_unlock_kills": m["nextUnlockKills"], "contact_damage": m["contactDamage"]} for m in balance["monsters"]]
    config["stages"] = [{"initial": s["initial"], "target": s["target"], "cap": s["cap"],
        "refill_seconds": s["refillSeconds"], "batch": s["batch"], "weights": s["weights"]} for s in balance["stages"]]
    config["effects"] = {key: {"chance": row["chance"], "damage_ratio": row["damageRatio"],
        **{prop: row[prop] for prop in ("targets", "range", "radius", "delay") if prop in row}}
        for key, row in config["skill_rows"].items() if row.get("unlock")}
    config["chest"] = {"chance": balance["chestChance"], "coins": balance["chestCoins"], "interaction_seconds": .8}
    config["upgrades"] = {key: {"base_cost": row["baseCost"], "cost_growth": row["costGrowth"],
        "max_rank": row["maxRank"], "min_stage": row["minStage"], "step": row.get("step", 0),
        "requires": row.get("requires"), "damage_ratio": row.get("damageRatio", 0)} for key, row in config["growth_rows"].items()}
    config["upgrades"]["damage"]["step"] *= balance["baseDamage"]
    config["upgrades"]["population"]["step"] = int(config["upgrades"]["population"]["step"])
    for policy in config["policies"].values():
        for key in ("chainChance", "splashChance", "meteorChance"):
            policy[key] = policy["proc"]
        for key in CURRENCY_GROWTH_IDS:
            policy[key] = policy["yield"]
        policy["experienceGradeChance"] = policy["xp"]
    return config


def resolve_grade(config, base_grade, stage, chance, sample):
    grades = config["runtime_balance"].get("lootGrades")
    if grades is None:
        return 1
    ceiling = max(row["grade"] for row in grades if row["unlockStage"] <= stage)
    grade = max(1, min(base_grade, ceiling))
    return grade + int(grade < ceiling and sample < min(1, max(0, chance)))


CURRENCY_GROWTH_IDS = ("silverDropChance", "goldDropChance", "emeraldDropChance", "amethystDropChance")


def currency_probabilities(config, kind, stage, bonuses):
    balance = config["runtime_balance"]
    grades = balance.get("lootGrades")
    count = len(grades) if grades is not None else 1
    ceiling = max(row["grade"] for row in grades if row["unlockStage"] <= stage) if grades else 1
    monster = balance["monsters"][kind]
    chances = [0.] * count
    if monster.get("currencyChances") is None:
        chances[max(1, min(monster.get("currencyGrade", 1), ceiling)) - 1] = 1.
    else:
        total = sum(monster["currencyChances"][:ceiling])
        if total > 0:
            chances = [value / total if index < ceiling else 0. for index, value in enumerate(monster["currencyChances"])]
        else: chances[0] = 1.
    for index in range(min(count, ceiling) - 1, 0, -1):
        requested = min(1., max(0., bonuses[index] if index < len(bonuses) else 0.))
        available = sum(chances[:index])
        transfer = min(requested, available)
        if transfer <= 0: continue
        for lower in range(index): chances[lower] *= (available - transfer) / available
        chances[index] += transfer
    return chances


def resolve_currency_grade(config, kind, stage, bonuses, sample):
    cumulative, last = 0., 1
    for index, chance in enumerate(currency_probabilities(config, kind, stage, bonuses)):
        if chance <= 0: continue
        last = index + 1
        cumulative += chance
        if sample < cumulative: return last
    return last


def currency_bonuses(config, ranks):
    return [0.] + [ranks.get(key, 0) * config["upgrades"].get(key, {}).get("step", 0) for key in CURRENCY_GROWTH_IDS]


def grade_base(config, kind, grade, currency):
    balance = config["runtime_balance"]
    if balance.get("lootGrades") is None:
        return balance["monsters"][kind]["coins" if currency else "xp"]
    return balance["lootGrades"][grade - 1]["currencyAmount" if currency else "experienceAmount"]


def grade_reward(config, kind, grade, currency, quantity, bonus_coin, rounding_sample):
    amount = grade_base(config, kind, grade, currency)
    if currency and bonus_coin: amount += config["runtime_balance"]["monsters"][kind]["bonusCoins"]
    amount *= 1 + max(0, quantity)
    if not currency: return min(3.4028234663852886e38, amount)
    raw = min(2147483647, max(0, amount))
    whole = math.floor(raw)
    return whole + int(whole < 2147483647 and rounding_sample < raw - whole)


def legacy_cost(row, rank, scale):
    return min(2147483647, max(1, math.ceil(row["baseCost"] * row["costGrowth"] ** rank *
        (1 + (scale - 1) * (1 - math.exp(-rank / 8))))))


def current_price(config, key, rank):
    row = config["growth_rows"][key]
    scale = row.get("legacyMaxRank", row["maxRank"]) / row["maxRank"]
    if scale == 1 or rank <= 0:
        return legacy_cost(row, rank, config["late_price_scale"])
    start, end = rank * scale, (rank + 1) * scale
    amount = sum(legacy_cost(row, old, config["late_price_scale"]) *
        (min(end, old + 1) - max(start, old)) for old in range(math.floor(start), math.ceil(end)))
    if row.get("costRampRanks", 0) > 0:
        amount *= 1 - math.exp(-rank / row["costRampRanks"])
    return min(2147483647, max(1, math.ceil(amount - 1e-8)))


def node_block(config, progress, node):
    key = node["growthId"]
    row = config["growth_rows"][key]
    rank = progress.ranks.get(key, 0)
    if rank < node["startRank"] or rank >= node["startRank"] + node["ranks"] or rank >= row["maxRank"]:
        return "rank"
    if progress.stage < row["minStage"] or (row.get("requires") and not progress.ranks.get(row["requires"], 0)):
        return "stage/skill"
    if node.get("parentRanks", 0) > 0:
        parent = config["node_index"][node["parent"]]
        parent_rank = min(parent["ranks"], max(0, progress.ranks.get(parent["growthId"], 0) - parent["startRank"]))
        if parent_rank < node["parentRanks"]:
            return "parent"
    return None


def benefit(config, progress, key):
    ranks = progress.ranks
    row = config["upgrades"][key]
    rank = ranks.get(key, 0)
    if key == "damage":
        bonus = config["first_damage_bonus"]
        return (row["step"] + (bonus if not rank else 0)) / (config["player"]["damage"] + rank * row["step"] + (bonus if rank else 0))
    if key in ("speed", "yield", "xp", "respawn"):
        return row["step"] / (1 + rank * row["step"])
    if key in CURRENCY_GROWTH_IDS:
        bonuses = currency_bonuses(config, ranks)
        upgraded = list(bonuses)
        upgraded[CURRENCY_GROWTH_IDS.index(key) + 1] += row["step"]
        base = gain = 0
        for kind, weight in enumerate(config["stages"][progress.stage]["weights"]):
            if not weight: continue
            amounts = [grade_base(config, kind, grade + 1, True) for grade in range(len(bonuses))]
            old = sum(p * value for p, value in zip(currency_probabilities(config, kind, progress.stage, bonuses), amounts))
            new = sum(p * value for p, value in zip(currency_probabilities(config, kind, progress.stage, upgraded), amounts))
            base += weight * old
            gain += weight * (new - old)
        return gain / base if base else 0
    if key == "experienceGradeChance":
        currency = False
        weights = config["stages"][progress.stage]["weights"]
        base = gain = 0
        for kind, weight in enumerate(weights):
            if not weight: continue
            monster = config["runtime_balance"]["monsters"][kind]
            grade = monster.get("currencyGrade" if currency else "experienceGrade", 1)
            promoted = resolve_grade(config, grade, progress.stage, 1, 0)
            value = grade_base(config, kind, grade, currency)
            delta = grade_base(config, kind, promoted, currency) - value
            base += weight * (value + rank * row["step"] * delta)
            gain += weight * row["step"] * delta
        return gain / base if base else 0
    if key == "time":
        return row["step"] / (config["player"]["run_seconds"] + rank * row["step"])
    if key == "extension":
        return 4 * row["step"] / (config["time_reward"]["base_chance"] + rank * row["step"])
    if key == "population":
        return .6 * row["step"] / (config["stages"][progress.stage]["target"] + rank * row["step"])
    if key == "multishot":
        return row["damage_ratio"] / (1 + rank * row["damage_ratio"])
    skill = key.removesuffix("Chance")
    effect = config["effects"][skill]
    targets = {"chain": 2, "splash": 3, "meteor": 4}[skill]
    return .75 * (row["step"] if key.endswith("Chance") else effect["chance"]) * effect["damage_ratio"] * targets


def shop_current(config, progress, policy):
    purchases = []
    while True:
        choices = []
        for node in config["nodes"]:
            if node_block(config, progress, node):
                continue
            key = node["growthId"]
            amount = current_price(config, key, progress.ranks.get(key, 0))
            if amount <= progress.wallet:
                choices.append((benefit(config, progress, key) * config["policies"][policy][key] / amount, key, node["id"], amount))
        if not choices:
            return purchases
        _, key, node_id, amount = max(choices)
        progress.wallet -= amount
        progress.ranks[key] = progress.ranks.get(key, 0) + 1
        purchases.append({"upgrade": key, "node": node_id, "rank": progress.ranks[key], "cost": amount})


class CurrentRun(FrontierRun):
    def __init__(self, config, progress, seed, policy="balanced", profile=None):
        super().__init__(config, progress, seed, policy, profile)
        self.critical_hits = 0
        self.player_health = config["runtime_balance"]["playerHealth"]
        self.player_defeated = False
        self.next_contact = 0

    def monster_rewards(self, enemy):
        kind = enemy.kind
        monster = self.config["runtime_balance"]["monsters"][kind]
        def grade(currency):
            if currency:
                return resolve_currency_grade(self.config, kind, self.progress.stage, currency_bonuses(self.config, self.progress.ranks), self.rng.random())
            key = "experienceGradeChance"
            return resolve_grade(self.config, monster.get("currencyGrade" if currency else "experienceGrade", 1),
                self.progress.stage, self.progress.ranks.get(key, 0) * self.config["upgrades"].get(key, {}).get("step", 0), self.rng.random())
        xp_grade = grade(False)
        coin_grade = grade(True)
        xp = grade_reward(self.config, kind, xp_grade, False, self.progress.ranks.get("xp", 0) * self.config["upgrades"]["xp"]["step"], False, 0)
        coins = grade_reward(self.config, kind, coin_grade, True, self.progress.ranks.get("yield", 0) * self.config["upgrades"]["yield"]["step"],
                             self.rng.random() < monster["bonusChance"], self.rng.random())
        return coins, xp

    def attack_speed(self):
        return (1 + self.progress.ranks.get("speed", 0) * self.config["upgrades"]["speed"]["step"]) * (1 + self.run_speed * self.options["run_speed_step"])

    def chance(self, key):
        return min(1, self.config["effects"][key]["chance"] + self.progress.ranks.get(key + "Chance", 0) *
            self.config["upgrades"][key + "Chance"]["step"] + self.run_proc * self.options["run_proc_step"])

    def direct_hit_damage(self, damage):
        if self.rng.random() < self.config["runtime_balance"]["criticalChance"]:
            self.critical_hits += 1
            return damage * self.config["runtime_balance"]["criticalMultiplier"]
        return damage

    def level_up(self):
        self.level += 1
        self.threshold = math.ceil(self.options["xp_first"] * self.options["xp_growth"] ** (self.level - 1))
        ranks = {"damage": self.run_damage, "speed": self.run_speed, "proc": self.run_proc,
                 **{key: int(key in self.owned) for key in self.config["effects"]}}
        candidates = [key for key, row in self.config["skill_rows"].items() if ranks[key] < row["maxRank"]
            and (not row.get("unlock") or self.progress.ranks.get(row["unlock"], 0) > 0)
            and (key != "proc" or self.owned)]
        acquisitions = [key for key in candidates if key in self.config["effects"] and key not in self.owned]
        offered = []
        if acquisitions:
            offered.append(self.rng.choice(acquisitions)); candidates.remove(offered[0])
        offered += self.rng.sample(candidates, min(3 - len(offered), len(candidates)))
        # A reproducible player strategy, not an optimizer: acquire an offered effect first,
        # then choose the largest rough DPS gain. All choices must actually be offered.
        scores = {"damage": self.options["run_damage_step"] / (1 + self.run_damage * self.options["run_damage_step"]),
                  "speed": self.options["run_speed_step"] / (1 + self.run_speed * self.options["run_speed_step"]),
                  "proc": self.options["run_proc_step"] * sum(self.config["effects"][key]["damage_ratio"] *
                      {"chain": 2, "splash": 3, "meteor": 4}[key] for key in self.owned)}
        key = max(offered, key=lambda key: 10 if key in acquisitions else scores[key]) if offered else None
        if key in acquisitions: self.owned.append(key)
        elif key == "damage": self.run_damage += 1
        elif key == "speed": self.run_speed += 1
        elif key == "proc": self.run_proc += 1

    def extra_event(self, kind, payload):
        if kind != "contact":
            return
        balance = self.config["runtime_balance"]
        if self.time >= self.next_contact:
            damage = max((self.config["monsters"][enemy.kind]["contact_damage"] for enemy in self.enemies.values()
                          if enemy.radius <= balance["playerContactRadius"]), default=0)
            if damage > 0:
                self.player_health = max(0, self.player_health - damage)
                self.next_contact = self.time + balance["playerInvulnerability"]
                if self.player_health <= 0:
                    self.player_defeated = True
                    self.duration = self.time
                    return
        self.schedule(self.time + .05, "contact", None)

    def play(self):
        if self.options.get("stationary_contact"):
            self.schedule(.05, "contact", None)
        row = super().play()
        row.update({"critical_hits": self.critical_hits, "player_defeated": self.player_defeated, "player_health": self.player_health})
        return row


def campaign(config, seed, policy="balanced", profile=None):
    progress = Progress()
    wall = active = 0
    rows = []
    overhead = config["overhead"]
    while wall < config["horizon_hours"] * 3600:
        row = CurrentRun(config, progress, seed * 100003 + len(rows), policy, profile).play()
        choices = row["level_ups"] * overhead["level_choice_seconds"]
        active += row["seconds"]
        if row["boss_arrived"]:
            wall += row["seconds"] + choices
            row.update({"wall_seconds": wall, "purchases": [], "wallet": progress.wallet})
            rows.append(row)
            return {"arrived": True, "arrival_seconds": wall, "active_seconds": active,
                    "runs": len(rows), "records": rows, "ranks": progress.ranks}
        purchases = shop_current(config, progress, policy)
        wall += row["seconds"] + choices + overhead["results_seconds"] + overhead["restart_seconds"] + overhead["shop_base_seconds"] + len(purchases) * overhead["purchase_seconds"]
        row.update({"wall_seconds": wall, "purchases": purchases, "wallet": progress.wallet})
        rows.append(row)
    return {"arrived": False, "arrival_seconds": None, "active_seconds": active,
            "runs": len(rows), "records": rows, "ranks": progress.ranks}


def summarize(rows, choice_seconds=2.5):
    reached = [row for row in rows if row["arrived"]]
    return {"samples": len(rows), "arrival_fraction": len(reached) / len(rows),
        "wall_hours_conditional": stats([row["arrival_seconds"] / 3600 for row in reached]) if reached else None,
        "active_hours_conditional": stats([row["active_seconds"] / 3600 for row in reached]) if reached else None,
        "runs_conditional": stats([row["runs"] for row in reached]) if reached else None,
        "within_4_to_5_hours_fraction": sum(4 * 3600 <= row["arrival_seconds"] <= 5 * 3600 for row in reached) / len(rows),
        "choice_seconds_sensitivity_same_campaigns": {str(seconds): stats([
            (row["arrival_seconds"] + (seconds - choice_seconds) * sum(run["level_ups"] for run in row["records"])) / 3600
            for row in reached]) if reached else None for seconds in (.5, 1, 2.5, 5)},
        "defeated_runs": sum(r["player_defeated"] for row in rows for r in row["records"]),
        "example_seed_0": rows[0]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--samples", type=int, default=20)
    parser.add_argument("--policy", choices=["all", "balanced", "combat", "field"], default="all")
    parser.add_argument("--profile", choices=["default", "low_collection", "low_accuracy", "stationary"], default="default")
    parser.add_argument("--output", type=Path, default=ROOT / "Temp/Balance-current.json")
    args = parser.parse_args()
    if args.samples < 1: parser.error("samples must be positive")
    config = load_current()
    profiles = {"default": {}, "low_collection": {"collection_rate": .65}, "low_accuracy": {"accuracy": .7},
                "stationary": {"enemy_stop_radius": [0, 0], "stationary_contact": True}}
    output = {"model": "current-tables-radial-approximation", "profile": args.profile,
        "table_sha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.glob("Prototype*.json"))},
        "rules_sha256": hashlib.sha256((ROOT / "Assets/Scripts/Systems/Data/PrototypeProgression.cs").read_bytes()).hexdigest(),
        "grade_rules_sha256": hashlib.sha256((ROOT / "Assets/Scripts/Systems/Pickups/LootGradeRules.cs").read_bytes()).hexdigest(),
        "assumptions": {"simulation": config["simulation"] | profiles[args.profile], "overhead": config["overhead"],
            "notes": ["No Unity movement/physics replay", "Default assumes evasion, stationary profile includes health/contact",
                      "Only new saves; first boss arrival, not victory; no offline income"]},
        "costs": {key: [current_price(config, key, rank) for rank in range(row["maxRank"])] for key, row in config["growth_rows"].items()},
        "policies": {}}
    output["loot_cases"] = []
    for stage in range(5):
        for kind in range(stage + 1):
            monster = config["runtime_balance"]["monsters"][kind]
            for currency in (False, True):
                for chance in (0, .18, 1):
                    for sample in (0, .179, .18, .9):
                        bonuses = [0, chance, chance, chance, chance]
                        grade = resolve_currency_grade(config, kind, stage, bonuses, sample) if currency else resolve_grade(config, monster.get("experienceGrade", 1), stage, chance, sample)
                        output["loot_cases"].append({"kind": kind, "stage": stage, "currency": currency, "chance": chance, "sample": sample,
                            "grade": grade, "bonuses": bonuses, "quantity": .04, "bonus_coin": currency, "rounding_sample": .25,
                            "reward": grade_reward(config, kind, grade, currency, .04, currency, .25)})
    policies = config["policies"] if args.policy == "all" else [args.policy]
    for policy in policies:
        rows = [campaign(config, seed, policy, profiles[args.profile]) for seed in range(args.samples)]
        output["policies"][policy] = summarize(rows, config["overhead"]["level_choice_seconds"])
        summary = output["policies"][policy]
        print(policy, "arrival", summary["arrival_fraction"], "wall hours", summary["wall_hours_conditional"],
              "active hours", summary["active_hours_conditional"], "runs", summary["runs_conditional"], flush=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
