"""Standalone proposed-balance model. Does not load or modify Unity save data.

This is a radial encounter approximation, not a Unity physics/playtest replay.
Only standard-library dependencies. All randomness is seeded for reproduction.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import heapq
import hashlib
import json
import math
from pathlib import Path
import random
import statistics


@dataclass
class Progress:
    ranks: dict[str, int] = field(default_factory=dict)
    kills: list[int] = field(default_factory=lambda: [0] * 5)
    wallet: int = 0
    stage: int = 0


@dataclass(slots=True)
class Enemy:
    uid: int
    kind: int
    hp: float
    radius: float
    stop: float
    speed: float
    ux: float
    uy: float

    def position(self):
        return self.ux * self.radius, self.uy * self.radius


def cost(config, key, rank):
    row = config["upgrades"][key]
    return math.ceil(row["base_cost"] * row["cost_growth"] ** rank - 1e-9)


def shop(config, progress, policy):
    """Transparent weighted marginal-benefit heuristic; NOT optimal purchasing."""
    bought = []
    while True:
        choices = []
        ranks = progress.ranks
        for key, row in config["upgrades"].items():
            rank = ranks.get(key, 0)
            if rank >= row["max_rank"] or progress.stage < row["min_stage"]:
                continue
            if row.get("requires") and not ranks.get(row["requires"], 0):
                continue
            price = cost(config, key, rank)
            if price > progress.wallet:
                continue
            if key == "damage":
                gain = row["step"] / (config["player"]["damage"] + rank * row["step"])
            elif key == "speed":
                gain = row["step"] / (1 + rank * row["step"])
            elif key == "time":
                gain = row["step"] / (config["player"]["run_seconds"] + rank * row["step"])
            elif key == "yield":
                gain = row["step"] / (1 + rank * row["step"])
            elif key == "multishot":
                gain = row["damage_ratio"] / (1 + rank * row["damage_ratio"])
            elif key == "proc":
                gain = row["step"] * (1.3 + 2 * ranks.get("splash", 0) + 7 * ranks.get("meteor", 0))
            else:
                # Discount for run-level acquisition delay; AoE target counts are estimates.
                effect = config["effects"][key]
                expected_targets = {"chain": 2, "splash": 3, "meteor": 4}[key]
                gain = 0.75 * effect["chance"] * effect["damage_ratio"] * expected_targets
            choices.append((gain * config["policies"][policy][key] / price, key, price))
        if not choices:
            break
        _, key, price = max(choices)
        progress.wallet -= price
        ranks[key] = ranks.get(key, 0) + 1
        bought.append({"upgrade": key, "rank": ranks[key], "cost": price})
    return bought


class Run:
    def __init__(self, config, progress, seed, policy="balanced", profile=None):
        self.config, self.progress = config, progress
        self.rng = random.Random(seed)
        self.policy = policy
        self.options = config["simulation"] | (profile or {})
        self.time = 0.0
        self.duration = config["player"]["run_seconds"] + progress.ranks.get("time", 0) * config["upgrades"]["time"]["step"]
        self.enemies = {}
        self.events = []
        self.serial = self.uid = 0
        self.next_attack = self.next_refill = 0.0
        self.level = 1
        self.xp = self.total_xp = self.coins = self.dropped_coins = 0
        self.threshold = self.options["xp_first"]
        self.run_damage = self.run_speed = self.run_proc = 0
        self.owned = []
        self.kills = [0] * 5
        self.procs = {key: 0 for key in config["effects"]}
        self.hits = self.fired = self.spawned = self.wasted_damage = 0
        self.damage_total = self.occupancy = self.empty_ticks = 0
        self.peak = 0
        self.unlocks = []
        self.first_kill = None
        self.effect_damage = {key: 0.0 for key in config["effects"]}

    def schedule(self, when, kind, payload):
        self.serial += 1
        heapq.heappush(self.events, (when, self.serial, kind, payload))

    def spawn(self, amount, initial=False):
        stage = self.config["stages"][self.progress.stage]
        amount = min(amount, stage["cap"] - len(self.enemies))
        radii = self.options["initial_radius"] if initial else self.options["spawn_radius"]
        for _ in range(max(0, amount)):
            kind = self.rng.choices(range(5), weights=stage["weights"])[0]
            monster = self.config["monsters"][kind]
            theta = self.rng.uniform(0, 2 * math.pi)
            self.uid += 1
            self.enemies[self.uid] = Enemy(self.uid, kind, monster["hp"], self.rng.uniform(*radii),
                self.rng.uniform(*self.options["enemy_stop_radius"]), monster["speed"], math.cos(theta), math.sin(theta))
            self.spawned += 1

    def main_damage(self):
        base = self.config["player"]["damage"] + self.progress.ranks.get("damage", 0) * self.config["upgrades"]["damage"]["step"]
        return base * (1 + self.run_damage * self.options["run_damage_step"])

    def chance(self, key):
        return min(1, self.config["effects"][key]["chance"] + self.progress.ranks.get("proc", 0) * self.config["upgrades"]["proc"]["step"] + self.run_proc * self.options["run_proc_step"])

    def hurt(self, uid, damage, source="basic"):
        enemy = self.enemies.get(uid)
        if enemy is None:
            return
        self.wasted_damage += max(0, damage - enemy.hp)
        self.damage_total += damage
        if source != "basic":
            self.effect_damage[source] += min(damage, enemy.hp)
        enemy.hp -= damage
        if enemy.hp > 1e-8:
            return
        del self.enemies[uid]
        self.kills[enemy.kind] += 1
        self.progress.kills[enemy.kind] += 1
        if self.first_kill is None:
            self.first_kill = self.time
        while self.progress.stage < 4 and self.options.get("progression_enabled", True):
            current = self.progress.stage
            if self.progress.kills[current] < self.config["monsters"][current]["next_unlock_kills"]:
                break
            self.progress.stage += 1
            self.unlocks.append({"stage": self.progress.stage, "seconds": round(self.time, 2)})
        monster = self.config["monsters"][enemy.kind]
        coins = monster["coins"] + (monster["bonus_coins"] if self.rng.random() < monster["bonus_chance"] else 0)
        multiplier = 1 + self.progress.ranks.get("yield", 0) * self.config["upgrades"]["yield"]["step"]
        raw = coins * multiplier
        coins = math.floor(raw) + (self.rng.random() < raw % 1)
        self.dropped_coins += coins
        # Independent collection of coin and XP bundles; late drops can remain uncollected.
        delay = self.options["collection_delay"] + enemy.radius * self.options["collection_delay_per_distance"]
        if self.rng.random() < self.options["collection_rate"]:
            self.schedule(self.time + delay, "coins", coins)
        if self.rng.random() < self.options["collection_rate"]:
            self.schedule(self.time + delay, "xp", monster["xp"])
        chest = self.config.get("chest")
        if chest and self.rng.random() < chest["chance"]:
            raw = chest["coins"] * multiplier
            chest_coins = math.floor(raw) + (self.rng.random() < raw % 1)
            self.dropped_coins += chest_coins
            if self.rng.random() < self.options["collection_rate"]:
                self.schedule(self.time + delay + chest["interaction_seconds"], "coins", chest_coins)

    def nearby(self, origin, radius, exclude=()):
        ox, oy = origin
        result = []
        for enemy in self.enemies.values():
            if enemy.uid in exclude:
                continue
            x, y = enemy.position()
            distance = (x - ox) ** 2 + (y - oy) ** 2
            if distance <= radius ** 2:
                result.append((distance, enemy.uid))
        return sorted(result)

    def basic_hit(self, uid, damage):
        enemy = self.enemies.get(uid)
        if enemy is None or self.rng.random() >= self.options["accuracy"]:
            return
        self.hits += 1
        origin = enemy.position()
        # Roll all effects before damage so a killing hit still has every independent roll.
        triggered = [key for key in self.owned if self.rng.random() < self.chance(key)]
        self.hurt(uid, damage)
        for key in triggered:
            self.procs[key] += 1
            effect = self.config["effects"][key]
            power = self.main_damage() * effect["damage_ratio"]
            if key == "chain":
                previous, visited = origin, {uid}
                for _ in range(effect["targets"]):
                    candidates = self.nearby(previous, effect["range"], visited)
                    if not candidates:
                        break
                    target = candidates[0][1]
                    previous = self.enemies[target].position()
                    visited.add(target)
                    self.hurt(target, power, key)
            elif key == "splash":
                for _, target in self.nearby(origin, effect["radius"]):
                    self.hurt(target, power, key)
            else:
                self.schedule(self.time + effect["delay"], "meteor", (origin, power))

    def level_up(self):
        self.level += 1
        self.threshold = math.ceil(self.options["xp_first"] * self.options["xp_growth"] ** (self.level - 1) - 1e-9)
        available = [key for key in self.config["effects"] if self.progress.ranks.get(key, 0) and key not in self.owned]
        if available:
            self.owned.append(available[0])
        elif self.policy == "speed_proc":
            if self.owned and self.level % 2 == 0 and self.run_proc < self.options["max_run_proc_rank"]:
                self.run_proc += 1
            else:
                self.run_speed += 1
        elif self.policy == "time":
            if self.level % 2 == 0:
                self.run_damage += 1
            else:
                self.run_speed += 1
        else:
            self.run_damage += 1

    def play(self):
        stage = self.config["stages"][self.progress.stage]
        self.spawn(stage["initial"], initial=True)
        self.peak = len(self.enemies)
        self.schedule(stage["refill_seconds"], "refill", None)
        self.schedule(0, "attack", None)
        # Exact event times avoid rounding attack/refill intervals to a simulation tick.
        while self.events and self.events[0][0] <= self.duration:
            when, _, kind, payload = heapq.heappop(self.events)
            dt = when - self.time
            self.occupancy += len(self.enemies) * dt
            self.empty_ticks += dt if not self.enemies else 0
            for enemy in self.enemies.values():
                enemy.radius = max(enemy.stop, enemy.radius - enemy.speed * dt)
            self.time = when
            if kind == "hit":
                self.basic_hit(*payload)
            elif kind == "coins":
                self.coins += payload
            elif kind == "xp":
                self.xp += payload
                self.total_xp += payload
                while self.xp >= self.threshold:
                    self.xp -= self.threshold
                    self.level_up()
            elif kind == "meteor":
                origin, power = payload
                for _, target in self.nearby(origin, self.config["effects"]["meteor"]["radius"]):
                    self.hurt(target, power, "meteor")
            elif kind == "refill":
                stage = self.config["stages"][self.progress.stage]
                self.spawn(min(stage["batch"], max(0, stage["target"] - len(self.enemies))))
                self.schedule(self.time + stage["refill_seconds"], "refill", None)
            elif kind == "attack":
                if self.enemies:
                    targets = sorted(self.enemies.values(), key=lambda enemy: enemy.radius)
                    rank = self.progress.ranks.get("multishot", 0)
                    for projectile in range(1 + rank):
                        target = targets[projectile % len(targets)]
                        self.fired += 1
                        travel = target.radius / self.config["player"]["projectile_speed"]
                        if travel <= self.config["player"]["projectile_lifetime"]:
                            ratio = 1 if projectile == 0 else self.config["upgrades"]["multishot"]["damage_ratio"]
                            self.schedule(self.time + travel, "hit", (target.uid, self.main_damage() * ratio))
                speed = 1 + self.progress.ranks.get("speed", 0) * self.config["upgrades"]["speed"]["step"] + self.run_speed * self.options["run_speed_step"]
                self.schedule(self.time + self.config["player"]["attack_interval"] / speed, "attack", None)
            self.peak = max(self.peak, len(self.enemies))
        self.occupancy += len(self.enemies) * (self.duration - self.time)
        self.empty_ticks += self.duration - self.time if not self.enemies else 0
        self.progress.wallet += self.coins
        return {"seconds": self.duration, "kills": sum(self.kills), "kills_by_kind": self.kills,
                "coins": self.coins, "dropped_coins": self.dropped_coins, "xp": self.total_xp,
                "level_ups": self.level - 1, "stage": self.progress.stage, "unlocks": self.unlocks,
                "basic_hits": self.hits, "fired": self.fired, "procs": self.procs,
                "effect_damage": self.effect_damage, "first_kill_seconds": self.first_kill,
                "mean_alive": self.occupancy / self.duration, "peak_alive": self.peak,
                "empty_fraction": self.empty_ticks / self.duration,
                "overkill_fraction": self.wasted_damage / self.damage_total if self.damage_total else 0,
                "owned_effects": self.owned, "starting_ranks": dict(self.progress.ranks)}


def campaign(config, seed, policy, runs=20, profile=None):
    progress = Progress()
    records = []
    first_unlock = {}
    for index in range(runs):
        row = Run(config, progress, seed * 1009 + index, policy, profile).play()
        row["purchases"] = shop(config, progress, policy)
        row["wallet"] = progress.wallet
        row["next_ranks"] = dict(progress.ranks)
        for unlock in row["unlocks"]:
            first_unlock.setdefault(str(unlock["stage"]), index + 1)
        records.append(row)
    return {"runs": records, "unlock_run": first_unlock, "final_ranks": progress.ranks, "final_stacks": progress.kills}


def percentile(values, fraction):
    values = sorted(values)
    if not values:
        return None
    index = (len(values) - 1) * fraction
    low, high = math.floor(index), math.ceil(index)
    return values[low] + (values[high] - values[low]) * (index - low)


def stats(values):
    return {"mean": round(statistics.mean(values), 2), "p10": round(percentile(values, .1), 2),
            "p50": round(percentile(values, .5), 2), "p90": round(percentile(values, .9), 2)}


def summarize(campaigns):
    rows = []
    metrics = ["seconds", "kills", "coins", "xp", "level_ups", "stage", "basic_hits", "mean_alive", "empty_fraction", "overkill_fraction"]
    for index in range(len(campaigns[0]["runs"])):
        rows.append({metric: stats([c["runs"][index][metric] for c in campaigns]) for metric in metrics})
    unlocks = {}
    for stage in range(1, 5):
        reached = [c["unlock_run"][str(stage)] for c in campaigns if str(stage) in c["unlock_run"]]
        unlocks[str(stage)] = {"reached_fraction": len(reached) / len(campaigns), "run_conditional": stats(reached) if reached else None}
    first_purchases = {}
    keys = sorted({row["upgrade"] for c in campaigns for run in c["runs"] for row in run["purchases"]})
    for key in keys:
        first = []
        for c in campaigns:
            reached = next((index + 1 for index, run in enumerate(c["runs"]) if any(p["upgrade"] == key for p in run["purchases"])), None)
            if reached is not None:
                first.append(reached)
        first_purchases[key] = {"reached_fraction": len(first) / len(campaigns), "run_conditional": stats(first) if first else None}
    return {"runs": rows, "unlocks": unlocks, "first_purchases": first_purchases,
            "total_coins": stats([sum(row["coins"] for row in c["runs"]) for c in campaigns]),
            "total_seconds": stats([sum(row["seconds"] for row in c["runs"]) for c in campaigns]),
            "final_ranks_mean": {key: round(statistics.mean(c["final_ranks"].get(key, 0) for c in campaigns), 2) for key in sorted({key for c in campaigns for key in c["final_ranks"]})},
            "example_seed_0": campaigns[0]}


def budget_snapshots(config, samples):
    """Fixed stage and fixed initial budget, separate from snowballing campaigns."""
    output = {}
    for budget in (0, 100, 250, 500, 1000):
        output[str(budget)] = {}
        for policy in config["policies"]:
            initial = Progress(wallet=budget, stage=2)
            purchases = shop(config, initial, policy)
            rows = []
            for seed in range(samples):
                progress = Progress(ranks=dict(initial.ranks), stage=2)
                rows.append(Run(config, progress, seed, policy, {"progression_enabled": False}).play())
            output[str(budget)][policy] = {
                "ranks": initial.ranks, "purchases": purchases, "unspent_budget": initial.wallet,
                "seconds": rows[0]["seconds"], "kills": stats([row["kills"] for row in rows]),
                "coins": stats([row["coins"] for row in rows]),
                "coins_per_second": stats([row["coins"] / row["seconds"] for row in rows]),
                "level_ups": stats([row["level_ups"] for row in rows]),
                "mean_alive": stats([row["mean_alive"] for row in rows])}
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=Path(__file__).with_name("early_game_v1.json"))
    parser.add_argument("--output", type=Path, default=Path(__file__).with_name("results.json"))
    parser.add_argument("--samples", type=int, default=200)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--sensitivity", action="store_true")
    args = parser.parse_args()
    if args.samples < 1 or args.runs < 1:
        parser.error("samples and runs must be positive")
    config = json.loads(args.config.read_text(encoding="utf-8"))
    output = {"config_version": config["version"], "config_sha256": hashlib.sha256(args.config.read_bytes()).hexdigest(),
              "samples_per_policy": args.samples, "runs_per_campaign": args.runs,
              "seeds": [0, args.samples - 1], "policies": {}, "sensitivity": {}}
    for policy in config["policies"]:
        data = [campaign(config, seed, policy, args.runs) for seed in range(args.samples)]
        output["policies"][policy] = summarize(data)
        row = output["policies"][policy]
        print(policy, "last run", row["runs"][-1]["kills"], "unlocks", row["unlocks"], flush=True)
    output["same_budget_stage_2"] = budget_snapshots(config, args.samples)
    if args.sensitivity:
        profiles = {
            "low_accuracy": {"accuracy": .7}, "low_collection": {"collection_rate": .65},
            "spread_enemies": {"enemy_stop_radius": [2.5, 5.0]},
            "long_approach": {"initial_radius": [10, 20], "spawn_radius": [10, 20]}
        }
        for name, profile in profiles.items():
            data = [campaign(config, seed, "balanced", args.runs, profile) for seed in range(args.samples)]
            output["sensitivity"][name] = summarize(data)
            print(name, "total_coins", output["sensitivity"][name]["total_coins"], flush=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Saved", args.output, flush=True)


if __name__ == "__main__":
    main()
