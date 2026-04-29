import argparse
import json
import re
import sys
from pathlib import Path

import UnityPy


MONSTERS_FILE = "monsters.json"
PET_SKIN_FILE = "pet_skin.json"
CONFIG_MONSTERS_ASSET_PATH = "Assets/Game/Configs/bytes/monsters.bytes"
CONFIG_PET_SKIN_ASSET_PATH = "Assets/Game/Configs/bytes/pet_skin.bytes"
HASH_REGEX = re.compile(r"\b[0-9a-f]{32}\b")
ALLOWED_NAME_REGEX = re.compile(r"^[\u4e00-\u9fffA-Za-z0-9\-\s\u3000\u00b7]+$")
HAS_CHINESE_REGEX = re.compile(r"[\u4e00-\u9fff]")


def normalize_path(path_value: str) -> Path:
    return Path(path_value).expanduser().resolve()


def looks_like_source_root(source_root: Path) -> bool:
    return (
        (source_root / "Seer_Data" / "yoo" / "ConfigPackage" / "ManifestFiles" / "PackageManifest_ConfigPackage.version").is_file()
        and (source_root / "Seer_Data" / "yoo" / "DefaultPackage" / "ManifestFiles" / "PackageManifest_DefaultPackage.version").is_file()
    )


def package_manifest_dir(source_root: Path, package_name: str) -> Path:
    return source_root / "Seer_Data" / "yoo" / package_name / "ManifestFiles"


def package_version_path(source_root: Path, package_name: str) -> Path:
    return package_manifest_dir(source_root, package_name) / f"PackageManifest_{package_name}.version"


def package_version(source_root: Path, package_name: str) -> str:
    path = package_version_path(source_root, package_name)
    if not path.is_file():
        return ""
    return path.read_text(encoding="utf-8-sig").strip()


def package_manifest_bytes_path(source_root: Path, package_name: str) -> Path:
    manifest_dir = package_manifest_dir(source_root, package_name)
    version = package_version(source_root, package_name)
    if version:
        exact_path = manifest_dir / f"PackageManifest_{package_name}_{version}.bytes"
        if exact_path.is_file():
            return exact_path

    candidates = sorted(
        manifest_dir.glob(f"PackageManifest_{package_name}_*.bytes"),
        key=lambda item: item.stat().st_mtime,
        reverse=True,
    )
    return candidates[0] if candidates else Path()


def bundle_data_path(source_root: Path, package_name: str, bundle_hash: str) -> Path:
    return (
        source_root
        / "Seer_Data"
        / "yoo"
        / package_name
        / "CacheBundleFiles"
        / bundle_hash[:2]
        / bundle_hash
        / "__data"
    )


def read_manifest_index(source_root: Path, package_name: str) -> tuple[bytes, str, list[str]]:
    manifest_path = package_manifest_bytes_path(source_root, package_name)
    if not manifest_path.is_file():
        raise FileNotFoundError(f"{package_name} manifest not found under {source_root}")

    manifest_bytes = manifest_path.read_bytes()
    manifest_text = manifest_bytes.decode("latin-1")
    ordered_hashes: list[str] = []
    seen_hashes: set[str] = set()
    for match in HASH_REGEX.finditer(manifest_text):
        bundle_hash = match.group(0)
        if bundle_hash not in seen_hashes:
            seen_hashes.add(bundle_hash)
            ordered_hashes.append(bundle_hash)

    return manifest_bytes, manifest_text, ordered_hashes


def bundle_hash_for_asset_path(manifest_bytes: bytes, manifest_text: str, ordered_hashes: list[str], asset_path: str) -> str:
    asset_offset = manifest_text.find(asset_path)
    if asset_offset < 0:
        return ""

    bundle_index_offset = asset_offset + len(asset_path)
    if bundle_index_offset + 1 >= len(manifest_bytes):
        return ""

    bundle_index = manifest_bytes[bundle_index_offset] + (manifest_bytes[bundle_index_offset + 1] * 256)
    if not 0 <= bundle_index < len(ordered_hashes):
        return ""

    return ordered_hashes[bundle_index]


def read_text_asset_bytes_from_bundle(source_root: Path, package_name: str, asset_path: str) -> bytes:
    manifest_bytes, manifest_text, ordered_hashes = read_manifest_index(source_root, package_name)
    bundle_hash = bundle_hash_for_asset_path(manifest_bytes, manifest_text, ordered_hashes, asset_path)
    if not bundle_hash:
        raise FileNotFoundError(f"Unable to resolve bundle hash for {asset_path}")

    bundle_path = bundle_data_path(source_root, package_name, bundle_hash)
    if not bundle_path.is_file():
        raise FileNotFoundError(f"Bundle file not found: {bundle_path}")

    env = UnityPy.load(str(bundle_path))
    target_key = asset_path.lower()
    for container_path, obj in env.container.items():
        if container_path.lower() != target_key:
            continue

        asset = obj.read()
        script = getattr(asset, "m_Script", None)
        if isinstance(script, bytes):
            return bytes(script)
        if isinstance(script, str):
            return script.encode("utf-8", "surrogateescape")

        raise TypeError(f"Unsupported TextAsset payload type for {asset_path}: {type(script).__name__}")

    raise FileNotFoundError(f"TextAsset not found in bundle for {asset_path}")


def build_output_paths(output_root: Path, layout: str) -> tuple[Path, Path]:
    if layout == "mirror":
        config_dir = output_root / "config"
        return config_dir / MONSTERS_FILE, config_dir / PET_SKIN_FILE
    if layout == "cache":
        return output_root / MONSTERS_FILE, output_root / PET_SKIN_FILE
    raise ValueError(f"Unexpected layout: {layout}")


def read_monsters_bytes(source_root: Path) -> tuple[bytes, str]:
    raw_path = source_root / "Seer_Data" / "yoo" / "ConfigPackage" / "rawfile" / "__data" / "monsters.bytes"
    if raw_path.is_file():
        return raw_path.read_bytes(), "local-rawfile-monsters.bytes"

    return read_text_asset_bytes_from_bundle(source_root, "ConfigPackage", CONFIG_MONSTERS_ASSET_PATH), "config-bundle-monsters.bytes"


def read_pet_skin_bytes(source_root: Path) -> tuple[bytes, str]:
    txt_path = source_root / "Seer_Data" / "yoo" / "ConfigPackage" / "rawfile_txt" / "pet_skin.txt"
    if txt_path.is_file():
        return txt_path.read_bytes(), "local-pet_skin.txt"

    return read_text_asset_bytes_from_bundle(source_root, "ConfigPackage", CONFIG_PET_SKIN_ASSET_PATH), "config-bundle-pet_skin.bytes"


def is_monster_name_candidate(candidate: str) -> bool:
    return bool(candidate) and len(candidate) <= 32 and HAS_CHINESE_REGEX.search(candidate) and ALLOWED_NAME_REGEX.match(candidate)


def extract_monster_names(data: bytes) -> list[str]:
    names: list[str] = []
    for index in range(0, max(0, len(data) - 2)):
        byte_length = data[index] | (data[index + 1] << 8)
        if byte_length < 1 or byte_length > 48 or index + 2 + byte_length > len(data):
            continue

        try:
            candidate = data[index + 2 : index + 2 + byte_length].decode("utf-8").strip()
        except Exception:
            continue

        if not is_monster_name_candidate(candidate):
            continue

        names.append(candidate)
    return names


def validate_monster_names(names: list[str]) -> None:
    if len(names) < 5000:
        raise ValueError(f"Parsed monster count looks incorrect: {len(names)}")

    unique_names = len(set(names))
    if unique_names < len(names) * 0.85:
        raise ValueError(f"Parsed monster names contain too many duplicates: {unique_names}/{len(names)} unique")


def create_default_skin_kinds() -> list[dict]:
    return [{"ID": 1, "LifeTime": 0, "Type": 2}]


def cleanup_pet_skin_field(value: str) -> str:
    return value.replace("\x00", "").replace("\t", "").replace("\f", "").replace("\v", "").strip()


def looks_like_go_path(value: str) -> bool:
    return bool(value) and (
        value.lower().startswith("app/")
        or value.lower().startswith("ui/")
        or "/" in value
    )


def looks_like_go_type(value: str) -> bool:
    lower = value.lower()
    return lower in {"module", "panel", "window"}


def infer_skin_type(go: str, go_type: str) -> int:
    return 3 if not go and not go_type else 0


def find_next_crlf(data: bytes, start: int) -> int:
    return data.find(b"\r\n", start)


def read_utf8_line(data: bytes, offset: int) -> tuple[str, int] | None:
    if offset >= len(data):
        return None

    line_end = find_next_crlf(data, offset)
    if line_end < 0:
        return None

    return data[offset:line_end].decode("utf-8"), line_end + 2


def peek_utf8_line(data: bytes, offset: int) -> str | None:
    line_end = find_next_crlf(data, offset)
    if line_end < 0:
        return None
    return data[offset:line_end].decode("utf-8")


def read_uint16_le(data: bytes, offset: int) -> int:
    return data[offset] | (data[offset + 1] << 8)


def read_int32_le(data: bytes, offset: int) -> int:
    return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24)


def try_read_utf8_sized_string(data: bytes, offset: int, byte_length: int | None = None) -> tuple[str, int] | None:
    current_offset = offset
    if byte_length is None:
        if current_offset + 2 > len(data):
            return None
        byte_length = read_uint16_le(data, current_offset)
        current_offset += 2

    if byte_length < 0 or current_offset + byte_length > len(data):
        return None

    value = data[current_offset : current_offset + byte_length].decode("utf-8") if byte_length > 0 else ""
    return value, current_offset + byte_length


def looks_like_binary_pet_skin_data(data: bytes) -> bool:
    return data is not None and len(data) > 32 and data[10:14] == b"\x01\x00\x00\x00"


def try_read_pet_skin_text_record(data: bytes, offset: int, skin_id: int) -> tuple[dict, int] | None:
    record_start = offset
    if offset + 4 > len(data):
        return None

    mon_id = data[offset] | (data[offset + 1] << 8)
    if data[offset + 2 : offset + 4] != b"\r\n":
        return None

    offset += 4
    line_result = read_utf8_line(data, offset)
    if line_result is None:
        return None

    raw_name, offset = line_result
    name = cleanup_pet_skin_field(raw_name)
    if not name:
        return None

    go = ""
    go_type = ""
    next_line = peek_utf8_line(data, offset)
    if next_line is not None:
        cleaned_line = cleanup_pet_skin_field(next_line)
        if looks_like_go_path(cleaned_line):
            go_result = read_utf8_line(data, offset)
            if go_result is None:
                return None
            go, offset = go_result
            go = cleanup_pet_skin_field(go)

            maybe_go_type = peek_utf8_line(data, offset)
            if maybe_go_type is not None:
                cleaned_type = cleanup_pet_skin_field(maybe_go_type)
                if looks_like_go_type(cleaned_type):
                    go_type_result = read_utf8_line(data, offset)
                    if go_type_result is None:
                        return None
                    go_type, offset = go_type_result
                    go_type = cleanup_pet_skin_field(go_type)

    return (
        {
            "ID": skin_id,
            "MonID": mon_id,
            "Name": name,
            "Type": infer_skin_type(go, go_type),
            "SkinKind": create_default_skin_kinds(),
        },
        offset if offset > record_start else record_start,
    )


def try_read_pet_skin_binary_record(data: bytes, offset: int) -> tuple[dict, int] | None:
    record_start = offset
    if offset + 14 > len(data):
        return None

    skin_id = read_int32_le(data, offset)
    mon_id = read_int32_le(data, offset + 4)
    name_length = read_uint16_le(data, offset + 8)
    if skin_id <= 0 or mon_id <= 0 or name_length <= 0:
        return None

    offset += 10
    name_result = try_read_utf8_sized_string(data, offset, name_length)
    if name_result is None:
        return None

    raw_name, offset = name_result
    name = cleanup_pet_skin_field(raw_name)
    if not name:
        return None

    if offset + 4 > len(data):
        return None

    skin_type = read_int32_le(data, offset)
    offset += 4

    go_result = try_read_utf8_sized_string(data, offset)
    if go_result is None:
        return None
    _, offset = go_result

    go_type_result = try_read_utf8_sized_string(data, offset)
    if go_type_result is None:
        return None
    _, offset = go_type_result

    return (
        {
            "ID": skin_id,
            "MonID": mon_id,
            "Name": name,
            "Type": skin_type,
            "SkinKind": create_default_skin_kinds(),
        },
        offset if offset > record_start else record_start,
    )


def parse_pet_skin_records(data: bytes) -> list[dict]:
    if looks_like_binary_pet_skin_data(data):
        skins: list[dict] = []
        offset = 10
        while offset + 14 <= len(data):
            parsed = try_read_pet_skin_binary_record(data, offset)
            if parsed is None:
                break
            skin, offset = parsed
            skins.append(skin)
        return skins

    skins = []
    offset = 0
    next_skin_id = 1
    while offset + 4 <= len(data):
        parsed = try_read_pet_skin_text_record(data, offset, next_skin_id)
        if parsed is None:
            offset += 1
            continue

        skin, offset = parsed
        skins.append(skin)
        next_skin_id += 1
    return skins


def validate_pet_skin_count(source_label: str, count: int) -> None:
    if count <= 0:
        raise ValueError("No pet skin entries were parsed.")
    if source_label in {"local-pet_skin.txt", "config-bundle-pet_skin.bytes"} and count < 500:
        raise ValueError(f"{source_label} parse looks incomplete: {count} entries.")


def create_empty_pet_skins_root() -> dict:
    return {"PetSkins": {"Skin": []}}


def count_existing_pet_skins(path: Path) -> int:
    if not path.is_file():
        return 0
    try:
        return path.read_text(encoding="utf-8").count('"MonID"')
    except Exception:
        return 0


def export_config(source_root: Path, output_root: Path, layout: str, preserve_existing_pet_skin: bool) -> dict:
    monsters_path, pet_skin_path = build_output_paths(output_root, layout)
    monsters_path.parent.mkdir(parents=True, exist_ok=True)
    pet_skin_path.parent.mkdir(parents=True, exist_ok=True)

    monsters_bytes, monsters_source = read_monsters_bytes(source_root)
    monster_names = extract_monster_names(monsters_bytes)
    validate_monster_names(monster_names)

    monsters_root = {
        "Monsters": {
            "Monster": [{"ID": index + 1, "DefName": name} for index, name in enumerate(monster_names)]
        }
    }

    monsters_path.write_text(json.dumps(monsters_root, ensure_ascii=False, indent=2), encoding="utf-8")

    pet_skin_error = ""
    pet_skin_source = ""
    pet_skin_count = 0
    try:
        pet_skin_bytes, pet_skin_source = read_pet_skin_bytes(source_root)
        skins = parse_pet_skin_records(pet_skin_bytes)
        validate_pet_skin_count(pet_skin_source, len(skins))
        pet_skins_root = {"PetSkins": {"Skin": skins}}
        pet_skin_path.write_text(json.dumps(pet_skins_root, ensure_ascii=False, indent=2), encoding="utf-8")
        pet_skin_count = len(skins)
    except Exception as ex:
        pet_skin_error = str(ex)
        if preserve_existing_pet_skin and pet_skin_path.is_file():
            pet_skin_source = "preserved-existing"
            pet_skin_count = count_existing_pet_skins(pet_skin_path)
        else:
            pet_skin_source = "generated-empty"
            pet_skin_path.write_text(json.dumps(create_empty_pet_skins_root(), ensure_ascii=False, indent=2), encoding="utf-8")

    return {
        "Success": True,
        "SourceRoot": str(source_root),
        "OutputRoot": str(output_root),
        "Layout": layout,
        "ConfigVersion": package_version(source_root, "ConfigPackage"),
        "DefaultManifestVersion": package_version(source_root, "DefaultPackage"),
        "MonsterCount": len(monster_names),
        "MonsterSource": monsters_source,
        "PetSkinCount": pet_skin_count,
        "PetSkinSource": pet_skin_source,
        "PetSkinError": pet_skin_error,
        "MonstersJsonPath": str(monsters_path),
        "PetSkinJsonPath": str(pet_skin_path),
        "Error": "",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export Seer config JSON without using the Unity Editor.")
    parser.add_argument("--source-root", dest="source_root")
    parser.add_argument("--install-root", dest="install_root")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--layout", choices=["mirror", "cache"], default="mirror")
    parser.add_argument("--no-preserve-existing-pet-skin", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source_root_value = args.source_root or args.install_root
    if not source_root_value:
        print(json.dumps({"Success": False, "Error": "Missing --source-root/--install-root"}))
        return 2

    source_root = normalize_path(source_root_value)
    output_root = normalize_path(args.output_dir)
    if not looks_like_source_root(source_root):
        print(json.dumps({"Success": False, "Error": f"Invalid Seer source root: {source_root}"}))
        return 1

    try:
        result = export_config(
            source_root=source_root,
            output_root=output_root,
            layout=args.layout,
            preserve_existing_pet_skin=not args.no_preserve_existing_pet_skin,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except Exception as ex:
        print(json.dumps({"Success": False, "Error": str(ex)}, ensure_ascii=False, indent=2))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
