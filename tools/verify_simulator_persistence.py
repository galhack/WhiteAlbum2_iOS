#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Run the debug app's write/read save probe in two simulator processes."""
import argparse
import configparser
import subprocess
import time
from pathlib import Path


def simctl(*arguments):
    return subprocess.check_output(["xcrun", "simctl", *arguments], text=True).strip()


def run(device, folder, language, script, timeout):
    bundle = "org.local.wa2.apple"
    documents = Path(simctl("get_app_container", device, bundle, "data")) / "Documents"
    resources = documents / folder
    if not resources.is_dir():
        raise FileNotFoundError("Import the resource folder first: " + str(resources))
    result_path = documents / "port-save-result.cfg"
    process_ids = []
    for phase in ("write", "read"):
        output = simctl("launch", "--terminate-running-process", device, bundle, "--",
            "--resources", str(resources), "--language", language, "--play", "--start-script", script,
            "--verify-save", phase)
        process = int(output.rsplit(":", 1)[1].strip())
        process_ids.append(process)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            result = configparser.ConfigParser()
            if result_path.exists():
                result.read(result_path)
                if result.has_section("test") and result.getint("test", "process", fallback=-1) == process:
                    status = result.get("test", "status").strip('"')
                    if status != ("written" if phase == "write" else "passed"):
                        raise RuntimeError(f"{phase}: {status}: {result.get('test', 'message')}")
                    print(f"{device} {phase}: {status}, process {process}", flush=True)
                    break
            time.sleep(1)
        else:
            raise TimeoutError(f"{phase} did not produce a result within {timeout}s")
    if process_ids[0] == process_ids[1]:
        raise RuntimeError("Save was not loaded in a different process")
    if (documents / "port-save-expectation.cfg").exists():
        raise RuntimeError("Probe expectation was not cleaned up")
    print("Cross-process save persistence passed.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("device", help="Dedicated simulator UUID")
    parser.add_argument("--folder", choices=("Wa2Res", "Wa2SC"), default="Wa2SC")
    parser.add_argument("--language", default="zh-Hans")
    parser.add_argument("--script", default="4000")
    parser.add_argument("--timeout", type=int, default=330)
    args = parser.parse_args()
    run(args.device, args.folder, args.language, args.script, args.timeout)
