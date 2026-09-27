# PowerShell twin of bump.sh. Run after editing ANY file under a .claude tree.
#   .\bump.ps1 global  "reviewer: check Postman assertions"
#   .\bump.ps1 project "react skill: forbid default exports"
#   .\bump.ps1 $HOME\.claude "…"   .\bump.ps1 C:\repo\.claude "…"   add -Minor or -Major to bump those parts
param([Parameter(Mandatory)][string]$Target, [Parameter(Mandatory)][string]$Message, [switch]$Minor, [switch]$Major)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dir = switch ($Target) { 'global' { Join-Path $Here 'global\.claude' } 'project' { Join-Path $Here 'project\.claude' } default { $Target } }
$M = Join-Path $Dir 'manifest.yml'; if (-not (Test-Path $M)) { throw "no manifest at $M" }
$txt = Get-Content $M -Raw
$cur = [regex]::Match($txt, '(?m)^version:\s*(\S+)').Groups[1].Value; $p = $cur -split '\.' | ForEach-Object { [int]$_ }
if ($Major) { $p[0]++; $p[1]=0; $p[2]=0 } elseif ($Minor) { $p[1]++; $p[2]=0 } else { $p[2]++ }
$new = "$($p[0]).$($p[1]).$($p[2])"
$side = [regex]::Match($txt, '(?m)^side:\s*(\S+)').Groups[1].Value; $base = [regex]::Match($txt, '(?m)^base:\s*(\S+)').Groups[1].Value
$who = (git config user.name 2>$null); if (-not $who) { $who = $env:USERNAME }; $when = Get-Date -Format 'yyyy-MM-dd'
$skip = @('manifest.yml','pipeline.yml','pipeline.local.yml','pipeline.local.example.yml','triage-memory.md','design-system.md')
$files = Get-ChildItem $Dir -Recurse -File | Where-Object { $skip -notcontains $_.Name -and $_.FullName -notlike '*\.pipeline-incoming\*' } |
  Sort-Object FullName | ForEach-Object { "  $($_.FullName.Substring($Dir.Length+1).Replace('\','/')): $((Get-FileHash $_.FullName -Algorithm SHA256).Hash.Substring(0,12).ToLower())" }
$changes = ($txt -split '(?m)^changes:\s*$')[1]; if ($changes) { $changes = $changes.TrimEnd() }
$out = @("package: momenta-feature-pipeline", "side: $side", "version: $new", "base: $base", "files:") + $files + @("changes:")
if ($changes) { $out += $changes.TrimStart("`r","`n") }
$out += "  - { v: $new, date: $when, by: `"$who`", note: `"$Message`" }"
($out -join "`n") + "`n" | Set-Content $M -NoNewline
Write-Host "$Dir → v$new  ($Message)"
