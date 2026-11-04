#!/usr/bin/env python3
"""Build a UPM tarball from the runtime directory, never the host project's Game code."""
import argparse
import json
from pathlib import Path
import tarfile


def main():
    repository = Path(__file__).resolve().parents[2]
    source = repository / "Assets" / "Scripts" / "Ability"
    manifest = json.loads((source / "package.json").read_text(encoding="utf-8"))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path.cwd() / (manifest["name"] + "-" + manifest["version"] + ".tgz"))
    args = parser.parse_args()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    files = sorted(path for path in source.rglob("*") if path.is_file() and not any(part.startswith(".") or part in {"bin", "obj"} for part in path.relative_to(source).parts))
    if output == source or source in output.parents:
        raise SystemExit("Output must be outside the package source directory.")
    for path in source.rglob("*"):
        if path.suffix != ".meta" and not Path(str(path) + ".meta").is_file():
            raise SystemExit("Unity package asset is missing stable metadata: " + str(path))
    with tarfile.open(output, "w:gz", format=tarfile.PAX_FORMAT) as archive:
        for path in files:
            if path.is_symlink():
                raise SystemExit("Package symlinks are not supported: " + str(path))
            info = archive.gettarinfo(str(path), arcname="package/" + path.relative_to(source).as_posix())
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            with path.open("rb") as stream:
                archive.addfile(info, stream)
    print(str(output) + " (" + str(len(files)) + " files)")


if __name__ == "__main__":
    main()
