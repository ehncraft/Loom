#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <tag-prefix>" >&2
    exit 1
fi

tag_prefix="$1"

today=$(date -u +%Y-%m-%d)
year="${today%%-*}"
rest="${today#*-}"
month=$((10#${rest%%-*}))
day=$((10#${rest#*-}))
today_version="${year}.${month}.${day}"

tag_exists() {
    [[ -n "$(git tag -l "$1")" ]]
}

if ! tag_exists "${tag_prefix}${today_version}"; then
    echo "$today_version"
    exit 0
fi

n=1
while tag_exists "${tag_prefix}${today_version}.${n}"; do
    n=$((n + 1))
done

echo "${today_version}.${n}"
