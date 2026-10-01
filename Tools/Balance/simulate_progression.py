"""V2: time replenishment from run one; first boss ARRIVAL, not boss defeat.

Reuses the tested radial encounter approximation from v1. No Unity writes.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path

from simulate import Progress, Run, stats


def load_config(path):
    spec = json.loads(path.read_text(encoding="utf-8"))
    base_path = path.with_name(spec["base_config"])
    config = json.loads(base_path.read_text(encoding="utf-8"))
    config.update(spec)
    for monster, threshold in zip(config["monsters"], spec["monster_unlock_kills"]):
        monster["next_unlock_kills"] = threshold
    return config


def price(config, key, rank):
    row = config["upgrades"][key]
    # Initial purchase stays affordable; later ranks can be tuned without delaying run one.
    scale = 1 + (config["late_price_scale"] - 1) * (1 - math.exp(-rank / 8))
    return math.ceil(row["base_cost"] * row["cost_growth"] ** rank * scale - 1e-9)


class FrontierRun(Run):
    def __init__(self, config, progress, seed, policy="balanced", profile=None):
        self.design = config
        env = copy.deepcopy(config)
        ranks = progress.ranks
        if ranks.get("damage", 0):
            env["player"]["damage"] += config["first_damage_bonus"]
        extra_population = ranks.get("population", 0) * config["upgrades"]["population"]["step"]
        refill_multiplier = 1 + ranks.get("respawn", 0) * config["upgrades"]["respawn"]["step"]
        for stage in env["stages"]:
            original_target = stage["target"]
            stage["target"] += extra_population
            stage["initial"] = math.ceil(stage["initial"] * stage["target"] / original_target)
            stage["cap"] += extra_population
            stage["refill_seconds"] /= refill_multiplier
        multiplier = 1 + ranks.get("xp", 0) * config["upgrades"]["xp"]["step"]
        for monster in env["monsters"]:
            monster["xp"] *= multiplier
        super().__init__(env, progress, seed, policy, profile)
        self.initial_seconds = self.duration
        self.duration = min(self.duration, config["boss_elapsed_seconds"])
        self.time_chance = min(1, config["time_reward"]["base_chance"] + ranks.get("extension", 0) * config["upgrades"]["extension"]["step"])
        self.extensions = 0
        self.extension_seconds = 0

    def hurt(self, uid, damage, source="basic"):
        before = sum(self.kills)
        super().hurt(uid, damage, source)
        # Every actual death can replenish time, including effect kills; never on mere hits.
        if sum(self.kills) != before and self.rng.random() < self.time_chance:
            previous = self.duration
            self.duration = min(self.design["boss_elapsed_seconds"], self.duration + self.design["time_reward"]["seconds"])
            self.extensions += 1
            self.extension_seconds += self.duration - previous

    def play(self):
        row = super().play()
        row.update({"boss_arrived": self.duration >= self.design["boss_elapsed_seconds"] - 1e-8,
                    "initial_seconds": self.initial_seconds, "time_extensions": self.extensions,
                    "added_seconds": self.extension_seconds, "time_chance": self.time_chance})
        return row


def shop_v2(config, progress, policy):
    purchases = []
    while True:
        choices = []
        ranks = progress.ranks
        for key, row in config["upgrades"].items():
            rank = ranks.get(key, 0)
            if rank >= row["max_rank"] or progress.stage < row["min_stage"]:
                continue
            if row.get("requires") and not ranks.get(row["requires"], 0):
                continue
            amount = price(config, key, rank)
            if amount > progress.wallet:
                continue
            if key == "damage":
                previous = config["player"]["damage"] + rank * row["step"] + (config["first_damage_bonus"] if rank else 0)
                gain = (row["step"] + (config["first_damage_bonus"] if not rank else 0)) / previous
            elif key in ("speed", "yield", "xp", "respawn"):
                gain = row["step"] / (1 + rank * row["step"])
            elif key == "population":
                gain = .6 * row["step"] / (config["stages"][progress.stage]["target"] + rank * row["step"])
            elif key == "time":
                gain = row["step"] / (config["player"]["run_seconds"] + rank * row["step"])
            elif key == "extension":
                gain = 4 * row["step"] / (config["time_reward"]["base_chance"] + rank * row["step"])
            elif key == "multishot":
                gain = row["damage_ratio"] / (1 + rank * row["damage_ratio"])
            elif key == "proc":
                gain = row["step"] * (1.3 + 2 * ranks.get("splash", 0) + 7 * ranks.get("meteor", 0))
            else:
                effect = config["effects"][key]
                gain = .75 * effect["chance"] * effect["damage_ratio"] * {"chain": 2, "splash": 3, "meteor": 4}[key]
            choices.append((gain * config["policies"][policy][key] / amount, key, amount))
        if not choices:
            return purchases
        _, key, amount = max(choices)
        progress.wallet -= amount
        ranks[key] = ranks.get(key, 0) + 1
        purchases.append({"upgrade": key, "rank": ranks[key], "cost": amount})


def simulate_campaign(config, seed, policy="balanced", profile=None):
    progress = Progress()
    total_seconds = 0
    rows = []
    milestones = {}
    limit = config["horizon_hours"] * 3600
    while total_seconds < limit:
        row = FrontierRun(config, progress, seed * 100003 + len(rows), policy, profile).play()
        choices = row["level_ups"] * config["overhead"]["level_choice_seconds"]
        # The finish time stops at boss appearance, before that run's results/shop.
        arrival_time = total_seconds + row["seconds"] + choices
        if row["boss_arrived"]:
            row.update({"wall_seconds": arrival_time, "purchases": [], "wallet": progress.wallet})
            rows.append(row)
            return {"arrived": True, "arrival_seconds": arrival_time, "runs": len(rows), "milestones": milestones,
                    "records": rows, "ranks": progress.ranks}
        purchases = shop_v2(config, progress, policy)
        overhead = config["overhead"]
        elapsed = row["seconds"] + choices + overhead["results_seconds"] + overhead["restart_seconds"] + overhead["shop_base_seconds"] + len(purchases) * overhead["purchase_seconds"]
        total_seconds += elapsed
        row.update({"wall_seconds": total_seconds, "purchases": purchases, "wallet": progress.wallet})
        rows.append(row)
        for unlock in row["unlocks"]:
            milestones.setdefault(str(unlock["stage"]), {"run": len(rows), "seconds": total_seconds - elapsed + unlock["seconds"]})
    return {"arrived": False, "arrival_seconds": None, "runs": len(rows), "milestones": milestones,
            "records": rows, "ranks": progress.ranks}


def summarize_campaigns(data):
    arrived = [row for row in data if row["arrived"]]
    milestones = {}
    for stage in range(1, 5):
        times = [row["milestones"][str(stage)]["seconds"] / 60 for row in data if str(stage) in row["milestones"]]
        milestones[str(stage)] = {"fraction": len(times) / len(data), "minutes": stats(times) if times else None}
    return {"samples": len(data), "arrival_fraction": len(arrived) / len(data),
            "arrival_hours_conditional": stats([row["arrival_seconds"] / 3600 for row in arrived]) if arrived else None,
            "runs_conditional": stats([row["runs"] for row in arrived]) if arrived else None,
            "monster_milestones": milestones,
            "purchase_runs_fraction": stats([sum(bool(r["purchases"]) for r in row["records"][:-1]) / max(1, len(row["records"]) - 1) for row in data]),
            "example_seed_0": data[0]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=Path(__file__).with_name("progression_v2.json"))
    parser.add_argument("--output", type=Path, default=Path(__file__).with_name("progression_results_v2.json"))
    parser.add_argument("--samples", type=int, default=30)
    parser.add_argument("--policy", choices=["all", "balanced", "combat", "field"], default="all")
    parser.add_argument("--scale", type=float)
    parser.add_argument("--boss-seconds", type=float)
    args = parser.parse_args()
    if args.samples < 1 or (args.scale is not None and args.scale < 1):
        parser.error("positive samples and scale >= 1 required")
    if args.boss_seconds is not None and args.boss_seconds <= 0:
        parser.error("boss-seconds must be positive")
    config = load_config(args.config)
    if args.scale is not None:
        config["late_price_scale"] = args.scale
    if args.boss_seconds is not None:
        config["boss_elapsed_seconds"] = args.boss_seconds
    output = {"version": config["version"], "config_sha256": hashlib.sha256(args.config.read_bytes()).hexdigest(),
              "base_config_sha256": hashlib.sha256(args.config.with_name(config["base_config"]).read_bytes()).hexdigest(),
              "effective_price_scale": config["late_price_scale"], "boss_elapsed_seconds": config["boss_elapsed_seconds"],
              "horizon_hours": config["horizon_hours"], "policies": {}}
    policies = config["policies"] if args.policy == "all" else [args.policy]
    for policy in policies:
        rows = [simulate_campaign(config, seed, policy) for seed in range(args.samples)]
        output["policies"][policy] = summarize_campaigns(rows)
        result = output["policies"][policy]
        print(policy, "reached", result["arrival_fraction"], "hours", result["arrival_hours_conditional"], "runs", result["runs_conditional"], flush=True)
    args.output.write_text(json.dumps(output, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
