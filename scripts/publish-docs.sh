#!/usr/bin/env bash
#
# Builds the docs site and publishes it to the gh-pages branch, which GitHub Pages serves at
# https://breeze.github.io/breeze-server-v3/ (Settings -> Pages -> Deploy from a branch ->
# gh-pages, / (root)).
#
#   scripts/publish-docs.sh                build, commit to gh-pages, push
#   scripts/publish-docs.sh --no-push      build and commit, but leave the push to you
#   scripts/publish-docs.sh --force        publish even if gh-pages was built from this commit
#
# gh-pages holds nothing but the built site. It is checked out in a temporary worktree, so the
# branch you are on and your working copy are never touched. Each publish is one commit on
# gh-pages that names the commit it was built from - which is why a working copy with
# uncommitted changes is refused: the site would not match any commit.
#
# "Already published" is decided by that commit, not by comparing files: DocFX writes its search
# index in the order pages finish rendering, so two builds of the same source differ.
#
# No base path is set, unlike the client's equivalent. DocFX's output is entirely relative, so it
# serves correctly from /breeze-server-v3/ with no configuration.
#
# The build runs in two steps rather than as one `dotnet docfx docs/docfx.json`, so that warnings
# can fail the publish. The metadata step emits two known warnings (see DOCS.md); the build step
# should emit none, and a broken link or an unresolved xref is exactly what would show up there.
# DocFX reports those as warnings, not errors, so without this check a dead link would go live.

set -euo pipefail

BRANCH=gh-pages
REMOTE_BRANCH="origin/$BRANCH"

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
site_dir="$repo_root/docs/_site"
docfx_json="$repo_root/docs/docfx.json"

push=true
force=false
for arg in "$@"; do
  case "$arg" in
    --no-push) push=false ;;
    --force)   force=true ;;
    *) echo "publish-docs: unknown argument: $arg" >&2; exit 1 ;;
  esac
done

say()  { echo "publish-docs: $*"; }
fail() { echo "publish-docs: $*" >&2; exit 1; }

git_r() { git -C "$repo_root" "$@"; }

# Git in the gh-pages worktree: the site is committed exactly as built, whatever core.autocrlf says.
git_pages() { git -C "$worktree" -c core.autocrlf=false -c core.safecrlf=false "$@"; }

[ -n "$(git_r status --porcelain)" ] && \
  fail "the working copy has uncommitted changes. Commit or stash them, so the published site matches a commit."

source_sha=$(git_r rev-parse HEAD)
short=${source_sha:0:7}

# Bring local gh-pages level with the remote one, so this publish goes on top of whatever was
# published last, from here or anywhere else.
on_remote=false
[ -n "$(git_r ls-remote --heads origin "$BRANCH")" ] && on_remote=true
$on_remote && git_r fetch --quiet origin "$BRANCH"

local_exists=false
[ -n "$(git_r branch --list "$BRANCH")" ] && local_exists=true

if $on_remote && ! $local_exists; then
  git_r branch --quiet "$BRANCH" "$REMOTE_BRANCH"
elif $on_remote && $local_exists; then
  if git_r merge-base --is-ancestor "$BRANCH" "$REMOTE_BRANCH" 2>/dev/null; then
    git_r branch --quiet --force "$BRANCH" "$REMOTE_BRANCH"        # behind: fast-forward
  elif ! git_r merge-base --is-ancestor "$REMOTE_BRANCH" "$BRANCH" 2>/dev/null; then
    fail "local $BRANCH and $REMOTE_BRANCH have diverged. Delete the local one (git branch -D $BRANCH) and run this again."
  fi                                                               # ahead: unpushed publishes, keep them
fi

exists=false
{ $on_remote || $local_exists; } && exists=true

unpushed() {
  $exists || return 1
  $on_remote || return 0
  [ "$(git_r rev-list --count "$REMOTE_BRANCH..$BRANCH")" != "0" ]
}

push_if_asked() {
  if $push; then
    git_r push --quiet origin "$BRANCH"
    say "pushed $BRANCH. GitHub Pages updates in a minute or so."
  else
    say "$BRANCH is committed but not pushed. Push it with: git push origin $BRANCH"
  fi
}

if $exists && ! $force && git_r log -1 --format=%B "$BRANCH" | grep -qF "$source_sha"; then
  say "$BRANCH was already built from $short."
  if unpushed; then push_if_asked
  else say "Nothing to publish. Use --force to rebuild and publish it again."
  fi
  exit 0
fi

say "building $short"

# The API metadata. Its two known warnings are tolerated; see DOCS.md.
dotnet docfx metadata "$docfx_json"

# The site. Any warning here is a content problem - a dead link, an unresolved xref, a code
# snippet whose region is missing - and stops the publish.
build_log=$(mktemp)
trap 'rm -f "$build_log"' EXIT
set +e
dotnet docfx build "$docfx_json" 2>&1 | tee "$build_log"
build_status=${PIPESTATUS[0]}
set -e
[ "$build_status" -ne 0 ] && fail "docfx build failed."

warnings=$(sed 's/\x1b\[[0-9;]*m//g' "$build_log" | sed -n 's/^[[:space:]]*\([0-9][0-9]*\) warning(s)$/\1/p' | tail -1)
if [ -n "$warnings" ] && [ "$warnings" != "0" ]; then
  fail "docfx build reported $warnings warning(s); the site is not published. Fix them, or see DOCS.md."
fi

[ -d "$site_dir" ] || fail "no site at $site_dir."

worktree=$(mktemp -d -t breeze-gh-pages-XXXXXX)
rmdir "$worktree"          # git worktree add wants to create it

cleanup() {
  git_r worktree remove --force "$worktree" >/dev/null 2>&1 || true
  [ -d "$worktree" ] && rm -rf "$worktree"
  rm -f "$build_log"
  return 0
}
trap cleanup EXIT

if $exists; then
  git_r worktree add --quiet "$worktree" "$BRANCH"
else
  git_r worktree add --quiet --orphan -b "$BRANCH" "$worktree"
fi

# Replace everything but .git with the new build.
find "$worktree" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r "$site_dir/." "$worktree/"
# Without this, Pages runs the site through Jekyll, which drops files whose names start with an
# underscore. Nothing DocFX emits does today; this keeps it that way if that changes.
: > "$worktree/.nojekyll"

git_pages add --all
git_pages commit --quiet --allow-empty -m "Publish docs from $short" -m "Built from $source_sha"
say "committed the site built from $short to $BRANCH."

cleanup
trap - EXIT

push_if_asked
