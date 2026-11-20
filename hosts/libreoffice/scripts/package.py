"""Packages the LibreOffice extension: publishes the engine (dotnet publish of hosts/libreoffice/engine) and zips it
with the contents of hosts/libreoffice/oxt into an .oxt. Runs with any Python 3.8+ on Windows, Linux and macOS.
Ported from the Slidemax LibreOffice host (scripts/package.py), translated and reduced.

  python hosts/libreoffice/scripts/package.py [--rid portable] [--rid win-x64 ...] [--out dist]
      [--version <x.y.z>] [--dotnet <path>] [--configuration Release]

--rid portable (default): engine as a framework-dependent .dll that runs on every platform with the .NET 10 runtime
  installed; one package for all platforms: dist/InventaSketch-LibreOffice-<version>.oxt
--rid <runtime id> (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64): self-contained single-file
  engine (no .NET needed), one package per platform: dist/InventaSketch-LibreOffice-<version>-<rid>.oxt
The version defaults to <Version> in Directory.Build.props.
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
LO = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(LO))
ENGINE_NAME = "InventaSketch.LibreOffice.Engine"

PLATFORMS = {
    "portable": "all",
    "win-x64": "windows_x86_64",
    "win-arm64": "windows_aarch64",
    "linux-x64": "linux_x86_64",
    "linux-arm64": "linux_aarch64",
    # LibreOffice calls macOS "macosx" (Slidemax finding: "macos_..." is rejected as a foreign platform).
    "osx-x64": "macosx_x86_64",
    "osx-arm64": "macosx_aarch64",
}


def props_version():
    with open(os.path.join(ROOT, "Directory.Build.props"), encoding="utf-8") as f:
        found = re.search(r"<Version>([^<]+)</Version>", f.read())
    if not found:
        raise SystemExit("No <Version> in Directory.Build.props.")
    return found.group(1).strip()


def find_dotnet(given):
    if given:
        return given
    sys.path.insert(0, os.path.join(LO, "oxt", "pythonpath"))
    from inventasketch_lo.engine import dotnet_candidates
    searched = dotnet_candidates()
    for path in searched:
        if not os.path.isfile(path):
            continue
        try:
            sdks = subprocess.run([path, "--list-sdks"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                  timeout=30).stdout.decode("utf-8", "replace")
        except Exception:
            continue
        if any(int(m) >= 10 for m in re.findall(r"^(\d+)\.", sdks, re.M)):
            return path
    raise SystemExit(".NET 10 SDK not found (searched %s); pass --dotnet." % ", ".join(searched))


def publish_engine(dotnet, rid, target, configuration, version):
    cmd = [dotnet, "publish", os.path.join(LO, "engine", ENGINE_NAME + ".csproj"), "-c", configuration, "-o", target,
           "--nologo", "-p:Version=" + version]
    cmd += ["-p:UseAppHost=false"] if rid == "portable" else ["-r", rid]
    print(" ".join(cmd), flush=True)
    subprocess.run(cmd, check=True)
    for name in os.listdir(target):
        if name.endswith((".pdb", ".xml")):
            os.remove(os.path.join(target, name))


def dotnet_notice_files(dotnet):
    """LICENSE.txt and ThirdPartyNotices.txt of the .NET installation (shipped inside self-contained engines)."""
    base = os.path.dirname(os.path.realpath(shutil.which(dotnet) or dotnet))
    found = []
    for name in ("LICENSE.txt", "ThirdPartyNotices.txt"):
        source = os.path.join(base, name)
        if os.path.isfile(source):
            found.append(("dotnet-" + name, source))
    if len(found) < 2:
        raise SystemExit("LICENSE.txt/ThirdPartyNotices.txt of .NET not found in %s." % base)
    return found


def stage(rid, version, engine_dir, work, dotnet):
    folder = os.path.join(work, "oxt-" + rid)
    if os.path.isdir(folder):
        shutil.rmtree(folder)
    shutil.copytree(os.path.join(LO, "oxt"), folder, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    path = os.path.join(folder, "description.xml")
    with open(path, encoding="utf-8") as f:
        desc = f.read()
    desc = re.sub(r'<version value="[^"]*"/>', '<version value="%s"/>' % version, desc)
    desc = re.sub(r'<platform value="[^"]*"/>', '<platform value="%s"/>' % PLATFORMS[rid], desc)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(desc)
    path = os.path.join(folder, "pythonpath", "inventasketch_lo", "__init__.py")
    with open(path, encoding="utf-8") as f:
        init = f.read()
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(re.sub(r'VERSION = "[^"]*"', 'VERSION = "%s"' % version, init))
    shutil.copy2(os.path.join(ROOT, "LICENSE"), os.path.join(folder, "LICENSE"))
    shutil.copy2(os.path.join(ROOT, "THIRD-PARTY-NOTICES.md"), os.path.join(folder, "THIRD-PARTY-NOTICES.md"))
    if rid != "portable":
        licences = os.path.join(folder, "licenses")
        os.makedirs(licences)
        for name, source in dotnet_notice_files(dotnet):
            shutil.copy2(source, os.path.join(licences, name))
    shutil.copytree(engine_dir, os.path.join(folder, "engine", rid))
    return folder


def zip_folder(folder, target):
    if os.path.exists(target):
        os.remove(target)
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for base, dirs, files in os.walk(folder):
            dirs.sort()
            for name in sorted(files):
                path = os.path.join(base, name)
                info = zipfile.ZipInfo.from_file(path, os.path.relpath(path, folder).replace(os.sep, "/"))
                if name == ENGINE_NAME:  # self-contained engine on Linux/macOS: executable
                    info.external_attr = 0o100755 << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                with open(path, "rb") as f:
                    z.writestr(info, f.read())


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--version", default=None)
    ap.add_argument("--out", default=os.path.join(ROOT, "dist"))
    ap.add_argument("--rid", action="append", choices=sorted(PLATFORMS))
    ap.add_argument("--dotnet", default=None)
    ap.add_argument("--configuration", default="Release")
    args = ap.parse_args(argv)
    version = args.version or props_version()
    dotnet = find_dotnet(args.dotnet)
    work = os.path.join(LO, "build")
    os.makedirs(args.out, exist_ok=True)
    made = []
    for rid in args.rid or ["portable"]:
        engine_dir = os.path.join(work, "engine-" + rid)
        if os.path.isdir(engine_dir):
            shutil.rmtree(engine_dir)
        publish_engine(dotnet, rid, engine_dir, args.configuration, version)
        folder = stage(rid, version, engine_dir, work, dotnet)
        name = "InventaSketch-LibreOffice-%s%s.oxt" % (version, "" if rid == "portable" else "-" + rid)
        target = os.path.join(os.path.abspath(args.out), name)
        zip_folder(folder, target)
        print("Package: %s (%.1f MB)" % (target, os.path.getsize(target) / 1e6), flush=True)
        made.append(target)
    return made


if __name__ == "__main__":
    main()
    sys.exit(0)
