#!/usr/bin/env python3
"""Build a standalone runtime ZIP or UPM tarball, excluding host project code."""
import argparse
import gzip
import json
from pathlib import Path
import tarfile
import zipfile


def main():
    repository = Path(__file__).resolve().parents[2]
    source = repository / "Assets" / "Scripts" / "Ability"
    manifest = json.loads((source / "package.json").read_text(encoding="utf-8"))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path.cwd() / (manifest["name"] + "-" + manifest["version"] + ".tgz"))
    parser.add_argument("--format", choices=("tgz", "zip"), help="Defaults to ZIP for a .zip output, otherwise UPM tarball.")
    args = parser.parse_args()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    if output == source or source in output.parents:
        raise SystemExit("Output must be outside the package source directory.")
    entries = sorted(path for path in source.rglob("*") if not any(part.startswith(".") or part in {"bin", "obj"} for part in path.relative_to(source).parts))
    for path in entries:
        if path.is_symlink():
            raise SystemExit("Package symlinks are not supported: " + str(path))
        if path.suffix != ".meta" and not Path(str(path) + ".meta").is_file():
            raise SystemExit("Unity package asset is missing stable metadata: " + str(path))
    files = [path for path in entries if path.is_file()]
    archive_format = args.format or ("zip" if output.suffix.lower() == ".zip" else "tgz")
    if archive_format == "zip":
        with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for path in files:
                info = zipfile.ZipInfo(manifest["name"] + "/" + path.relative_to(source).as_posix(), date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = 0o100644 << 16
                archive.writestr(info, path.read_bytes())
    else:
        with output.open("wb") as target, gzip.GzipFile(fileobj=target, mode="wb", filename="", mtime=0) as compressed, tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as archive:
            for path in files:
                info = archive.gettarinfo(str(path), arcname="package/" + path.relative_to(source).as_posix())
                info.uid = info.gid = info.mtime = 0
                info.uname = info.gname = ""
                with path.open("rb") as stream:
                    archive.addfile(info, stream)
    print(str(output) + " (" + str(len(files)) + " files)")


if __name__ == "__main__":
    main()
