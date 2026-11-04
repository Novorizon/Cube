#!/usr/bin/env python3
"""Build the project-independent Ability source and verification ZIP."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import zipfile


SOURCE_REPOSITORY = "https://github.com/Novorizon/Cube"
SOURCE_REVISION = "4fefa2038106734ba917f1340d4dbc14eb7c92c9"
SOURCE_DIRECTORIES = (
    "Assets/Scripts/Ability",
    "Assets/Tests/EditMode/Ability",
    "Data/AbilityJsonSources/Validation",
    "Tools/AbilityVerification/Core",
    "Tools/AbilityVerification/PackageSmoke",
    "Tools/AbilityVerification/Performance",
)


def checked_name(value):
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or not path.parts:
        raise ValueError("Unsafe archive path: " + value)
    return path.as_posix()


def collect_directory(repository, relative):
    source = repository / relative
    if not source.is_dir():
        raise SystemExit("Required source directory is missing: " + relative)
    entries = {}
    for path in sorted(source.rglob("*")):
        parts = path.relative_to(source).parts
        if any(part.startswith(".") or part in {"bin", "obj", "__pycache__"} for part in parts):
            continue
        if path.is_symlink():
            raise SystemExit("Bundle symlinks are not supported: " + str(path))
        if path.is_file():
            entries[checked_name(path.relative_to(repository).as_posix())] = path.read_bytes()
    return entries


def readme(version):
    return f"""# Ability Runtime {version}：独立源码与验证工程

本目录只包含技能包、核心测试、JSON 样例及便携验证工具。没有 Cube 的 Framework、塔防业务、项目 UI、资源服务或 Business 验证桩；技能包的生产源码只有一份，位于 `Assets/Scripts/Ability`。

## 安装技能包

在 Unity 2022.3 项目中打开 Package Manager，选择 **Add package from disk**，选中本目录中的 `Assets/Scripts/Ability/package.json`。也可以将整个 Ability 目录复制到项目的 `Packages/com.novorizon.ability`。请选择一种导入方式，避免同时从 Assets 和 Packages 编译两份同名程序集。

导入后，由业务自己的程序集实现 `IUnit`、`IWorld`、资源和表现接口，并调用公开 API。特殊技能脚本也放在业务程序集注册；不需要修改技能包。

本目录不是完整 Unity 项目：它没有 ProjectSettings、Packages/manifest.json 或具体场景。请先新建一个干净的 Unity 2022.3 项目进行导入验收。若要运行真实 EditMode 测试，将 `Assets/Tests/EditMode/Ability` 复制到该项目相同位置，将 `Data/AbilityJsonSources/Validation` 复制到项目根的相同位置，安装 Unity Test Framework，然后使用 Test Runner。不要把便携宿主中的 Unity/NUnit 替身复制到 Unity 的 Assets 中。

## 脱离 Cube 运行源码检查

安装 .NET 8 SDK；在本 README 所在目录执行：

```sh
dotnet run --project Tools/AbilityVerification/Core/AbilityTests.csproj
bash Tools/AbilityVerification/PackageSmoke/run.sh
```

第一条使用最小 Unity/NUnit 兼容层运行真实 EditMode fixture 源码；第二条仅复制 Ability 目录，在空目录编译独立的外部消费者。前一版本的检查规模为核心 223 项、独立包 7 项，实际结果以本次命令输出为准。可用 `DOTNET=/absolute/path/to/dotnet bash .../run.sh` 指定 SDK 路径。

这些检查不是 Unity Test Runner、Unity 编译、PlayMode、资源加载或 IL2CPP 验证。完整 Cube 中的 Business 检查有项目依赖，未收录在此独立工程。

## 性能复测

```sh
dotnet build Tools/AbilityVerification/Performance/AbilityPerformance.csproj -c Release
DOTNET_TieredCompilation=0 dotnet Tools/AbilityVerification/Performance/bin/Release/net8.0/AbilityPerformance.dll --label current --output Tools/AbilityVerification/Performance/results/current.json
```

`Tools/AbilityVerification/Performance/README.md` 说明场景、采样、GC、源码指纹、历史结果及其限制。历史 baseline/optimized JSON 是上游实现的记录，不是这次解压后的新测量；复测写入 current.json。必须在 Unity 的目标构建和设备上另测 Profiler CPU、GC、内存与长时间稳定性，不能把 .NET 合成时间直接当作游戏帧率。

## 全面验收

若包含 `Docs/AbilityValidation.md`，请按其中的架构、规则、时序、异常、性能和 Unity 交付门槛执行。JSON 技能样例与扩展边界见 `Data/AbilityJsonSources/Validation/README.md`；包的公开 API 说明见 `Assets/Scripts/Ability/README.md`。

源码来源与归档范围见 `SOURCE.md`，文件 SHA-256 校验见 `CHECKSUMS.sha256`。授权条件以归档中的原始 LICENSE 为准。
"""


def main():
    repository = Path(__file__).resolve().parents[2]
    manifest = json.loads((repository / "Assets/Scripts/Ability/package.json").read_text(encoding="utf-8"))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=repository.parent / ("AbilityRuntime-" + manifest["version"] + "-Validation.zip"))
    parser.add_argument("--license", type=Path, help="Original upstream license file, if the source snapshot omitted it.")
    parser.add_argument("--source-revision", default=SOURCE_REVISION, help="Upstream runtime source commit; does not assert unmodified verification docs.")
    args = parser.parse_args()
    output = args.output.resolve()
    if output == repository or repository in output.parents:
        raise SystemExit("Output must be outside the source repository.")

    license_path = args.license
    if license_path is None:
        license_path = next((repository / name for name in ("LICENSE", "LICENSE.md", "COPYING") if (repository / name).is_file()), None)
    if license_path is None or not license_path.is_file() or license_path.is_symlink():
        raise SystemExit("The original repository license is required; pass --license if this snapshot omitted it.")

    entries = {}
    for relative in SOURCE_DIRECTORIES:
        entries.update(collect_directory(repository, relative))
    for relative in ("Tools/AbilityVerification/build-package.py", "Tools/AbilityVerification/build-validation-bundle.py", "Docs/AbilityValidation.md"):
        path = repository / relative
        if path.is_symlink():
            raise SystemExit("Bundle symlinks are not supported: " + str(path))
        if path.is_file():
            entries[checked_name(relative)] = path.read_bytes()
    entries["LICENSE"] = license_path.read_bytes()
    entries["README.md"] = readme(manifest["version"]).encode("utf-8")
    entries["SOURCE.md"] = ("# Source and scope\n\n"
        + "- Original repository: " + SOURCE_REPOSITORY + "\n"
        + "- Runtime source revision: " + args.source_revision + "\n"
        + "- Package: " + manifest["name"] + " " + manifest["version"] + "\n"
        + "- Verification/bundling documentation may contain follow-up changes from the working tree.\n"
        + "- Source files are copied without rewriting paths or substituting production code.\n"
        + "- Excluded: Framework, Game/TowerDefense, Business verification, project assets and build outputs.\n"
        + "- Historical performance JSON files are upstream evidence, not a fresh benchmark of this archive.\n"
        + "- The original repository LICENSE is preserved verbatim.\n").encode("utf-8")
    entries["CHECKSUMS.sha256"] = "".join(hashlib.sha256(data).hexdigest() + "  " + name + "\n" for name, data in sorted(entries.items())).encode("utf-8")

    output.parent.mkdir(parents=True, exist_ok=True)
    prefix = "AbilityRuntime-" + manifest["version"] + "-Validation/"
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(entries.items()):
            info = zipfile.ZipInfo(checked_name(prefix + name), date_time=(1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            mode = 0o755 if name.endswith(".sh") else 0o644
            info.external_attr = (stat.S_IFREG | mode) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, data)
    print(str(output) + " (" + str(len(entries)) + " files)")


if __name__ == "__main__":
    main()
