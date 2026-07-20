#!/usr/bin/env bash
# Downloads python-build-standalone install_only archives into test/fixtures/ for the
# current platform (all platforms with --all). Archives are gitignored; tests point a
# DirectorySource at test/fixtures/.
set -euo pipefail

TAG="20260623"
VERSIONS=("3.12.13" "3.13.14")
TRIPLES_ALL=(
    "x86_64-pc-windows-msvc"
    "aarch64-pc-windows-msvc"
    "x86_64-apple-darwin"
    "aarch64-apple-darwin"
    "x86_64-unknown-linux-gnu"
    "aarch64-unknown-linux-gnu"
)

fixtures_dir="$(cd "$(dirname "$0")/.." && pwd)/fixtures"
mkdir -p "$fixtures_dir"

current_triple() {
    local arch os
    case "$(uname -m)" in
        x86_64|amd64) arch="x86_64" ;;
        arm64|aarch64) arch="aarch64" ;;
        *) echo "unsupported arch: $(uname -m)" >&2; exit 1 ;;
    esac
    case "$(uname -s)" in
        Darwin) os="apple-darwin" ;;
        Linux) os="unknown-linux-gnu" ;;
        MINGW*|MSYS*|CYGWIN*) os="pc-windows-msvc" ;;
        *) echo "unsupported OS: $(uname -s)" >&2; exit 1 ;;
    esac
    echo "${arch}-${os}"
}

if [[ "${1:-}" == "--all" ]]; then
    triples=("${TRIPLES_ALL[@]}")
else
    triples=("$(current_triple)")
fi

auth_args=()
if [[ -n "${GITHUB_TOKEN:-}" ]]; then
    auth_args=(-H "Authorization: Bearer ${GITHUB_TOKEN}")
fi

for triple in "${triples[@]}"; do
    for version in "${VERSIONS[@]}"; do
        name="cpython-${version}+${TAG}-${triple}-install_only.tar.gz"
        target="${fixtures_dir}/${name}"
        if [[ -f "$target" ]]; then
            echo "exists: ${name}"
            continue
        fi
        url="https://github.com/astral-sh/python-build-standalone/releases/download/${TAG}/${name}"
        echo "fetching: ${name}"
        curl -fSL --retry 3 ${auth_args[@]+"${auth_args[@]}"} -o "${target}.partial" "$url"
        mv "${target}.partial" "$target"
    done
done

echo "fixtures ready in ${fixtures_dir}"
