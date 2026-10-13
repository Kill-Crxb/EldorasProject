#!/usr/bin/env bash
# Publishes the embedded CrabSystem package (Packages/com.crxb.crabsystem) to its own repo, so other projects
# install it by git URL. NinjaGame keeps the copy you edit; this copies it into a clone of the CrabSystem repo,
# commits, tags it with the package.json version and pushes.
#
#   Tools/publish-crabsystem.sh
#
# CRAB_REMOTE  the CrabSystem repo (default https://github.com/Kill-Crxb/CrabSystem.git)
# CRAB_CLONE   where its clone lives (default: a CrabSystem folder next to this project)
set -euo pipefail

project="$(cd "$(dirname "$0")/.." && pwd)"
package="$project/Packages/com.crxb.crabsystem"
remote="${CRAB_REMOTE:-https://github.com/Kill-Crxb/CrabSystem.git}"
clone="${CRAB_CLONE:-$(dirname "$project")/CrabSystem}"
version="$(sed -n 's/^ *"version": *"\([^"]*\)".*/\1/p' "$package/package.json" | head -1)"
tag="v$version"
source_commit="$(git -C "$project" rev-parse --short HEAD)"

if [ -n "$(git -C "$project" status --porcelain -- Packages/com.crxb.crabsystem)" ]; then
  echo "Commit the CrabSystem changes in this project first."
  exit 1
fi

if [ ! -d "$clone/.git" ]; then
  git clone -q "$remote" "$clone"
fi
git -C "$clone" fetch -q --tags origin

if git -C "$clone" rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
  echo "$tag is already published. Bump \"version\" in package.json and add a CHANGELOG line."
  exit 1
fi

# A brand-new repo has no main yet.
if git -C "$clone" rev-parse -q --verify origin/main >/dev/null; then
  git -C "$clone" checkout -q -B main origin/main
else
  git -C "$clone" symbolic-ref HEAD refs/heads/main
fi

find "$clone" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -a "$package/." "$clone/"

git -C "$clone" add -A
if ! git -C "$clone" diff --cached --quiet; then
  git -C "$clone" commit -q -m "CrabSystem $version (from EldorasProject $source_commit)"
fi
git -C "$clone" tag "$tag"
git -C "$clone" push -q origin main "$tag"

echo "Published $tag."
echo "Install in another project: Package Manager > + > Add package from git URL > $remote#$tag"
