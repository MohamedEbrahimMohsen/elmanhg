# Feature pipeline installer. PowerShell twin of install.sh — same options, same behaviour.
#   .\install.ps1                     interactive: global + project in the current directory
#   .\install.ps1 -Global             only $HOME\.claude
#   .\install.ps1 -Project C:\repo    only that repo, asks for the role
#   .\install.ps1 -Role fullstack -Project C:\repo
param([switch]$Global, [string]$Project, [string]$Role)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcGlobal = Join-Path $Here 'global\.claude'; $SrcProject = Join-Path $Here 'project\.claude'
$DstGlobal = if ($env:CLAUDE_HOME) { $env:CLAUDE_HOME } else { Join-Path $HOME '.claude' }
$DoGlobal = [bool]$Global -or -not $Project; $DoProject = [bool]$Project -or -not $Global
$ProjectDir = if ($Project) { $Project } else { (Get-Location).Path }
$Skip = @('pipeline.yml','pipeline.local.yml','triage-memory.md','design-system.md','manifest.yml')

function Ver($dir) { $m = Join-Path $dir 'manifest.yml'; if (Test-Path $m) { (Select-String -Path $m -Pattern '^version:\s*(\S+)').Matches[0].Groups[1].Value } }
function Keep-Base($src, $v, $label) { $side = if ($label -like "global*") { "global" } else { "project" }; $b = Join-Path $DstGlobal ".pipeline-base\$v\$side"; New-Item -ItemType Directory -Force $b | Out-Null; Copy-Item "$src\*" $b -Recurse -Force }
function Sha($f) { (Get-FileHash $f -Algorithm SHA256).Hash.Substring(0,12) }
function Rel($base, $f) { $f.Substring($base.Length + 1).Replace('\','/') }

function Other-Stack($rel, $dst) { $st = $null
  if ($rel -match '^skills/([a-z-]+)-feature/') { $st = $Matches[1] } elseif ($rel -match '^conventions/([a-z]+)-testing\.md$') { $st = $Matches[1] } else { return $false }
  $py = Join-Path $dst 'pipeline.yml'; if (-not (Test-Path $py)) { return $false }
  -not (Select-String -Path $py -Pattern "^  $([regex]::Escape($st)):" -Quiet) }
function Compare-Tree($src, $dst) {
  $out = @()
  Get-ChildItem $src -Recurse -File | ForEach-Object {
    $rel = Rel $src $_.FullName; if ($Skip -contains (Split-Path $rel -Leaf)) { return }
    if (Other-Stack $rel $dst) { return }
    $d = Join-Path $dst $rel
    if (-not (Test-Path $d)) { $out += "missing  $rel" }
    elseif ((Sha $_.FullName) -ne (Sha $d)) { $out += "changed  $rel" }
  }
  $m = Join-Path $dst 'manifest.yml'
  if (Test-Path $m) { $inFiles = $false; Get-Content $m | ForEach-Object {
    if ($_ -match '^files:') { $inFiles = $true; return }; if ($_ -match '^changes:') { $inFiles = $false }
    if ($inFiles -and $_ -match '^  (\S+): ') { $rel = $Matches[1]; if (-not (Test-Path (Join-Path $src $rel)) -and -not (Other-Stack $rel $dst)) { $out += "removed  $rel" } }
  } }
  $out
}

function Install-Tree($src, $dst, $label) {
  $new = Ver $src; $cur = Ver $dst
  if (-not $cur) {
    Write-Host "→ ${label}: fresh install v$new"; New-Item -ItemType Directory -Force $dst | Out-Null
    $clash = @(Get-ChildItem $src -Recurse -File | ForEach-Object { $rel = Rel $src $_.FullName; $d = Join-Path $dst $rel; if ((Test-Path $d) -and ((Sha $_.FullName) -ne (Sha $d))) { $rel } })
    if ($clash.Count -gt 0) {
      Write-Host "  these already exist in $dst and differ from the package:"; $clash | ForEach-Object { Write-Host "    $_" }
      Write-Host '  Options: [o]verwrite them · [k]eep yours (package versions saved next to them as *.pkg) · [a]bort'
      switch (Read-Host '  choose') {
        'o' { }
        'k' { Get-ChildItem $src -Recurse -File | ForEach-Object { $rel = Rel $src $_.FullName; $d = Join-Path $dst $rel
                if ($clash -contains $rel) { Copy-Item $_.FullName "$d.pkg" } else { New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; Copy-Item $_.FullName $d } }
              Keep-Base $src $new $label; Write-Host "  installed v$new; your files kept, package copies as *.pkg — diff them when you have time"; return }
        default { Write-Host '  aborted'; exit 1 }
      }
    }
    Copy-Item "$src\*" $dst -Recurse -Force; Keep-Base $src $new $label; return
  }
  Write-Host "→ ${label}: installed v$cur, package v$new"
  $diff = Compare-Tree $src $dst
  if (-not $diff -and $cur -eq $new) { Write-Host '  identical, nothing to do'; return }
  $diff | ForEach-Object { Write-Host "  $_" }
  Write-Host '  changed = edited locally · missing = new in package · removed = package no longer ships it'
  Write-Host '  Options: [u]pdate all (overwrites changed) · [n]ew files only (keeps your edits) · [s]kip · [c]laude merge (run /pipeline-sync later)'
  switch (Read-Host '  choose') {
    'u' { Copy-Item "$src\*" $dst -Recurse -Force; Keep-Base $src $new $label; Write-Host "  updated to v$new" }
    'n' { Get-ChildItem $src -Recurse -File | ForEach-Object { $rel = Rel $src $_.FullName; $d = Join-Path $dst $rel
            if (-not (Test-Path $d)) { New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null; Copy-Item $_.FullName $d; Write-Host "  + $rel" } }
          $m = Join-Path $dst 'manifest.yml'; if (Test-Path $m) { (Get-Content $m) -replace '^base:.*', "base: $new" | Set-Content $m }
          Write-Host "  new files added; edited files kept; manifest base → $new" }
    'c' { $inc = Join-Path $dst '.pipeline-incoming'; New-Item -ItemType Directory -Force $inc | Out-Null; Copy-Item "$src\*" $inc -Recurse -Force
          Write-Host "  package copied to $inc — open claude in that repo and run: /pipeline-sync" }
    default { Write-Host '  skipped' }
  }
}

function Pick-Role {
  if ($script:Role) { return }
  Write-Host 'Project role:'; Write-Host '  1) backend         dotnet'; Write-Host '  2) frontend        react'; Write-Host '  3) fullstack       dotnet + react'
  Write-Host '  4) mobile-flutter  flutter'; Write-Host '  5) mobile-kmp      kmp'; Write-Host '  6) ai              python + node + react   (AI tools + POC web app)'; Write-Host '  Stacks available for custom: dotnet · react · node · flutter · kmp · python · python-flask · python-django'; Write-Host '  7) custom          pick stacks'
  switch (Read-Host 'choose 1-7') { '1' {$script:Role='backend'} '2' {$script:Role='frontend'} '3' {$script:Role='fullstack'} '4' {$script:Role='mobile-flutter'} '5' {$script:Role='mobile-kmp'} '6' {$script:Role='ai'} '7' {$script:Role='custom'} default { throw 'invalid' } }
}
function Stacks-For-Role {
  switch ($script:Role) { 'backend' {'dotnet'} 'frontend' {'react'} 'fullstack' {'dotnet','react'} 'mobile-flutter' {'flutter'} 'mobile-kmp' {'kmp'} 'ai' {'python','node','react'}
    'custom' { (Read-Host 'stacks (space separated)') -split '\s+' } default { throw "unknown role $script:Role" } }
}

function Install-Project {
  $dst = Join-Path $ProjectDir '.claude'; Pick-Role; $stacks = @(Stacks-For-Role)
  Write-Host "→ project: $ProjectDir · role $script:Role · stacks: $($stacks -join ' ')"
  if (Test-Path (Join-Path $dst 'manifest.yml')) { Install-Tree $SrcProject $dst 'project' }
  else {
    New-Item -ItemType Directory -Force (Join-Path $dst 'skills'), (Join-Path $dst 'conventions') | Out-Null
    Copy-Item (Join-Path $SrcProject 'README.md'), (Join-Path $SrcProject 'manifest.yml') $dst
    if (-not (Test-Path (Join-Path $dst 'triage-memory.md'))) { Copy-Item (Join-Path $SrcProject 'triage-memory.md') $dst }
    foreach ($s in $stacks) {
      Copy-Item (Join-Path $SrcProject "skills\$s-feature") (Join-Path $dst 'skills') -Recurse -Force
      $t = $s
      Copy-Item (Join-Path $SrcProject "conventions\$t-testing.md") (Join-Path $dst 'conventions')
    }
    $py = Join-Path $dst 'pipeline.yml'
    if (Test-Path $py) { Write-Host '  pipeline.yml exists, kept' } else {
      $frag = Join-Path $SrcGlobal 'templates\pipeline'
      $parts = @(Get-Content (Join-Path $frag '00-header.yml') -Raw)
      foreach ($s in $stacks) { $parts += Get-Content (Join-Path $frag "10-stack-$s.yml") -Raw }
      $parts += Get-Content (Join-Path $frag '90-rest.yml') -Raw
      ($parts -join '') | Set-Content $py -NoNewline
    }
    Copy-Item (Join-Path $SrcProject 'pipeline.local.example.yml') $dst
    $gi = Join-Path $ProjectDir '.gitignore'
    foreach ($ig in @('.claude/pipeline.local.yml','.claude/settings.local.json')) {
      if (-not (Test-Path $gi) -or -not (Select-String -Path $gi -SimpleMatch -Pattern $ig -Quiet)) { Add-Content $gi $ig }
    }
    if ($stacks | Where-Object { $_ -in 'react','flutter','kmp' }) { if (-not (Test-Path (Join-Path $dst 'design-system.md'))) { Copy-Item (Join-Path $SrcProject 'design-system.md') $dst } }
    Keep-Base $SrcProject (Ver $SrcProject) 'project'
    Write-Host "  installed v$(Ver $SrcProject)"
  }
  Write-Host ''; Write-Host "Next in ${ProjectDir}:"
  Write-Host '  1. edit .claude\pipeline.yml — roots, build/test commands, OpenCode model, base branch'
  Write-Host '  2. tune .claude\skills\<stack>-feature\SKILL.md and .claude\conventions\<stack>-testing.md'
  if ($stacks | Where-Object { $_ -in 'react','flutter','kmp' }) { Write-Host '  3. UI/UX team fills .claude\design-system.md (frontend work refuses to start without it)' }
  Write-Host '  then: claude → /feature #<issue>'
}

if ($DoGlobal) { Install-Tree $SrcGlobal $DstGlobal 'global (~/.claude)' }
if ($DoProject) { Install-Project }
