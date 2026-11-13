#!/usr/bin/env python3
"""Compare explicitly recorded manual-player snapshots, never the live player save."""
import argparse
from collections import Counter
import datetime as dt
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "verification"
KINDS = {"paused_exact", "paused_commands", "resume_light", "resume_feed", "restore_paused"}
RELATIVE_BOUND = Decimal("1e-12")


def reject_constant(value):
    raise ValueError("Non-finite JSON number: " + value)


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate JSON field: " + key)
        value[key] = item
    return value


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_float=Decimal,
                      parse_constant=reject_constant, object_pairs_hook=unique_object)


def json_safe(value):
    if isinstance(value, Decimal):
        return str(value)
    if isinstance(value, dict):
        return {key: json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [json_safe(item) for item in value]
    return value


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence_path(value, base):
    path = Path(value)
    path = (base / path).resolve() if not path.is_absolute() else path.resolve()
    try:
        path.relative_to(EVIDENCE.resolve())
    except ValueError:
        raise ValueError("Snapshot/screenshot must be a recorded verification artifact: " + str(path))
    if not path.is_file():
        raise ValueError("Missing evidence file: " + str(path))
    return path


def decode_fingerprint(value):
    # These no-argument commands intentionally use their literal name in production GameSession.
    if value in {"undo", "redo", "confirm", "fill", "challenge"}:
        return [value]
    fields, position = [], 0
    while position < len(value):
        end = value.find(":", position)
        if end < 0:
            raise ValueError("Malformed session fingerprint: " + value)
        token = value[position:end]
        if not re.fullmatch(r"-1|0|[1-9][0-9]*", token):
            raise ValueError("Malformed fingerprint field length: " + token)
        length = int(token)
        position = end + 1
        if length == -1:
            fields.append(None)
        else:
            if position + length > len(value):
                raise ValueError("Truncated fingerprint: " + value)
            fields.append(value[position:position + length])
            position += length
    return fields


def enum_names(path, name):
    match = re.search(r"enum\s+" + re.escape(name) + r"\s*\{([^}]+)\}", path.read_text())
    if not match:
        raise ValueError("Missing source enum " + name)
    names = [part.strip() for part in match.group(1).split(",") if part.strip()]
    if any(not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9]*", item) for item in names):
        raise ValueError("Review explicitly valued/changed enum before analysis: " + name)
    return names


def named_enum(value, names):
    if type(value) is not int or not 0 <= value < len(names):
        raise ValueError("Unknown enum value: " + str(value))
    return names[value]


def compare(left, right, codec=False):
    differences, numeric_differences = [], []
    count, max_error = 0, Decimal(0)

    def difference(path, kind, a, b):
        nonlocal count
        count += 1
        if len(differences) < 64:
            differences.append({"path": path, "kind": kind, "before": a, "after": b})

    def visit(a, b, path):
        nonlocal max_error
        if type(a) is not type(b):
            difference(path, "type", a, b)
        elif isinstance(a, dict):
            for key in sorted(a.keys() | b.keys()):
                if key not in a or key not in b:
                    difference(path + "." + key, "missing_field", a.get(key), b.get(key))
                else:
                    visit(a[key], b[key], path + "." + key)
        elif isinstance(a, list):
            if len(a) != len(b):
                difference(path, "array_length", len(a), len(b))
            for i, (x, y) in enumerate(zip(a, b)):
                visit(x, y, path + "[" + str(i) + "]")
        elif isinstance(a, Decimal) and codec:
            error = abs(a - b)
            bound = RELATIVE_BOUND * max(Decimal(1), abs(a), abs(b))
            if error:
                numeric_differences.append({"path": path, "before": a, "after": b,
                                            "absolute_error": error, "bound": bound})
                max_error = max(max_error, error)
            if error > bound:
                difference(path, "decimal_outside_codec_bound", a, b)
        elif a != b:
            difference(path, "value", a, b)

    visit(left, right, "$")
    return {"passed": count == 0, "comparison": "codec" if codec else "exact",
            "difference_count": count, "differences_first_64": differences,
            "nonzero_decimal_differences": numeric_differences, "maximum_decimal_error": max_error}


def receipt_map(snapshot):
    result = {}
    for receipt in snapshot["Receipts"]:
        key = receipt["TransactionId"]
        if not key or key in result:
            raise ValueError("Missing or duplicate receipt TransactionId")
        fields = decode_fingerprint(receipt["Fingerprint"])
        if not fields:
            raise ValueError("Empty session fingerprint")
        result[key] = {**receipt, "decoded": fields}
    return result


def summary(label, path, data, species, actions, stages):
    world, game, ecology = data["World"], data["Gameplay"], data["Ecology"]
    if (data["SchemaVersion"], world["SchemaVersion"], game["SchemaVersion"]) != (1, 2, 2):
        raise ValueError("Unsupported session/world/gameplay schema in " + label)
    by_species, by_kind = Counter(), Counter()
    distribution, by_kind_actions = Counter(), {"fish": Counter(), "shrimp": Counter(), "unknown": Counter()}
    counters = Counter()
    ids = set()
    for agent in world["Agents"]:
        if agent["EntityId"] in ids:
            raise ValueError("Duplicate agent ID in " + label)
        ids.add(agent["EntityId"])
        identity = agent["SpeciesId"]
        kind = species.get(identity, "unknown")
        if kind not in by_kind_actions:
            kind = "unknown"
        action = named_enum(agent["Action"], actions)
        by_species[identity] += 1
        by_kind[kind] += 1
        distribution[action] += 1
        by_kind_actions[kind][action] += 1
        for field in ["Meals", "Startles", "GlassApproaches", "ProbeBursts", "GrazeDwells", "ActionChanges"]:
            counters[field] += agent[field]
    receipts = receipt_map(data)
    return {"label": label, "path": str(path), "sha256": digest(path), "bytes": path.stat().st_size,
            "file_mtime_utc": dt.datetime.fromtimestamp(path.stat().st_mtime, dt.timezone.utc).isoformat(),
            "save_id": game["SaveId"], "real_seconds": world["RealSeconds"],
            "world_ecology_days": world["EcologyDays"], "ecology_days": ecology["GameDays"],
            "fixed_accumulator": world["FixedAccumulator"], "ecology_rate": data["EcologyRate"],
            "paused": game["Paused"], "light": world["LightIsOn"], "coins": game["WalletCoin"],
            "stage": named_enum(game["Stage"], stages), "filled": game["Filled"],
            "food_bindings": data["Food"], "food_portions": sum(x["RemainingPortions"] for x in data["Food"]),
            "food_mg_n": ecology["State"]["FoodMgN"], "living_mg_n": ecology["State"]["LivingMgN"],
            "food_input_mg_n": ecology["Audit"]["InputByPoolMgN"][0],
            "cumulative_ingested_mg_n": ecology["Audit"]["TransferMgN"][5],
            "cumulative_food_to_organic_mg_n": ecology["Audit"]["TransferMgN"][1],
            "owned_living_reserves_mg_n": sum((entry["MgN"] for entry in data["LivingReserves"]["Entries"]), Decimal(0)),
            "stimuli": world["Stimuli"], "next_event_id": world["NextEventId"],
            "receipts": len(receipts), "receipts_by_operation": Counter(x["decoded"][0] for x in receipts.values()),
            "agents": len(ids), "species": by_species, "kinds": by_kind,
            "actions": distribution, "actions_by_kind": by_kind_actions, "behavior_counters": counters,
            "agent_food_counters": [{"entity_id": agent["EntityId"], "species_id": agent["SpeciesId"],
                                     "meals": agent["Meals"], "last_food_id": agent["LastFoodId"]} for agent in world["Agents"]]}


def run_check(spec, snapshots, summaries):
    kind, before, after = spec["kind"], spec["before"], spec["after"]
    if kind not in KINDS:
        raise ValueError("Unknown manual check kind: " + kind)
    a, b = snapshots[before], snapshots[after]
    sa, sb = summaries[before], summaries[after]
    checks = []

    def require(name, condition, detail=None):
        checks.append({"name": name, "passed": bool(condition), "detail": detail})

    require("same save identity", sa["save_id"] == sb["save_id"])
    require("same fish and shrimp identities", sorted((x["EntityId"], x["SpeciesId"]) for x in a["World"]["Agents"]) ==
            sorted((x["EntityId"], x["SpeciesId"]) for x in b["World"]["Agents"]))
    report = {"kind": kind, "before": before, "after": after, "assertions": checks,
              "physical_evidence": "Requires the coordinator's input observation and screenshot review; state alone cannot identify a key or mouse click."}
    if kind in {"paused_exact", "paused_commands", "restore_paused"}:
        require("both snapshots paused", sa["paused"] is True and sb["paused"] is True)
        result = compare(a, b, codec=kind == "restore_paused")
        report["snapshot_comparison"] = result
        require("whole snapshot matches", result["passed"])
        report["byte_equal"] = sa["sha256"] == sb["sha256"]
        if kind != "restore_paused":
            require("serialized bytes identical", report["byte_equal"])
        if kind == "paused_commands":
            require("no accepted F/L receipt", receipt_map(a) == receipt_map(b))
    else:
        expected_pause = spec.get("after_paused", False)
        if type(expected_pause) is not bool:
            raise ValueError("after_paused must be a boolean")
        require("pause state matches declared capture state", sb["paused"] is expected_pause,
                {"expected_paused": expected_pause, "actual_paused": sb["paused"]})
        require("real clock advanced", sb["real_seconds"] > sa["real_seconds"],
                {"before": sa["real_seconds"], "after": sb["real_seconds"]})
        previous, current = receipt_map(a), receipt_map(b)
        removed = sorted(previous.keys() - current.keys())
        changed = sorted(key for key in previous.keys() & current.keys() if previous[key] != current[key])
        added = [current[key] for key in sorted(current.keys() - previous.keys())]
        report["new_receipts"] = added
        require("prior receipts retained unchanged", not removed and not changed,
                {"removed": removed, "changed": changed})
        operation = "light" if kind == "resume_light" else "feed"
        accepted = [item for item in added if item["decoded"][0] == operation and item["Result"]["Success"] is True]
        require("exactly one accepted " + operation + " command", len(accepted) == 1)
        if kind == "resume_light":
            require("light toggled", sa["light"] != sb["light"])
            if accepted:
                fields = accepted[0]["decoded"]
                require("light receipt matches saved state", len(fields) == 2 and fields[1] == str(sb["light"]))
        else:
            expected = None
            if accepted:
                fields = accepted[0]["decoded"]
                require("feed receipt has position and portion arguments", len(fields) == 5)
                if len(fields) == 5:
                    valid_portions = fields[4] is not None and re.fullmatch(r"[1-9][0-9]*", fields[4]) is not None
                    require("feed receipt contains integer portions", valid_portions)
                    if valid_portions:
                        expected = Decimal(fields[4]) * Decimal("0.12")
            delta = sb["food_input_mg_n"] - sa["food_input_mg_n"]
            report["external_food_input_delta_mg_n"] = delta
            report["ingested_delta_mg_n"] = sb["cumulative_ingested_mg_n"] - sa["cumulative_ingested_mg_n"]
            report["meal_counter_delta"] = sb["behavior_counters"]["Meals"] - sa["behavior_counters"]["Meals"]
            first_new_event = sa["next_event_id"]
            report["agents_reporting_new_food_event"] = [item for item in sb["agent_food_counters"] if item["last_food_id"] >= first_new_event]
            if expected is not None:
                bound = RELATIVE_BOUND * max(Decimal(1), abs(expected), abs(delta))
                require("new external food nitrogen matches receipt portions", abs(delta - expected) <= bound,
                        {"expected": expected, "actual": delta, "bound": bound})
            require("food source event allocated", sb["next_event_id"] > sa["next_event_id"])
        if spec.get("same_wallet", True):
            require("wallet unchanged", sa["coins"] == sb["coins"],
                    {"before": sa["coins"], "after": sb["coins"]})
    report["passed"] = all(item["passed"] for item in checks)
    return report


def compact_counts(counts):
    return ", ".join(str(key) + " " + str(value) for key, value in sorted(counts.items())) or "—"


def markdown(report):
    lines = ["# Recorded manual-player snapshot analysis", "", report["coverage"], "",
             "| Snapshot | Real seconds | Ecology days | Paused | Light | Coins | Food portions | Receipts | Fish / shrimp | Behavior |",
             "|---|---:|---:|---|---|---:|---:|---:|---|---|"]
    for item in report["snapshots"]:
        lines.append("| " + " | ".join(str(x) for x in [item["label"], item["real_seconds"],
                     item["world_ecology_days"], item["paused"], item["light"], item["coins"],
                     item["food_portions"], item["receipts"], str(item["kinds"].get("fish", 0)) + " / " + str(item["kinds"].get("shrimp", 0)),
                     compact_counts(item["actions"])]) + " |")
    lines.extend(["", "| Check | Before → after | State result |", "|---|---|---|"])
    failures = []
    for item in report["checks"]:
        lines.append("| " + item["kind"] + " | " + item["before"] + " → " + item["after"] + " | " + ("PASS" if item["passed"] else "FAIL") + " |")
        for assertion in item["assertions"]:
            if not assertion["passed"]:
                failures.append(item["kind"] + ": " + assertion["name"])
    if failures:
        lines.extend(["", "Failed assertions:", ""] + ["- " + value for value in failures])
    lines.extend(["", "Physical input and screenshot findings remain a separate coordinator review.", "", "Recorded artifacts:", ""])
    for item in report["snapshots"]:
        lines.append("- [" + item["label"] + "](<" + item["path"] + ">), SHA256 " + item["sha256"])
    for item in report["screenshots"]:
        lines.append("- [" + item["label"] + "](<" + item["path"] + ">): " + item.get("observation", "Pending visual review"))
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", required=True, type=Path, help="Explicit snapshot/check manifest; paths relative to this file")
    parser.add_argument("--output", required=True, type=Path, help="New analysis directory under verification; never overwritten")
    parser.add_argument("--project", type=Path, default=ROOT / "unity6-validation", help="Read-only catalog and enum source")
    args = parser.parse_args()
    plan_path = evidence_path(str(args.plan.resolve()), ROOT)
    plan_sha = digest(plan_path)
    plan = read_json(plan_path)
    output = args.output.resolve()
    output.relative_to(EVIDENCE.resolve())
    if output.exists():
        raise ValueError("Refusing to overwrite analysis/evidence directory: " + str(output))
    project = args.project.resolve()
    catalog_path = project / "Assets/_Game/Resources/Configuration/runtime.json"
    profiles_path = project / "Assets/_Game/Core/Profiles.cs"
    game_path = project / "Assets/_Game/Core/Gameplay/GameplayModels.cs"
    source_hashes = {str(p): digest(p) for p in [Path(__file__), catalog_path, profiles_path, game_path]}
    catalog = read_json(catalog_path)
    species = {item["SpeciesId"]: item["ActorKind"].lower() for item in catalog["Species"]}
    actions, stages = enum_names(profiles_path, "ActionKind"), enum_names(game_path, "GameplayStage")
    snapshots, summaries = {}, {}
    for label, value in plan["snapshots"].items():
        path = evidence_path(value, plan_path.parent)
        before_sha = digest(path)
        snapshots[label] = read_json(path)
        summaries[label] = summary(label, path, snapshots[label], species, actions, stages)
        if summaries[label]["sha256"] != before_sha:
            raise ValueError("Recorded snapshot changed while being read: " + str(path))
    checks = [run_check(item, snapshots, summaries) for item in plan.get("checks", [])]
    screenshots = []
    for item in plan.get("screenshots", []):
        path = evidence_path(item["path"], plan_path.parent)
        screenshots.append({**item, "path": str(path), "sha256": digest(path), "visual_review": "not_performed_by_this_tool"})
    report = {"status": "state_evidence_passed" if checks and all(x["passed"] for x in checks) else "state_evidence_failed" if checks else "summary_only",
              "coverage": "Read-only recorded JSON comparison. No live-save discovery, Unity Restore execution, input injection, screenshot interpretation or physical-input certification.",
              "created_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "plan": {"path": str(plan_path), "sha256": plan_sha},
              "source_sha256": source_hashes,
              "snapshots": list(summaries.values()), "checks": checks, "screenshots": screenshots}
    for item in report["snapshots"]:
        if digest(Path(item["path"])) != item["sha256"]:
            raise ValueError("Recorded snapshot changed during analysis: " + item["path"])
    for path, expected in [(str(plan_path), plan_sha)] + list(source_hashes.items()):
        if digest(Path(path)) != expected:
            raise ValueError("Plan or source changed during analysis: " + path)
    output.mkdir(parents=True, exist_ok=False)
    (output / "report.json").write_text(json.dumps(json_safe(report), ensure_ascii=False, indent=2) + "\n")
    (output / "analysis.md").write_text(markdown(report))
    print(report["status"], output)
    return 1 if report["status"] == "state_evidence_failed" else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, KeyError, TypeError, OSError, ArithmeticError) as error:
        print("Analysis failed: " + str(error), file=sys.stderr)
        sys.exit(1)
