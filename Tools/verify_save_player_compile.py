#!/usr/bin/env python3
"""Compile the real save sources with and without UNITY_EDITOR defines.

Prerequisite: import/compile this worktree in Unity with the Android target first.
This focused gate omits analyzers/source generators and is NOT a player build.
Usage: python3 Tools/verify_save_player_compile.py --editor /path/to/Editor \
    --output /absolute/path/to/evidence
"""
import argparse
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    candidates = list((project / 'Library/Bee/artifacts').glob('**/QuackUp.Save.rsp'))
    if not candidates:
        parser.error('No generated QuackUp.Save.rsp. Compile Android-target Editor first.')
    response = max(candidates, key=lambda p: p.stat().st_mtime)
    source_lines = response.read_text().splitlines()
    if not any('UNITY_EDITOR' in line and line.startswith('-define:') for line in source_lines):
        parser.error('Expected an Editor response file for the control compilation.')
    if not any('TestMessagePackSaveObject.cs' in line for line in source_lines):
        parser.error('The concrete save subclass must be included in the compile gate.')
    dotnet = args.editor / 'Data/NetCoreRuntime/dotnet'
    compiler = args.editor / 'Data/DotNetSdkRoslyn/csc.dll'
    if not dotnet.is_file() or not compiler.is_file():
        parser.error('Editor must contain Data/NetCoreRuntime/dotnet and DotNetSdkRoslyn/csc.dll.')
    for mode in ('editor', 'player'):
        lines = []
        for line in source_lines:
            if re.match(r'^[-/](out|refout|analyzer|additionalfile):', line, re.I):
                continue
            if mode == 'player' and line.startswith('-define:'):
                symbols = line[len('-define:'):].split(';')
                symbols = [s for s in symbols if not s.startswith('UNITY_EDITOR')]
                if not symbols:
                    continue
                line = '-define:' + ';'.join(symbols)
            lines.append(line)
        lines.append('-out:"' + str(output / ('save-' + mode + '.dll')) + '"')
        rsp = output / ('save-' + mode + '.rsp')
        rsp.write_text('\n'.join(lines) + '\n')
        result = subprocess.run([str(dotnet), str(compiler), '@' + str(rsp)],
                                cwd=project, capture_output=True, text=True)
        log = result.stdout + result.stderr
        (output / ('save-' + mode + '.log')).write_text(log)
        print(f'{mode} control/probe: exit {result.returncode}')
        if log:
            print(log)
        if result.returncode:
            return result.returncode
    print('PASS: real save sources compile in both configurations; player build still required.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
