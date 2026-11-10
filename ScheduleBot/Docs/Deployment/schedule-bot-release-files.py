"""Compare a publish manifest with the installed release and stage its replacement."""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile


def relative_path(name):
    path = PurePosixPath(name)
    if not path.parts or path.is_absolute() or ".." in path.parts or path.as_posix() != name:
        raise ValueError(f"Invalid publish path: {name!r}")
    return name


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def preserved_setting(live, name):
    return (
        "/" not in name
        and name.startswith("appsettings")
        and name.endswith(".json")
        and (live / name).is_file()
    )


def plan(live, files):
    needed = []
    for name, expected in files.items():
        installed = live / name
        if preserved_setting(live, name):
            continue
        if not installed.is_file() or digest(installed) != expected:
            needed.append(name)
    print(json.dumps({"Files": needed}))


def assemble(stage, live, files, directories):
    incoming = stage / "incoming"
    incoming.mkdir()
    for name in directories:
        (incoming / name).mkdir(parents=True, exist_ok=True)

    uploaded = set()
    delta = stage / "delta.tar.gz"
    if delta.exists():
        with tarfile.open(delta, "r:gz") as archive:
            for member in archive:
                name = relative_path(member.name)
                if not member.isfile() or name not in files or name in uploaded:
                    raise ValueError(f"Unexpected delta archive member: {name!r}")
                destination = incoming / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                with archive.extractfile(member) as source, destination.open("xb") as target:
                    shutil.copyfileobj(source, target)
                uploaded.add(name)

    reused = 0
    for name, expected in files.items():
        destination = incoming / name
        if name not in uploaded:
            if preserved_setting(live, name):
                continue
            installed = live / name
            if not installed.is_file():
                raise ValueError(f"Installed file disappeared after comparison: {name}. Run deploy again.")
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(installed, destination)
            reused += 1
        if digest(destination) != expected:
            raise ValueError(f"SHA-256 mismatch for {name}. Run deploy again to refresh the transfer plan.")
    print(f"Verified {len(uploaded)} uploaded and {reused} reused publish files.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("plan", "assemble"))
    parser.add_argument("stage", type=Path)
    parser.add_argument("live", type=Path)
    args = parser.parse_args()
    try:
        manifest = json.loads((args.stage / "publish.json").read_text(encoding="utf-8"))
        files = {relative_path(entry["Path"]): entry["Sha256"] for entry in manifest["Files"]}
        directories = [relative_path(name) for name in manifest["Directories"]]
        if args.action == "plan":
            plan(args.live, files)
        else:
            assemble(args.stage, args.live, files, directories)
    except (OSError, ValueError, tarfile.TarError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
