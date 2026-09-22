<#
.SYNOPSIS
  Builds the docs site and publishes it to the gh-pages branch, which GitHub Pages serves
  at https://breeze.github.io/breeze-server-v3/.

.DESCRIPTION
  1. Refuses a working copy with uncommitted changes, so the published site always matches
     a commit.
  2. Builds, in two steps rather than as one `dotnet docfx docs/docfx.json`, so that warnings
     can stop the publish. The metadata step's two known warnings are tolerated; the build
     step must report none. A dead link, an unresolved xref or a missing code-snippet region
     shows up there, and DocFX calls all three a warning rather than an error - so without
     this check `docfx` exits 0 and a broken site goes live. See DOCS.md.
  3. Commits the built site to gh-pages, in a temporary worktree, so the branch you are on
     and your working copy are never touched. gh-pages holds only the built site. Its commits
     are named after the source commit: "Publish docs from 8379d69".
  4. Pushes gh-pages, unless -NoPush. Pages updates a minute or two later.

  If gh-pages was already published from the commit you are on it does nothing, or only
  pushes a publish that -NoPush left behind. It decides that by commit, not by comparing
  files: DocFX writes its search index in the order pages finish rendering, so two builds of
  the same source are never byte-identical.

  No base path is set, unlike the client's equivalent script. DocFX emits entirely relative
  links, so the site serves correctly from /breeze-server-v3/ with no configuration.

.PARAMETER NoPush
  Build and commit to gh-pages, but leave the push to you.

.PARAMETER Force
  Publish again from a commit that has already been published.

.EXAMPLE
  .\scripts\publish-docs.ps1

.EXAMPLE
  .\scripts\publish-docs.ps1 -NoPush

.EXAMPLE
  .\scripts\publish-docs.ps1 -Force
#>
[CmdletBinding()]
param(
  [switch] $NoPush,
  [switch] $Force
)

# Written for Windows PowerShell 5.1 as well as PowerShell 7: no ?? / && / ternaries.
$ErrorActionPreference = 'Stop'

$Branch = 'gh-pages'
$RemoteBranch = "origin/$Branch"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$siteDir = Join-Path $repoRoot 'docs\_site'
$docfxJson = Join-Path $repoRoot 'docs\docfx.json'

function Write-Step([string] $message) {
  Write-Host ""
  Write-Host "==> $message" -ForegroundColor Cyan
}

function Say([string] $message) {
  Write-Host "publish-docs: $message"
}

function Assert-Command([string] $name, [string] $hint) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "'$name' was not found on PATH. $hint"
  }
}

# git in the repo. Native exit codes do not trip $ErrorActionPreference, so callers that
# care check $LASTEXITCODE - and Invoke-Git throws for the ones that should never fail.
function Invoke-Git {
  $output = & git -C $repoRoot @args
  if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit $LASTEXITCODE)." }
  return $output
}

# Same, but a non-zero exit is an answer rather than a failure (merge-base --is-ancestor).
function Test-Git {
  & git -C $repoRoot @args | Out-Null
  return ($LASTEXITCODE -eq 0)
}

# git in the gh-pages worktree: the site is committed exactly as built, whatever
# core.autocrlf says.
function Invoke-PagesGit {
  $output = & git -C $worktree -c core.autocrlf=false -c core.safecrlf=false @args
  if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed in the worktree (exit $LASTEXITCODE)." }
  return $output
}

# DocFX colours its output, and the escape codes survive a pipe.
function Remove-Ansi([string] $text) {
  return ($text -replace '\x1b\[[0-9;]*m', '')
}

Assert-Command 'git' 'Install Git.'
Assert-Command 'dotnet' 'Install the .NET 10 SDK.'

if (Invoke-Git status --porcelain) {
  throw "The working copy has uncommitted changes. Commit or stash them, so the published site matches a commit."
}

$sourceSha = Invoke-Git rev-parse HEAD
$short = $sourceSha.Substring(0, 7)

# Bring local gh-pages level with the remote one, so this publish goes on top of whatever was
# published last, from here or anywhere else.
$onRemote = [bool] (Invoke-Git ls-remote --heads origin $Branch)
if ($onRemote) { Invoke-Git fetch --quiet origin $Branch | Out-Null }
$localExists = [bool] (Invoke-Git branch --list $Branch)

if ($onRemote -and -not $localExists) {
  Invoke-Git branch --quiet $Branch $RemoteBranch | Out-Null
} elseif ($onRemote -and $localExists) {
  if (Test-Git merge-base --is-ancestor $Branch $RemoteBranch) {
    Invoke-Git branch --quiet --force $Branch $RemoteBranch | Out-Null   # behind: fast-forward
  } elseif (-not (Test-Git merge-base --is-ancestor $RemoteBranch $Branch)) {
    throw "Local $Branch and $RemoteBranch have diverged. Delete the local one (git branch -D $Branch) and run this again."
  }                                                                      # ahead: unpushed publishes, keep them
}

$exists = ($onRemote -or $localExists)

function Test-Unpushed {
  if (-not $exists) { return $false }
  if (-not $onRemote) { return $true }
  return ((Invoke-Git rev-list --count "$RemoteBranch..$Branch") -ne '0')
}

function Push-IfAsked {
  if ($NoPush) {
    Say "$Branch is committed but not pushed. Push it with: git push origin $Branch"
  } else {
    Invoke-Git push --quiet origin $Branch | Out-Null
    Say "pushed $Branch. GitHub Pages updates in a minute or so."
  }
}

if ($exists -and -not $Force) {
  $lastMessage = (Invoke-Git log -1 --format=%B $Branch) -join "`n"
  if ($lastMessage.Contains($sourceSha)) {
    Say "$Branch was already built from $short."
    if (Test-Unpushed) {
      Push-IfAsked
    } else {
      Say "Nothing to publish. Use -Force to rebuild and publish it again."
    }
    exit 0
  }
}

# --- build ---------------------------------------------------------------------------------

Write-Step "Building $short"

# The API metadata. Its two known warnings are tolerated; see DOCS.md.
& dotnet docfx metadata $docfxJson
if ($LASTEXITCODE -ne 0) { throw "docfx metadata failed (exit $LASTEXITCODE)." }

# The site. Any warning here is a content problem, and stops the publish.
& dotnet docfx build $docfxJson | Tee-Object -Variable buildOutput
if ($LASTEXITCODE -ne 0) { throw "docfx build failed (exit $LASTEXITCODE)." }

$warnings = 0
foreach ($line in $buildOutput) {
  if ((Remove-Ansi $line) -match '^\s*(\d+)\s+warning\(s\)\s*$') { $warnings = [int] $Matches[1] }
}
if ($warnings -ne 0) {
  throw "docfx build reported $warnings warning(s); the site is not published. Fix them, or see DOCS.md."
}

if (-not (Test-Path $siteDir)) { throw "No site at $siteDir." }

# --- publish -------------------------------------------------------------------------------

$worktree = Join-Path ([IO.Path]::GetTempPath()) ("breeze-gh-pages-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$worktreeAdded = $false

try {
  Write-Step "Committing the site to $Branch"

  if ($exists) {
    Invoke-Git worktree add --quiet $worktree $Branch | Out-Null
  } else {
    Invoke-Git worktree add --quiet --orphan -b $Branch $worktree | Out-Null
  }
  $worktreeAdded = $true

  # Replace everything but .git with the new build.
  Get-ChildItem -LiteralPath $worktree -Force |
    Where-Object { $_.Name -ne '.git' } |
    Remove-Item -Recurse -Force

  Copy-Item -Path (Join-Path $siteDir '*') -Destination $worktree -Recurse -Force

  # Without this, Pages runs the site through Jekyll, which drops files whose names start
  # with an underscore. Nothing DocFX emits does today; this keeps it that way if that changes.
  [IO.File]::WriteAllText((Join-Path $worktree '.nojekyll'), '')

  Invoke-PagesGit add --all | Out-Null
  Invoke-PagesGit commit --quiet --allow-empty -m "Publish docs from $short" -m "Built from $sourceSha" | Out-Null
  Say "committed the site built from $short to $Branch."
} finally {
  if ($worktreeAdded) {
    & git -C $repoRoot worktree remove --force $worktree 2>$null | Out-Null
  }
  if (Test-Path -LiteralPath $worktree) {
    Remove-Item -LiteralPath $worktree -Recurse -Force -ErrorAction SilentlyContinue
  }
}

Push-IfAsked
