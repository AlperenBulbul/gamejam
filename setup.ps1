# CIVIL GAME JAM 2026 - repo setup. Safe to run more than once.
#  - First person: creates the folder structure, git repo and the PUBLIC GitHub repo.
#  - Teammates (after cloning): configures Unity Smart Merge + the commit size guard.
$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath $PSScriptRoot

function Say($m, $c = 'Cyan') { Write-Host $m -ForegroundColor $c }
function Has($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }

Say "`n== CIVIL GAME JAM 2026 - repo setup ==`n"

if (-not (Has git)) {
    # GitHub Desktop ships its own git but doesn't put it on PATH - borrow it.
    $gd = Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA 'GitHubDesktop') -Directory -Filter 'app-*' -ErrorAction SilentlyContinue |
        Sort-Object { [version]($_.Name -replace '^app-', '' -replace '[^0-9.].*$', '') } -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'resources\app\git\cmd' } |
        Where-Object { Test-Path (Join-Path $_ 'git.exe') } |
        Select-Object -Last 1
    if ($gd) {
        $env:PATH = "$gd;$env:PATH"
        Say "[ok] Using GitHub Desktop's git" Green
    } else {
        Say "Git not found. Install it from https://git-scm.com/download/win, then run setup.bat again." Red
        exit 1
    }
}

# ---------- 1. Unity project must exist in .\Game ----------
if (-not (Test-Path 'Game\ProjectSettings')) {
    Say "No Unity project found in .\Game yet." Yellow
    Say "  Unity Hub > New project > choose a template" Yellow
    Say "  Project name: Game" Yellow
    Say "  Location:     $PSScriptRoot" Yellow
    Say "Wait until Unity finishes opening it, then run setup.bat again." Yellow
    exit 1
}

# ---------- 2. Folder structure ----------
$folders = @(
    'Game\Assets\_Project\Scripts',
    'Game\Assets\_Project\Editor',
    'Game\Assets\_Project\Scenes',
    'Game\Assets\_Project\Scenes\Sandbox',
    'Game\Assets\_Project\Prefabs\Characters',
    'Game\Assets\_Project\Prefabs\Environment',
    'Game\Assets\_Project\Prefabs\UI',
    'Game\Assets\_Project\Art\Characters',
    'Game\Assets\_Project\Art\Environment',
    'Game\Assets\_Project\Art\UI',
    'Game\Assets\_Project\Art\VFX',
    'Game\Assets\_Project\Animations',
    'Game\Assets\_Project\Audio\Music',
    'Game\Assets\_Project\Audio\SFX',
    'Game\Assets\_Project\Materials',
    'Game\Assets\_Project\Fonts',
    'Game\Assets\ThirdParty',
    'ArtSource\Characters',
    'ArtSource\Environment'
)
foreach ($f in $folders) {
    if (-not (Test-Path $f)) {
        New-Item -ItemType Directory -Path $f -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $f '.gitkeep') -Force | Out-Null
    }
}
$jb = 'Game\Assets\_Project\Editor\JamBuild.cs'
if (-not (Test-Path $jb)) { Copy-Item '_setup\JamBuild.cs' $jb }
Say "[ok] Folder structure + Jam build menu" Green

# ---------- 3. Git repo ----------
if (-not (Test-Path '.git')) {
    git init -b main | Out-Null
    Say "[ok] Git repo initialized" Green
}
git config core.quotepath false

if (-not (git config user.name)) {
    $n = Read-Host "Your name for git commits"
    git config --global user.name "$n"
}
if (-not (git config user.email)) {
    $e = Read-Host "Your email for git commits (same as your GitHub account)"
    git config --global user.email "$e"
}

# ---------- 4. Unity Smart Merge (scenes/prefabs) ----------
$ver = $null
$pv = 'Game\ProjectSettings\ProjectVersion.txt'
if (Test-Path $pv) {
    $m = Select-String -Path $pv -Pattern 'm_EditorVersion:\s*(\S+)'
    if ($m) { $ver = $m.Matches[0].Groups[1].Value }
}
$roots = @(Join-Path $env:ProgramFiles 'Unity\Hub\Editor')
$sec = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
if (Test-Path $sec) { try { $p = Get-Content $sec -Raw | ConvertFrom-Json; if ($p) { $roots += [string]$p } } catch {} }
$cands = @()
foreach ($r in $roots) {
    if ($r -and (Test-Path $r)) {
        $cands += Get-ChildItem -Path $r -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName 'Editor\Data\Tools\UnityYAMLMerge.exe' } |
            Where-Object { Test-Path $_ }
    }
}
$yaml = $null
if ($ver) { $yaml = $cands | Where-Object { $_ -like "*\$ver\*" } | Select-Object -First 1 }
if (-not $yaml -and $cands) { $yaml = $cands | Sort-Object | Select-Object -Last 1 }

if ($yaml) {
    $y = $yaml -replace '\\', '/'
    git config merge.unityyamlmerge.name "Unity SmartMerge"
    git config merge.unityyamlmerge.driver "`"$y`" merge -h -p --force %O %B %A %A"
    git config merge.unityyamlmerge.recursive binary
    Say "[ok] Unity Smart Merge configured ($ver)" Green
} else {
    Say "[!!] UnityYAMLMerge.exe not found - scene/prefab merges will be manual. Ignore this if Unity is installed in an unusual place." Yellow
}

# ---------- 5. Pre-commit guard: block files over 50 MB ----------
$hook = @'
#!/bin/sh
# Installed by setup.ps1: GitHub rejects files > 100 MB, so block anything > 50 MB.
git diff --cached --name-only --diff-filter=AM | {
  fail=0
  while IFS= read -r f; do
    [ -f "$f" ] || continue
    size=$(wc -c < "$f" | tr -d ' ')
    if [ "$size" -gt 52428800 ]; then
      echo "BLOCKED: '$f' is $((size/1048576)) MB. Keep files under 50 MB (compress it, or keep it out of the repo)."
      fail=1
    fi
  done
  exit $fail
}
'@
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '.git\hooks\pre-commit'), ($hook -replace "`r`n", "`n"))
Say "[ok] Commit size guard installed" Green

# ---------- 6. First commit ----------
git rev-parse --verify -q HEAD | Out-Null
if ($LASTEXITCODE -ne 0) {
    git add -A
    git commit -q -m "Initial project setup (Unity + jam docs)"
    Say "[ok] First commit created" Green
}

# ---------- 7. Public GitHub repo (rule 6.5: missing/private repo = disqualified) ----------
$remotes = @(git remote)
if ($remotes -notcontains 'origin') {
    if (Has gh) {
        gh auth status *> $null
        if ($LASTEXITCODE -ne 0) { Say "Log in to GitHub in the browser window..." ; gh auth login --web -p https }
        $name = Read-Host "GitHub repo name [civil-game-jam-2026]"
        if (-not $name) { $name = 'civil-game-jam-2026' }
        gh repo create $name --public --source . --remote origin --push
        if ($LASTEXITCODE -eq 0) { Say "[ok] PUBLIC repo created and pushed" Green }
    } else {
        Say "`nLAST STEP - publish with GitHub Desktop:" Yellow
        Say "  1. GitHub Desktop > File > Add local repository > choose $PSScriptRoot" Yellow
        Say "  2. Click 'Publish repository'" Yellow
        Say "  3. UNTICK 'Keep this code private' (private repo = disqualified, rule 6.5)" Yellow
        Say "  4. Click Publish" Yellow
    }
}

# ---------- 8. Check the repo really is public ----------
if ((Has gh) -and (@(git remote) -contains 'origin')) {
    $vis = gh repo view --json visibility -q .visibility 2>$null
    if ($vis -eq 'PUBLIC') { Say "[ok] Repo is PUBLIC: $(git remote get-url origin)" Green }
    elseif ($vis) { Say "[!!] Repo is $vis. It MUST be public before submission (rule 6.5)." Red }
}

Say "`nDone. Next: add teammates as collaborators on GitHub (Settings > Collaborators), then each of them clones the repo and runs setup.bat." Cyan
