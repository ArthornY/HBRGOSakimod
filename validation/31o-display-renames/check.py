"""One-shot requirement 27 audit against the frozen, accepted pre-26/27 source.

This reads source/localization only. Native rendering and save compatibility are
covered by the project's effect suite; no mod assembly or game is executed here.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re


ROWS = """PiercingShotHbrgoSaki|穿透射击|刮奏
FragileDoubleStrikeHbrgoSaki|弱点打击|泛音
ChargedShotHbrgoSaki|蓄力射击|引而不发
FocusedShotHbrgoSaki|专注射击|屏息
SniperModeHbrgoSaki|狙击模式|独奏
SwitchingSlashHbrgoSaki|切换斩击|残心
DualSwordsEnhancementHbrgoSaki|双剑强化|卡农
QuickSwitchHbrgoSaki|快速切换|变奏
MagicSwordEnhancementHbrgoSaki|魔剑强化|兜割
TeleportHbrgoSaki|紧急脱离|纳刀
MagicSwordUniversalityHbrgoSaki|魔剑通用化|剑心为薪
MagicSwordSharingHbrgoSaki|魔剑共享|祥光
PerTurnChargeHbrgoSaki|每回合充能|通奏低音
RapidChargeHbrgoSaki|极速充能|急板
ChargeStrikeHbrgoSaki|充能打击|前奏
ChargeBurstHbrgoSaki|充能爆发|绝唱
OverclockUpgradeHbrgoSaki|OD升级|转调
DoublePowersHbrgoSaki|翻倍|高八度
StrengthenHbrgoSaki|强化|吉兆
ExtremeDodgeHbrgoSaki|极限闪避|间不容发
DoubleArmorBreakHbrgoSaki|双重脆弱|乘虚
RemoveDpHbrgoSaki|移除|破壁
EnergyDrainHbrgoSaki|能量汲取|以战养战
OverdrawMovementHbrgoSaki|透支移动|透支
CoveringFireHbrgoSaki|火力掩护|安可
TrainingTimeHbrgoSaki|训练时间|练习曲
RestTimeHbrgoSaki|休息时间|休止符
AllRounderHbrgoSaki|全能之人|六艺
MimicDreamBubbleHbrgoSaki|模仿·梦幻泡影|模仿-梦幻泡影"""
RENAMES = [tuple(row.split("|")) for row in ROWS.splitlines()]
COMMON = {"翻倍", "移除", "强化"}
POWERS = {
    "FOCUSED_SHOT_POWER": "屏息", "SNIPER_MODE_POWER": "独奏",
    "DUAL_SWORDS_ENHANCEMENT_POWER": "卡农", "QUICK_SWITCH_POWER": "变奏",
    "MAGIC_SWORD_ENHANCEMENT_POWER": "兜割", "MAGIC_SWORD_UNIVERSALITY_POWER": "剑心为薪",
    "HBRGOSAKIMOD-MAGIC_SWORD_UNIVERSALITY_POWER": "剑心为薪",
    "MAGIC_SWORD_SHARING_POWER": "祥光", "PER_TURN_CHARGE_POWER": "通奏低音",
    "STRENGTHEN_POWER": "吉兆", "EXTREME_DODGE_ROLL_PENALTY_POWER": "间不容发",
    "ENERGY_DRAIN_POWER": "以战养战", "TRAINING_TIME_POWER": "练习曲",
    "ALL_ROUNDER_POWER": "六艺",
}
TOKEN = re.compile(r'//[^\r\n]*|/\*[\s\S]*?\*/|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\s+|[^\s]', re.MULTILINE)
LOC = Path("HBRGOSakiMod/localization")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def load(path: Path):
    return json.loads(read(path))


def displayed(text: str) -> str:
    # Common verbs are not card references; only their exact title keys change.
    for _, old, new in RENAMES:
        if old not in COMMON:
            text = text.replace(old, new)
    return text.replace("Repture-", "Rapture-").replace("Repture Bow", "Rapture Bow")


def tokens(text: str, rename: bool = False) -> list[str]:
    result = []
    for match in TOKEN.finditer(text):
        value = match.group()
        if value.isspace() or value.startswith(("//", "/*")):
            continue
        if rename and '"' in value:
            value = displayed(value)
            # Explicit RollModifierDisplayEntry names, not general wording.
            if value == '"强化"':
                value = '"吉兆"'
        result.append(value)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--baseline-root", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    repo = args.repo.resolve()
    baseline = (args.baseline_root or repo / "outputs/desuwa-renames-20261114/rename-before").resolve()
    snapshot = load(baseline / "snapshot.json")
    passed, failed = [], []

    def check(ok: bool, message: str):
        (passed if ok else failed).append(message)

    for rel, expected in snapshot["files"].items():
        original = baseline / rel
        check(hashlib.sha256(original.read_bytes()).hexdigest() == expected, f"frozen baseline hash: {rel}")
    cards_before = load(baseline / LOC / "zhs/cards.json")
    cards = load(repo / LOC / "zhs/cards.json")
    title_changes = {}
    for cls, old, new in RENAMES:
        keys = [key for key, value in cards_before.items() if key.endswith(".title") and value == old]
        check(len(keys) == 1, f"unambiguous accepted title key: {cls}")
        if len(keys) == 1:
            title_changes[keys[0]] = new
            check(cards.get(keys[0]) == new, f"card title and stable key: {cls} / {keys[0]}")
        relative = Path("Core/Models/Cards") / (cls + ".cs")
        source = read(repo / relative)
        check(re.search(r"\bclass\s+" + re.escape(cls) + r"\b", source) is not None, f"card class kept: {cls}")
    powers = load(repo / LOC / "zhs/powers.json")
    for key, expected in POWERS.items():
        check(powers.get(key + ".title") == expected, f"power title and stable key: {key}")

    # Compare every existing localization value, rather than a second copy of
    # numeric effect implementations. Requirement 26 adds only desuwa's keys.
    for old_file in sorted((baseline / LOC).glob("*/*.json")):
        rel = old_file.relative_to(baseline)
        before, after = load(old_file), load(repo / rel)
        added = set(after) - set(before)
        allowed = {key for key in added if rel == LOC / "zhs/cards.json" and key.startswith("DESUWA_HBRGO_SAKI.")}
        check(set(before) <= set(after) and added == allowed, f"localization keys preserved, only desuwa added: {rel}")
        for key, value in before.items():
            if rel.parts[-2] == "eng":
                expected = value
            elif rel.name == "cards.json" and key in title_changes:
                expected = title_changes[key]
            elif rel.name == "powers.json" and key.removesuffix(".title") in POWERS and key.endswith(".title"):
                expected = POWERS[key.removesuffix(".title")]
            else:
                expected = displayed(value) if isinstance(value, str) else value
            check(after.get(key) == expected, f"display-only value; variables/numbers/common verbs unchanged: {rel}/{key}")

    # Exact accepted C# tokens include classes, identifiers, costs, numbers,
    # portrait paths and ModelIDs. Only approved string labels may differ.
    for rel in snapshot["files"]:
        if not rel.endswith(".cs"):
            continue
        check(tokens(read(baseline / rel), rename=True) == tokens(read(repo / rel)), f"C# behavior, IDs and portraits kept: {rel}")

    stale_literals = []
    old_names = [old for _, old, _ in RENAMES if old not in COMMON]
    for path in (repo / "Core").rglob("*.cs"):
        for match in TOKEN.finditer(read(path)):
            value = match.group()
            if value.startswith(("//", "/*")) or '"' not in value:
                continue
            if any(old in value for old in old_names) or re.search(r"\bRepture(?:[- ]|\b(?![A-Za-z_]))", value):
                stale_literals.append(f"{path.relative_to(repo)}:{read(path).count(chr(10), 0, match.start()) + 1}: {value}")
    check(not stale_literals, "production display strings contain no stale proper names; Repture identifiers remain allowed")
    report = {
        "ok": not failed, "checks": len(passed) + len(failed), "passed": len(passed),
        "failed": failed, "staleProductionStrings": stale_literals,
        "cards": 29, "powerTitles": 14, "baseline": str(baseline),
        "baselineSha256": hashlib.sha256((baseline / "snapshot.json").read_bytes()).hexdigest(),
        "boundary": "Read-only source/localization audit. Historical run-upload labels and original HBR/NGA quotations are not rewritten. No native save load, rendered UI, mod build, Godot, packaging or website export.",
    }
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("ok", "checks", "passed", "cards", "powerTitles")}, ensure_ascii=False))
    for failure in failed[:12]:
        print("FAIL:", failure)
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
