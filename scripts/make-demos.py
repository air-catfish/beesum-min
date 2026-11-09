#!/usr/bin/env python3
"""Render the README GIFs from real CLI runs.

Windows-oriented on purpose: the released win-x64 build and Ollama on the host
are what the screenshots show. Requires artifacts/agg.exe.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
import textwrap
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CLI_DIR = ROOT / "artifacts" / "publish" / "win-x64"
AGG = ROOT / "artifacts" / "agg.exe"
OUT = ROOT / "docs" / "demos"
CASTS = ROOT / "artifacts" / "demos"

PROMPT = "PS E:\\beesum-min> "
MODEL = ["--provider", "ollama", "--ollama-model", "qwen3:1.7b"]


def run_demo(args: list[str], stdin: bytes | None = None) -> tuple[list[str], list[str]]:
    if args and args[0] == "beesum-min":
        args = [str(CLI_DIR / "beesum-min.exe"), *args[1:]]
    proc = subprocess.run(
        args,
        cwd=ROOT,
        input=stdin,
        capture_output=True,
        timeout=300,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr.decode("utf-8", "replace"))
        raise SystemExit(f"demo failed ({proc.returncode}): {' '.join(args)}")

    stdout = proc.stdout.decode("utf-8", "replace").splitlines()
    stderr = proc.stderr.decode("utf-8", "replace").splitlines()
    stages = [line for line in stderr if line.startswith("[")]
    return stages, stdout


def wrap(lines: list[str], width: int = 92) -> list[str]:
    wrapped: list[str] = []
    for line in lines:
        if not line:
            wrapped.append("")
            continue
        if line.startswith("  "):  # preserve JSON indentation
            wrapped.extend(textwrap.wrap(line, width, subsequent_indent="  ") or [""])
        else:
            wrapped.extend(textwrap.wrap(line, width, break_long_words=False) or [""])
    return wrapped


def cast_for(display: str, stages: list[str], output: list[str], width: int = 112, height: int = 38) -> list[str]:
    events: list[tuple[float, str]] = []
    t = 0.2

    def add(delay: float, text: str) -> None:
        nonlocal t
        t += delay
        events.append((round(t, 3), text))

    add(0.0, "\u001b[1;32m" + PROMPT + "\u001b[0m")
    for index, line in enumerate(display.split("\n")):
        if index > 0:
            add(0.12, "\r\n")
        for ch in line:
            add(0.018, ch)
    add(0.25, "\r\n")

    for stage in stages:
        add(0.12, "\u001b[2;36m" + stage + "\u001b[0m\r\n")

    for line in wrap(output, 104):
        add(0.04, line + "\r\n")

    add(3.2, "")

    header = {
        "version": 2,
        "width": width,
        "height": height,
        "timestamp": int(time.time()),
        "env": {"SHELL": "/bin/bash", "TERM": "xterm-256color"},
    }
    rows = [json.dumps(header)]
    rows.extend(json.dumps([when, "o", text], ensure_ascii=False) for when, text in events)
    return rows


def render(name: str, rows: list[str]) -> None:
    CASTS.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    cast = CASTS / f"{name}.cast"
    cast.write_text("\n".join(rows) + "\n", encoding="utf-8")

    gif = OUT / f"{name}.gif"
    proc = subprocess.run(
        [
            str(AGG),
            "--theme", "dracula",
            "--font-family", "Consolas",
            "--font-size", "16",
            "--line-height", "1.35",
            "--fps-cap", "15",
            "--last-frame-duration", "2",
            str(cast),
            str(gif),
        ],
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr)
        raise SystemExit(f"agg failed for {name}")
    print(f"{gif.relative_to(ROOT)}  {gif.stat().st_size / 1024:.0f} KiB")


def main() -> None:
    if not AGG.exists():
        raise SystemExit("artifacts/agg.exe is missing")
    if not (CLI_DIR / "beesum-min.exe").exists():
        raise SystemExit("publish the CLI first: dotnet publish ... -o artifacts/publish/win-x64")

    demos = [
        (
            "real-video",
            'beesum-min "https://www.youtube.com/watch?v=fNk_zzaMoSs" `\n'
            '  --proxy socks5://127.0.0.1:1080 --sub-langs en `\n'
            '  --provider ollama --ollama-model qwen3:1.7b `\n'
            '  --prompt-file examples\\prompts\\eli5.md',
            [
                "beesum-min",
                "https://www.youtube.com/watch?v=fNk_zzaMoSs",
                "--proxy", "socks5://127.0.0.1:1080",
                "--sub-langs", "en",
                *MODEL,
                "--prompt-file", "examples/prompts/eli5.md",
            ],
            None,
        ),
        (
            "quick-start",
            "beesum-min --local-vtt samples\\demo\\talk.vtt",
            ["beesum-min", "--local-vtt", "samples/demo/talk.vtt"],
            None,
        ),
        (
            "json",
            "beesum-min --local-vtt smoke.vtt --json",
            ["beesum-min", "--local-vtt", "smoke.vtt", "--json"],
            None,
        ),
        (
            "custom-prompt",
            "beesum-min --local-vtt samples\\demo\\talk.vtt `\n  --provider ollama --ollama-model qwen3:1.7b `\n  --prompt-file examples\\prompts\\tech-lead.md",
            ["beesum-min", "--local-vtt", "samples/demo/talk.vtt", *MODEL, "--prompt-file", "examples/prompts/tech-lead.md"],
            None,
        ),
        (
            "chunking",
            "beesum-min --local-vtt samples\\demo\\talk.vtt `\n  --provider ollama --ollama-model qwen3:1.7b `\n  --chunk-tokens 200",
            ["beesum-min", "--local-vtt", "samples/demo/talk.vtt", *MODEL, "--chunk-tokens", "200"],
            None,
        ),
        (
            "stdin",
            "Get-Content smoke.vtt | beesum-min --stdin --input vtt --json",
            ["beesum-min", "--stdin", "--input", "vtt", "--json"],
            (ROOT / "smoke.vtt").read_bytes(),
        ),
    ]

    for name, display, args, stdin in demos:
        print(f"running {name}...")
        stages, output = run_demo(args, stdin)
        render(name, cast_for(display, stages, output))


if __name__ == "__main__":
    main()
