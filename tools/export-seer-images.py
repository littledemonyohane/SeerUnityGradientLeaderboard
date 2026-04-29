import argparse
import json
import re
import shutil
import sys
from collections import defaultdict
from pathlib import Path

import UnityPy


HASH_REGEX = re.compile(r"\b[0-9a-f]{32}\b")
PNG_SUFFIX = ".png"
PET_HEAD_PREFIX = "Assets/Art/Ui/assets/pet/head/"
COUNTERMARK_PREFIX = "Assets/Art/Ui/assets/countermark/icon/"


def normalize_path(path_value: str) -> Path:
    return Path(path_value).expanduser().resolve()


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


def looks_like_source_root(source_root: Path) -> bool:
    return (
        package_version_path(source_root, "ConfigPackage").is_file()
        and package_version_path(source_root, "DefaultPackage").is_file()
    )


def read_default_manifest_index(source_root: Path) -> tuple[bytes, str, list[str]]:
    manifest_path = package_manifest_bytes_path(source_root, "DefaultPackage")
    if not manifest_path.is_file():
        raise FileNotFoundError(f"DefaultPackage manifest not found under {source_root}")

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


def index_asset_paths(manifest_bytes: bytes, manifest_text: str, ordered_hashes: list[str], prefix: str) -> dict[str, str]:
    indexed: dict[str, str] = {}
    search_start = 0
    while 0 <= search_start < len(manifest_text):
        start = manifest_text.find(prefix, search_start)
        if start < 0:
            break

        end = manifest_text.find(PNG_SUFFIX, start)
        if end < 0:
            break

        end += len(PNG_SUFFIX)
        if end + 1 >= len(manifest_bytes):
            break

        asset_path = manifest_text[start:end]
        bundle_index = manifest_bytes[end] + (manifest_bytes[end + 1] * 256)
        if 0 <= bundle_index < len(ordered_hashes):
            indexed[asset_path] = ordered_hashes[bundle_index]

        search_start = end

    return indexed


def asset_id_from_path(asset_path: str, prefix: str) -> str:
    return asset_path[len(prefix) : -len(PNG_SUFFIX)]


def asset_sort_key(asset_path: str, prefix: str):
    asset_id = asset_id_from_path(asset_path, prefix)
    if asset_id.isdigit():
        return 0, int(asset_id), asset_id
    return 1, asset_id.lower(), asset_id


def normalize_requested_ids(values: list[str] | None) -> list[str]:
    if not values:
        return []

    normalized: list[str] = []
    for value in values:
        for part in value.split(","):
            cleaned = part.strip()
            if cleaned:
                normalized.append(cleaned)
    return normalized


def select_asset_paths(
    indexed: dict[str, str],
    prefix: str,
    limit: int,
    requested_ids: list[str] | None = None,
) -> tuple[list[str], list[str]]:
    normalized_ids = normalize_requested_ids(requested_ids)
    if normalized_ids:
        selected: list[str] = []
        missing_ids: list[str] = []
        seen_paths: set[str] = set()
        for asset_id in normalized_ids:
            asset_path = f"{prefix}{asset_id}{PNG_SUFFIX}"
            if asset_path in indexed:
                if asset_path not in seen_paths:
                    seen_paths.add(asset_path)
                    selected.append(asset_path)
            else:
                missing_ids.append(asset_id)

        if limit > 0:
            selected = selected[:limit]
        return selected, missing_ids

    ordered = sorted(indexed.keys(), key=lambda asset_path: asset_sort_key(asset_path, prefix))
    if limit > 0:
        return ordered[:limit], []
    return ordered, []


def collect_container_objects(env) -> dict[str, list]:
    by_path: dict[str, list] = defaultdict(list)
    for container_path, obj in env.container.items():
        by_path[container_path.lower()].append(obj)
    return by_path


def pick_best_container_object(objects: list):
    for preferred_type in ("Texture2D", "Sprite"):
        for obj in objects:
            if obj.type.name == preferred_type:
                return obj
    return objects[0] if objects else None


def save_container_object_image(obj, output_path: Path) -> None:
    asset = obj.read()
    image = getattr(asset, "image", None)
    if image is None:
        raise RuntimeError(f"Unsupported asset type for image export: {obj.type.name}")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path)


def build_category_output_dir(output_root: Path, prefix: str, layout: str) -> Path:
    if layout == "cache":
        return output_root
    if prefix == PET_HEAD_PREFIX:
        return output_root / "newseer" / "assets" / "art" / "ui" / "assets" / "pet" / "head"
    if prefix == COUNTERMARK_PREFIX:
        return output_root / "newseer" / "assets" / "art" / "ui" / "assets" / "countermark" / "icon"
    raise ValueError(f"Unexpected asset prefix: {prefix}")


def build_output_path(output_root: Path, prefix: str, asset_id: str, layout: str) -> Path:
    if layout == "cache":
        if prefix == PET_HEAD_PREFIX:
            return output_root / f"{asset_id}.png"
        if prefix == COUNTERMARK_PREFIX:
            return output_root / f"countermark_{asset_id}.png"
        raise ValueError(f"Unexpected asset prefix: {prefix}")

    return build_category_output_dir(output_root, prefix, layout) / f"{asset_id}.png"


def export_category(
    source_root: Path,
    output_root: Path,
    manifest_bytes: bytes,
    manifest_text: str,
    ordered_hashes: list[str],
    prefix: str,
    package_name: str,
    limit: int,
    requested_ids: list[str] | None,
    layout: str,
    verbose: bool,
) -> dict:
    indexed = index_asset_paths(manifest_bytes, manifest_text, ordered_hashes, prefix)
    selected_asset_paths, missing_requested_ids = select_asset_paths(indexed, prefix, limit, requested_ids)
    output_dir = build_category_output_dir(output_root, prefix, layout)

    if layout == "mirror" and output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    grouped: dict[str, list[str]] = defaultdict(list)
    for asset_path in selected_asset_paths:
        grouped[indexed[asset_path]].append(asset_path)

    exported = 0
    failed = 0
    sample_failures: list[str] = []

    for bundle_hash in sorted(grouped.keys()):
        bundle_path = bundle_data_path(source_root, package_name, bundle_hash)
        if not bundle_path.is_file():
            for asset_path in grouped[bundle_hash]:
                failed += 1
                if len(sample_failures) < 10:
                    sample_failures.append(f"{asset_path}: missing bundle {bundle_hash}")
            continue

        if verbose:
            print(f"[bundle] {bundle_hash} <- {bundle_path}", file=sys.stderr)

        env = UnityPy.load(str(bundle_path))
        container_objects = collect_container_objects(env)

        for asset_path in grouped[bundle_hash]:
            objects = container_objects.get(asset_path.lower(), [])
            obj = pick_best_container_object(objects)
            if obj is None:
                failed += 1
                if len(sample_failures) < 10:
                    sample_failures.append(f"{asset_path}: asset missing inside bundle {bundle_hash}")
                continue

            asset_id = asset_id_from_path(asset_path, prefix)
            output_path = build_output_path(output_root, prefix, asset_id, layout)
            try:
                save_container_object_image(obj, output_path)
                exported += 1
            except Exception as ex:  # noqa: BLE001
                failed += 1
                if len(sample_failures) < 10:
                    sample_failures.append(f"{asset_path}: {ex}")

    return {
        "Indexed": len(indexed),
        "Attempted": len(selected_asset_paths),
        "Exported": exported,
        "Failed": failed,
        "RequestedIds": normalize_requested_ids(requested_ids),
        "MissingRequestedIds": missing_requested_ids,
        "OutputDirectory": str(output_dir),
        "SampleFailures": sample_failures,
        "Source": "unitypy",
        "Layout": layout,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export Seer Unity head/countermark images directly from local bundles.")
    parser.add_argument("--source-root", dest="source_root")
    parser.add_argument("--install-root", dest="install_root")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--layout", choices=["mirror", "cache"], default="mirror")
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--head-ids", nargs="*")
    parser.add_argument("--countermark-ids", nargs="*")
    parser.add_argument("--skip-heads", action="store_true")
    parser.add_argument("--skip-countermarks", action="store_true")
    parser.add_argument("--verbose", action="store_true")
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
        manifest_bytes, manifest_text, ordered_hashes = read_default_manifest_index(source_root)
    except Exception as ex:  # noqa: BLE001
        print(json.dumps({"Success": False, "Error": str(ex)}))
        return 1

    result = {
        "Success": True,
        "SourceRoot": str(source_root),
        "OutputRoot": str(output_root),
        "DefaultPackageVersion": package_version(source_root, "DefaultPackage"),
        "Heads": {
            "Indexed": 0,
            "Attempted": 0,
            "Exported": 0,
            "Failed": 0,
            "RequestedIds": [],
            "MissingRequestedIds": [],
            "OutputDirectory": str(build_category_output_dir(output_root, PET_HEAD_PREFIX, args.layout)),
            "SampleFailures": [],
            "Source": "unitypy",
            "Layout": args.layout,
        },
        "Countermarks": {
            "Indexed": 0,
            "Attempted": 0,
            "Exported": 0,
            "Failed": 0,
            "RequestedIds": [],
            "MissingRequestedIds": [],
            "OutputDirectory": str(build_category_output_dir(output_root, COUNTERMARK_PREFIX, args.layout)),
            "SampleFailures": [],
            "Source": "unitypy",
            "Layout": args.layout,
        },
        "Error": "",
    }

    try:
        if not args.skip_heads:
            result["Heads"] = export_category(
                source_root=source_root,
                output_root=output_root,
                manifest_bytes=manifest_bytes,
                manifest_text=manifest_text,
                ordered_hashes=ordered_hashes,
                prefix=PET_HEAD_PREFIX,
                package_name="DefaultPackage",
                limit=args.limit,
                requested_ids=args.head_ids,
                layout=args.layout,
                verbose=args.verbose,
            )

        if not args.skip_countermarks:
            result["Countermarks"] = export_category(
                source_root=source_root,
                output_root=output_root,
                manifest_bytes=manifest_bytes,
                manifest_text=manifest_text,
                ordered_hashes=ordered_hashes,
                prefix=COUNTERMARK_PREFIX,
                package_name="DefaultPackage",
                limit=args.limit,
                requested_ids=args.countermark_ids,
                layout=args.layout,
                verbose=args.verbose,
            )
    except Exception as ex:  # noqa: BLE001
        result["Success"] = False
        result["Error"] = str(ex)

    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["Success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
