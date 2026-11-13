#!/usr/bin/env python3
"""Validate V0 typed CSV contracts using only the Python standard library.

This validates data and route funding. It deliberately does not implement the
water simulation or certify species husbandry advice.
"""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from decimal import Decimal, InvalidOperation
from pathlib import Path


TABLE_KEYS = {
    "tanks": "tank_id",
    "items": "item_id",
    "species": "species_id",
    "plants": "id",
    "behavior_profiles": "behavior_profile_id",
    "gates": "gate_id",
    "economy": "economy_id",
    "route": "step_index",
    "compatibility": "rule_id",
}
STATUSES = {"shipping", "test_only", "planned"}
BOOLS = {"true", "false"}


class ConfigValidator:
    def __init__(self, data_dir: Path):
        self.data_dir = data_dir
        self.tables: dict[str, dict[str, dict[str, str]]] = {}
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.route_summary: dict[str, object] = {}

    def check(self, passed: bool, message: str) -> None:
        if not passed:
            self.errors.append(message)

    def number(self, row: dict[str, str], field: str, label: str) -> Decimal:
        try:
            value = Decimal(row[field])
            if not value.is_finite():
                raise InvalidOperation
            return value
        except (KeyError, InvalidOperation):
            self.errors.append(f"{label}.{field}: expected a finite number")
            return Decimal(0)

    def load(self) -> None:
        for table, key in TABLE_KEYS.items():
            path = self.data_dir / f"{table}.csv"
            if not path.is_file():
                self.errors.append(f"Missing table: {path}")
                continue
            raw = path.read_bytes()
            self.check(raw.startswith(b"\xef\xbb\xbf"), f"{path.name}: UTF-8-SIG BOM required")
            try:
                with path.open(encoding="utf-8-sig", newline="") as source:
                    reader = csv.DictReader(source)
                    fields = reader.fieldnames or []
                    self.check(len(fields) == len(set(fields)), f"{path.name}: duplicate column")
                    self.check(key in fields, f"{path.name}: missing key {key}")
                    rows = list(reader)
            except (UnicodeError, csv.Error) as exc:
                self.errors.append(f"{path.name}: {exc}")
                continue
            indexed: dict[str, dict[str, str]] = {}
            for line, row in enumerate(rows, 2):
                label = f"{path.name}:{line}"
                if None in row or any(value is None for value in row.values()):
                    self.errors.append(f"{label}: row width differs from header")
                    continue
                self.check(all(value == value.strip() for value in row.values()), f"{label}: surrounding whitespace")
                record_id = row.get(key, "")
                self.check(bool(record_id), f"{label}: empty ID")
                self.check(record_id not in indexed, f"{label}: duplicate ID {record_id}")
                if table != "route":
                    allowed_statuses = {"shipping_candidate", "test_only", "planned"} if table == "plants" else STATUSES
                    self.check(row.get("content_status") in allowed_statuses, f"{label}: invalid content_status")
                indexed[record_id] = row
            self.tables[table] = indexed

    def references(self) -> None:
        t = self.tables
        gates = t["gates"]

        def gate_exists(value: str) -> bool:
            return value == "none" or value in gates

        for name, row in t["tanks"].items():
            length = self.number(row, "inner_length_cm", name)
            width = self.number(row, "inner_width_cm", name)
            height = self.number(row, "inner_height_cm", name)
            gross = self.number(row, "gross_volume_l", name)
            effective = self.number(row, "effective_volume_l_design", name)
            self.check(min(length, width, height) > 0, f"{name}: dimensions must be positive")
            self.check(abs(length * width * height / 1000 - gross) < Decimal("0.001"), f"{name}: gross volume must follow cm dimensions")
            self.check(0 < effective < gross, f"{name}: effective design volume must be below gross volume")
            self.check(row["simulation_ready"] in BOOLS, f"{name}: simulation_ready must be boolean")
            if row["simulation_ready"] == "false":
                self.warnings.append(f"{name}: water simulation contract is pending; do not use legacy nitrogen equations")

        for name, row in t["behavior_profiles"].items():
            self.check(row["supports_schooling"] in BOOLS, f"{name}: supports_schooling must be boolean")
            for field in ("startle_sensitivity01", "approach_probability_min01", "approach_probability_max01"):
                value = self.number(row, field, name)
                self.check(0 <= value <= 1, f"{name}.{field}: expected 0..1")
            for lower, upper in (("recovery_min_real_s", "recovery_max_real_s"), ("approach_probability_min01", "approach_probability_max01")):
                self.check(self.number(row, lower, name) <= self.number(row, upper, name), f"{name}: {lower} exceeds {upper}")
            for field in ("recovery_min_real_s", "recovery_max_real_s", "approach_cooldown_real_s", "min_behavior_real_s"):
                self.check(self.number(row, field, name) >= 0, f"{name}.{field}: duration must be nonnegative")
            self.check(row["time_domain"] == "real_seconds", f"{name}: behavioral interaction must use real seconds")

        for name, row in t["species"].items():
            self.check(row["behavior_profile_id"] in t["behavior_profiles"], f"{name}: missing behavior profile")
            self.check(row["actor_kind"] in {"fish", "shrimp", "snail", "plant"}, f"{name}: unsupported actor kind")
            self.check(row["inclusion_scope"] in {"v0_default", "reference_only"}, f"{name}: invalid inclusion scope")
            self.check(self.number(row, "game_bioload_per_individual", name) >= 0, f"{name}: negative game bioload")
            minimum = self.number(row, "game_school_min", name)
            maximum = self.number(row, "game_school_max", name)
            self.check(0 <= minimum <= maximum and minimum == int(minimum) and maximum == int(maximum), f"{name}: invalid game school size range")
            if row["content_status"] == "shipping":
                profile = t["behavior_profiles"].get(row["behavior_profile_id"], {})
                self.check(profile.get("content_status") == "shipping", f"{name}: shipping species cannot use nonshipping profile")
                self.check(row["inclusion_scope"] == "v0_default", f"{name}: shipping species must be in default scope")
                if row["source_status"] != "research_verified":
                    self.warnings.append(f"{name}: species ecology/source review pending")
                if not row["display_name_en"] or not row["source_name_en"] or not row["source_id"]:
                    self.warnings.append(f"{name}: verified English species naming/source is incomplete; no ID fallback allowed")

        growth_forms = {"rhizome", "moss", "rosette", "stem", "stolon", "floating"}
        placement_tags = {"foreground", "midground", "background", "hardscape", "surface"}
        for name, row in t["plants"].items():
            self.check(row["growth_form"] in growth_forms, f"{name}: unsupported plant growth form")
            self.check(set(row["placement_tags"].split("|")) <= placement_tags, f"{name}: invalid placement tag")
            self.check(row["rhizome_exposed"] in {"true", "false", "n/a"}, f"{name}: invalid rhizome_exposed")
            self.check(bool(row["source_id"]), f"{name}: plant source_id is required")
            if row["growth_form"] == "rhizome":
                self.check(row["rhizome_exposed"] == "true", f"{name}: attachable rhizome must remain exposed")
            if row["growth_form"] == "moss":
                self.check(row["rhizome_exposed"] == "n/a", f"{name}: moss has no rhizome exposed requirement")
            if row["growth_form"] == "rosette":
                self.check(row["planting_method"] == "rooted_crown_exposed", f"{name}: rosette roots enter substrate but crown must remain exposed")
            if row["content_status"] == "shipping_candidate":
                self.check(name in t["species"] and t["species"][name]["actor_kind"] == "plant", f"{name}: plant candidate must have a species model")
                self.check(any(item["species_id"] == name and item["content_status"] == "shipping" for item in t["items"].values()), f"{name}: plant candidate must pair with a base item")
                if row["source_verification"] != "research_verified":
                    self.warnings.append(f"{name}: plant traits await source verification")

        for name, row in t["items"].items():
            self.check(gate_exists(row["unlock_gate_id"]), f"{name}: unknown unlock gate")
            species_id = row["species_id"]
            self.check(not species_id or species_id in t["species"], f"{name}: unknown species")
            if species_id and species_id in t["species"] and row["content_status"] == "shipping":
                self.check(t["species"][species_id]["content_status"] == "shipping", f"{name}: shipping item refers to nonshipping species")
                self.check(t["species"][species_id]["inclusion_scope"] == "v0_default", f"{name}: reference-only species cannot enter shop")
            units = self.number(row, "units_per_purchase", name)
            price = self.number(row, "purchase_price_coin", name)
            sell = self.number(row, "post_confirm_sell_price_per_purchase_coin", name)
            self.check(units >= 1 and units == int(units), f"{name}: units_per_purchase must be a positive integer")
            self.check(price >= 0 and sell >= 0 and price == int(price) and sell == int(sell), f"{name}: coin prices must be nonnegative integers")
            self.check(sell <= price, f"{name}: buy/sell arbitrage")
            self.check(self.number(row, "pre_first_confirm_refund_ratio", name) == 1, f"{name}: pre-confirm refund must be 100%")
            if row["route_role"] == "required_base":
                self.check(row["unlock_gate_id"] == "none", f"{name}: dry-tank mandatory item must be initially purchasable")
            if row["route_role"] == "care":
                self.check(price == 0 and sell == 0, f"{name}: basic care must remain free without recurring income")
            if row["content_status"] == "shipping" and not row["display_name_en"]:
                self.warnings.append(f"{name}: default English display name pending; do not render source ID or Chinese fallback")

        for name, row in gates.items():
            self.check(gate_exists(row["prerequisite_gate_id"]), f"{name}: missing prerequisite gate")
            self.check(row["requires_wet_tank"] in BOOLS, f"{name}: requires_wet_tank must be boolean")
            self.check(self.number(row, "min_stable_game_hours", name) >= 0, f"{name}: stable window must be nonnegative")
            visited = {name}
            cursor = row["prerequisite_gate_id"]
            while cursor != "none" and cursor in gates:
                if cursor in visited:
                    self.errors.append(f"{name}: circular gate dependency")
                    break
                visited.add(cursor)
                cursor = gates[cursor]["prerequisite_gate_id"]
            if row["condition_contract_status"] != "defined":
                self.warnings.append(f"{name}: water quality condition awaits integrated simulation contract")

        for name, row in t["economy"].items():
            self.check(gate_exists(row["required_gate_id"]), f"{name}: unknown required gate")
            self.check(row["repeatable"] in BOOLS, f"{name}: repeatable must be boolean")
            amount = self.number(row, "amount_coin", name)
            self.check(amount >= 0 and amount == int(amount), f"{name}: amount must be a nonnegative integer")
            self.check(not (row["repeatable"] == "true" and amount > 0), f"{name}: repeatable positive reward would create income loop")
        self.check(t["economy"].get("free_retry", {}).get("policy") == "restore_snapshot_transactionally", "Required route needs transactional snapshot retry")
        self.check(t["economy"].get("optional_budget_guard", {}).get("policy") == "reserve_remaining_required_cost", "Optional purchases must preserve mandatory route funding")

        for name, row in t["compatibility"].items():
            self.check(row["decision"] in {"block", "prototype_allow"}, f"{name}: unknown compatibility decision")
            for prefix in ("subject", "object"):
                kind, target = row[f"{prefix}_kind"], row[f"{prefix}_id"]
                table = {"tank": "tanks", "species": "species", "water_type": ""}.get(kind)
                self.check(table is not None, f"{name}: unsupported compatibility entity {kind}")
                if table:
                    self.check(target in t[table], f"{name}: missing {prefix} {target}")
                elif kind == "water_type":
                    self.check(target in {"freshwater", "saltwater"}, f"{name}: invalid water type")
            if row["source_status"] == "pending_review":
                self.warnings.append(f"{name}: compatibility is provisional, not release approved")

    def validate_route(self) -> None:
        t = self.tables
        gates_unlocked = {"none"}
        wallet = Decimal(0)
        base_cost = Decimal(0)
        pre_reward_cost = Decimal(0)
        total_cost = Decimal(0)
        rewards: set[str] = set()
        inventory: Counter[str] = Counter()
        wet = False
        balances: dict[str, int] = {}
        rows = list(t["route"].values())
        try:
            rows.sort(key=lambda row: int(row["step_index"]))
        except ValueError:
            self.errors.append("route: step indices must be integers")
            return
        self.check([int(row["step_index"]) for row in rows] == list(range(1, len(rows) + 1)), "route: steps must be contiguous from 1")
        for row in rows:
            label = f"route step {row['step_index']}"
            self.check(row["route_id"] == "teaching_triangle", f"{label}: only teaching_triangle is supported in V0")
            self.check(row["required_for_v0"] in BOOLS, f"{label}: required_for_v0 must be boolean")
            self.check(row["required_gate_id"] in gates_unlocked, f"{label}: required gate not yet available")
            action = row["action"]
            self.check(action in {"grant", "purchase", "care", "complete_gate"}, f"{label}: unknown action {action}")
            if action == "grant":
                econ = t["economy"].get(row["economy_id"])
                if not econ:
                    self.errors.append(f"{label}: unknown economy event")
                    continue
                self.check(econ["content_status"] == "shipping", f"{label}: nonshipping reward")
                self.check(econ["required_gate_id"] in gates_unlocked, f"{label}: reward prerequisites missing")
                self.check(econ["repeatable"] == "true" or row["economy_id"] not in rewards, f"{label}: reward claimed twice")
                wallet += self.number(econ, "amount_coin", row["economy_id"])
                rewards.add(row["economy_id"])
            elif action in {"purchase", "care"}:
                item = t["items"].get(row["item_id"])
                if not item:
                    self.errors.append(f"{label}: unknown item")
                    continue
                self.check(item["content_status"] == "shipping", f"{label}: nonshipping content on mandatory route")
                self.check(item["unlock_gate_id"] in gates_unlocked, f"{label}: item still locked")
                count = self.number(row, "purchase_count", label)
                self.check(count >= 1 and count == int(count), f"{label}: purchase_count must be a positive integer")
                cost = self.number(item, "purchase_price_coin", item["item_id"]) * count
                wallet -= cost
                total_cost += cost
                if "trial_reward" not in rewards:
                    pre_reward_cost += cost
                if item["route_role"] == "required_base":
                    base_cost += cost
                self.check(wallet >= 0, f"{label}: insufficient coins; mandatory route soft-lock")
                if item["species_id"]:
                    inventory[item["species_id"]] += int(count * self.number(item, "units_per_purchase", item["item_id"]))
            elif action == "complete_gate":
                gate_id = row["completes_gate_id"]
                gate = t["gates"].get(gate_id)
                if not gate:
                    self.errors.append(f"{label}: unknown completed gate")
                    continue
                self.check(gate["prerequisite_gate_id"] in gates_unlocked, f"{label}: gate completed out of order")
                self.check(gate_id not in gates_unlocked, f"{label}: gate completed twice")
                if gate_id == "gate_filled":
                    wet = True
                self.check(gate["requires_wet_tank"] == "false" or wet, f"{label}: time-dependent wet gate before fill")
                gates_unlocked.add(gate_id)
                balances[gate_id] = int(wallet)

        self.check("gate_complete" in gates_unlocked, "route never reaches main-fish completion")
        self.check(base_cost == 1110, "teaching base cost must total 1110 coins")
        self.check(pre_reward_cost == 1410, "mandatory pre-reward purchases must total 1410 coins")
        self.check(wallet == 280, "route final wallet should be 280 coins at current seed prices")
        self.check(inventory["ember_tetra"] == 8 and inventory["neon_tetra"] == 8 and inventory["neocaridina_red"] == 6 and inventory["white_cloud"] == 0, "route pack quantities must produce 8 Ember Tetra, 8 Neon Tetra, 6 shrimp, and no cool-water White Cloud Mountain Minnow")
        for species_id, count in inventory.items():
            species = t["species"].get(species_id)
            if species and species["actor_kind"] == "fish":
                self.check(count >= self.number(species, "game_school_min", species_id), f"{species_id}: mandatory route below game school minimum")
        load = sum((self.number(t["species"][key], "game_bioload_per_individual", key) * count for key, count in inventory.items()), Decimal(0))
        capacity = self.number(t["tanks"].get("tank_60cm", {}), "game_bioload_capacity", "tank_60cm")
        self.check(load <= capacity * Decimal("0.8"), "teaching route exceeds 80% design stocking budget")
        filter_capacity = self.number(t["items"].get("filter_hangon_basic", {}), "game_filter_capacity", "filter_hangon_basic")
        margin = (filter_capacity - load) / capacity if capacity > 0 else Decimal(-1)
        self.check(margin >= Decimal("0.2"), "teaching route filter capacity leaves less than 20% design margin")
        self.route_summary = {"base_cost_coin": int(base_cost), "pre_reward_required_cost_coin": int(pre_reward_cost), "total_spend_coin": int(total_cost), "final_wallet_coin": int(wallet), "balance_at_gates": balances, "individuals": dict(inventory), "game_bioload_points": float(load), "design_filter_margin": float(margin), "care_cost_coin": 0}

    def run(self) -> dict[str, object]:
        self.load()
        if set(self.tables) == set(TABLE_KEYS) and not self.errors:
            try:
                self.references()
                self.validate_route()
            except KeyError as exc:
                self.errors.append(f"Missing required column: {exc}")
        return {"ok": not self.errors, "errors": self.errors, "warnings": self.warnings, "records": {key: len(value) for key, value in self.tables.items()}, "route": self.route_summary}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-dir", type=Path, default=Path(__file__).resolve().parents[1] / "data")
    parser.add_argument("--strict-release", action="store_true", help="Fail while ecology, compatibility or simulation contracts remain unreviewed")
    parser.add_argument("--json", action="store_true", help="Print machine-readable results")
    args = parser.parse_args()
    result = ConfigValidator(args.data_dir).run()
    if args.strict_release and result["warnings"]:
        result["ok"] = False
        result["errors"].append("Release gate blocked: unresolved review warnings remain")
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print("PASS: typed data and mandatory route" if result["ok"] else "FAIL: config validation")
        print("Records: " + ", ".join(f"{name}={count}" for name, count in result["records"].items()))
        if result["route"]:
            print("Route: " + json.dumps(result["route"], ensure_ascii=False))
        for error in result["errors"]:
            print("ERROR: " + error)
        for warning in result["warnings"]:
            print("PENDING: " + warning)
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
