#Requires -Version 7.2
<#
autopilot.ps1 — unattended supervisor for the feature pipeline. POC repos ONLY. Same behaviour as autopilot.sh.

  pwsh ~/.claude/bin/autopilot.ps1 -Epic 12              run every open story of epic #12, in order
  pwsh ~/.claude/bin/autopilot.ps1 -Prd docs/PRD.md      run /product first, then its epic
  options: -MaxTasks N (stop after N /feature runs)

Refuses to start unless .claude/pipeline.local.yml has `autopilot: on: true` and the git tree is clean.
One fresh `claude -p` process per story step (/feature does one sub-task per process, then prints RESULT:).
Never stops on failures: a failed task is labelled autopilot:blocked and skipped; a red main gets a "repair main"
task first; a subscription usage limit → wait and resume (not counted as an attempt).
Stops only on: all done · `hours` reached (default 72) · nothing runnable left · main still red after max_attempts
repair tasks · -MaxTasks · Ctrl+C.
Files: .process/autopilot/{ledger.md,REPORT.md,logs/} (git-ignored via .process/autopilot/.gitignore).
Env overrides: AUTOPILOT_MAX_TURNS (400) · AUTOPILOT_VERIFY_MAIN (1) · CLAUDE_BIN (claude) · GH_BIN (gh)
Timeout: the process tree is killed at per_task.timeout_min (Windows has no SIGINT; the bash runner sends INT first).
#>
[CmdletBinding()]
param(
  [string]$Epic = '',
  [string]$Prd = '',
  [int]$MaxTasks = 0
)
Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$ClaudeBin  = if ($env:CLAUDE_BIN) { $env:CLAUDE_BIN } else { 'claude' }
$GhBin      = if ($env:GH_BIN) { $env:GH_BIN } else { 'gh' }
$MaxTurns   = if ($env:AUTOPILOT_MAX_TURNS) { $env:AUTOPILOT_MAX_TURNS } else { '400' }
$VerifyMain = if ($null -ne $env:AUTOPILOT_VERIFY_MAIN) { $env:AUTOPILOT_VERIFY_MAIN } else { '1' }
$Inv = [Globalization.CultureInfo]::InvariantCulture

function Say([string]$m) { Write-Host ("[autopilot {0}] {1}" -f (Get-Date).ToUniversalTime().ToString('HH:mm:ss'), $m) }
function Die([string]$m) { [Console]::Error.WriteLine("autopilot: $m"); exit 1 }
function Iso([datetime]$d) { $d.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') }

$Epic = $Epic.TrimStart('#')
if (-not $Epic -and -not $Prd) { Die 'need -Epic <n> or -Prd <path>' }
if ($Epic -and $Epic -notmatch '^\d+$') { Die '-Epic must be a number' }

$root = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $root) { Die 'not inside a git repository' }
Set-Location $root

$Local = '.claude/pipeline.local.yml'
$Team  = '.claude/pipeline.yml'
if (-not (Test-Path $Team))  { Die "no $Team — run install.ps1 -Project . first" }
if (-not (Test-Path $Local)) { Die "no $Local — autopilot is enabled only there (see pipeline.local.example.yml)" }

# ---------- config (line parser; no YAML module needed) ----------
function Get-AutopilotBlock([string]$path) {
  $out = New-Object System.Collections.Generic.List[string]
  $in = $false
  foreach ($line in Get-Content -LiteralPath $path) {
    if ($line -match '^autopilot:\s*(#.*)?$') { $in = $true; continue }
    if ($in -and $line -match '^[^\s#]') { $in = $false }
    if ($in -and $line -notmatch '^\s*#') { $out.Add($line) }
  }
  return ,$out
}
function Get-Ap([string]$key, [string]$default) {
  foreach ($line in (Get-AutopilotBlock $Local)) {
    $clean = ($line -replace '#.*$', '')
    $m = [regex]::Match($clean, "(^|[\s,{])$([regex]::Escape($key)):\s*([^,}\s]+)")
    if ($m.Success) { return ($m.Groups[2].Value -replace '["'']', '') }
  }
  return $default
}
function ConvertTo-NumOr([string]$v, [string]$d) { if ($v -match '^[0-9.]+$') { $v } else { $d } }

if ((Get-AutopilotBlock $Team).Count -gt 0) { Say "warning: autopilot block in $Team ignored — only $Local can turn it on" }

$ApOn        = Get-Ap 'on' 'false'
$BudgetUsd   = [double]::Parse((ConvertTo-NumOr (Get-Ap 'usd' '0') '0'), $Inv)      # 0 = no money cap (subscription)
$BudgetHours = [double]::Parse((ConvertTo-NumOr (Get-Ap 'hours' '72') '72'), $Inv)
$RateWaitMin = if ($env:AUTOPILOT_RATE_WAIT_MIN) { [int]$env:AUTOPILOT_RATE_WAIT_MIN } else { 20 }
$script:Repairs = 0; $script:MainRedLog = ''
$MaxAttempts = [int](ConvertTo-NumOr (Get-Ap 'max_attempts' '3') '3')
$TimeoutMin  = [double]::Parse((ConvertTo-NumOr (Get-Ap 'timeout_min' '240') '240'), $Inv)
$OnBlocked   = Get-Ap 'on_blocked' 'skip'
$Base = 'main'
$inGh = $false
foreach ($line in Get-Content -LiteralPath $Team) {
  if ($line -match '^github:') { $inGh = $true; continue }
  if ($inGh -and $line -match '^[^\s#]') { $inGh = $false }
  if ($inGh -and $line -match '^\s+base:\s*(\S+)') { $Base = $Matches[1]; break }
}

if ($ApOn -ne 'true') { Die "autopilot.on is not true in $Local — refusing to start (human gates stay on)" }
if ($OnBlocked -notin @('skip', 'stop')) { Say "warning: autopilot.on_blocked '$OnBlocked' unknown — using skip"; $OnBlocked = 'skip' }

# ---------- tools ----------
$claudeCmd = Get-Command $ClaudeBin -ErrorAction SilentlyContinue
if (-not $claudeCmd) { Die "$ClaudeBin not on PATH" }
if (-not (Get-Command $GhBin -ErrorAction SilentlyContinue)) { Die "$GhBin not on PATH" }
& $GhBin auth status *> $null
if ($LASTEXITCODE -ne 0) { Die 'gh is not authenticated (gh auth login)' }

# ---------- state dir ----------
$AP = '.process/autopilot'
$Logs = "$AP/logs"
New-Item -ItemType Directory -Force -Path $Logs | Out-Null
if (-not (Test-Path "$AP/.gitignore")) { Set-Content -LiteralPath "$AP/.gitignore" -Value '*' -NoNewline }
$Ledger = "$AP/ledger.md"
$Report = "$AP/REPORT.md"

$dirty = (& git status --porcelain)
if ($dirty) { & git status --short; Die 'git tree is not clean — commit or stash first' }
$settings = '.claude/settings.local.json'
if (-not (Test-Path $settings) -or -not (Select-String -LiteralPath $settings -Pattern '"deny"' -Quiet)) {
  Say 'warning: no permissions.deny in .claude/settings.local.json — copy ~/.claude/settings.autopilot.example.json there'
}
if (-not $env:BASH_DEFAULT_TIMEOUT_MS) { $env:BASH_DEFAULT_TIMEOUT_MS = '300000' }
if (-not $env:BASH_MAX_TIMEOUT_MS)     { $env:BASH_MAX_TIMEOUT_MS = '1200000' }

$Start       = Get-Date
$RunId       = $Start.ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$Deadline    = $Start.AddHours($BudgetHours)
$DeadlineIso = Iso $Deadline
$Tries       = New-Object System.Collections.Generic.List[string]

$script:Spent = 0.0; $script:Tasks = 0; $script:Merged = 0; $script:Blocked = 0; $script:Other = 0; $script:Consec = 0
$script:StopReason = ''
$script:Result = ''; $script:Cost = 0.0; $script:Rc = 0

if (-not (Test-Path $Ledger)) {
  Set-Content -LiteralPath $Ledger -Value "# Autopilot ledger`n`n| Run | # | Issue | Started (UTC) | Minutes | Cost `$ | Exit | Result |`n|-----|---|-------|---------------|---------|--------|------|--------|"
}
function Get-RemainingUsd { if ($BudgetUsd -le 0) { 'unlimited' } else { [math]::Max(0.0, $BudgetUsd - $script:Spent).ToString('0.00', $Inv) } }
function Test-RateLimited([string]$a, [string]$b) { ($a + "`n" + $b) -match '(?i)usage limit|rate.?limit|limit reached|resets at|429|overloaded' }

# ---------- one claude process ----------
function Invoke-Claude([string]$label, [string]$prompt) {
  $json = "$Logs/$RunId-$label.json"; $err = "$Logs/$RunId-$label.err"
  $t0 = Get-Date
  Say "run: $prompt  (timeout $($TimeoutMin)m, budget left `$$(Get-RemainingUsd))"
  $argv = @('-p', $prompt, '--permission-mode', 'bypassPermissions', '--output-format', 'json', '--max-turns', $MaxTurns)
  if ((Get-RemainingUsd) -ne 'unlimited') { $argv += @('--max-budget-usd', (Get-RemainingUsd)) }
  $src = $claudeCmd.Source
  $psi = [System.Diagnostics.ProcessStartInfo]::new()
  switch -Regex ($src) {
    '\.ps1$'      { $psi.FileName = (Get-Process -Id $PID).Path; foreach ($a in @('-NoProfile', '-File', $src)) { $psi.ArgumentList.Add($a) } }
    '\.(cmd|bat)$' { $psi.FileName = $env:ComSpec; foreach ($a in @('/d', '/c', $src)) { $psi.ArgumentList.Add($a) } }
    default       { $psi.FileName = $src }
  }
  foreach ($a in $argv) { $psi.ArgumentList.Add([string]$a) }
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $outTask = $p.StandardOutput.ReadToEndAsync()
  $errTask = $p.StandardError.ReadToEndAsync()
  $timedOut = -not $p.WaitForExit([int]($TimeoutMin * 60000))
  if ($timedOut) { try { $p.Kill($true) } catch { Write-Verbose "kill failed: $_" }; $p.WaitForExit() }
  $stdout = $outTask.GetAwaiter().GetResult()
  Set-Content -LiteralPath $json -Value $stdout -NoNewline
  $stderr = $errTask.GetAwaiter().GetResult()
  Set-Content -LiteralPath $err -Value $stderr -NoNewline
  $script:Rc = if ($timedOut) { 124 } else { $p.ExitCode }
  $mins = ((Get-Date) - $t0).TotalMinutes.ToString('0.0', $Inv)

  $script:Cost = 0.0; $script:Result = ''
  try {
    $obj = $stdout | ConvertFrom-Json
    if ($obj.PSObject.Properties['total_cost_usd']) { $script:Cost = [double]$obj.total_cost_usd }
    if ($obj.PSObject.Properties['result'] -and $obj.result) {
      $script:Result = ((([string]$obj.result) -split "`r?`n") | Where-Object { $_ -match '^RESULT:' } | Select-Object -Last 1)
    }
  } catch {
    $m = [regex]::Matches($stdout, '"total_cost_usd"\s*:\s*([0-9.eE+-]+)')
    if ($m.Count -gt 0) { $script:Cost = [double]::Parse($m[$m.Count - 1].Groups[1].Value, $Inv) }
    $r = [regex]::Matches($stdout, 'RESULT: [^"\\]*')
    if ($r.Count -gt 0) { $script:Result = $r[$r.Count - 1].Value }
  }
  if (-not $script:Result) {
    $script:Result = if ($script:Rc -eq 124) { 'RESULT: stopped timeout' }
                     elseif (Test-RateLimited $stdout $stderr) { 'RESULT: stopped rate-limit' }
                     else { "RESULT: stopped no-result (exit $($script:Rc))" }
  }
  $script:Spent += $script:Cost
  $row = '| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |' -f $RunId, $script:Tasks, $label, (Iso $t0), $mins, $script:Cost.ToString($Inv), $script:Rc, ($script:Result -replace '^RESULT: ', '')
  Add-Content -LiteralPath $Ledger -Value $row
  Say ("→ {0} · `${1} · {2} min · spent `${3} / `${4}" -f $script:Result, $script:Cost.ToString($Inv), $mins, $script:Spent.ToString('0.####', $Inv), $BudgetUsd.ToString($Inv))
}

# ---------- GitHub helpers ----------
function Get-ChildIssue([string]$e) {
  $list = @()
  $raw = & $GhBin api "repos/{owner}/{repo}/issues/$e/sub_issues?per_page=100" 2>$null
  if ($LASTEXITCODE -eq 0 -and $raw) {
    foreach ($i in (($raw -join "`n") | ConvertFrom-Json)) {
      $list += [pscustomobject]@{ Number = [string]$i.number; State = ([string]$i.state).ToLower(); Labels = @($i.labels | ForEach-Object { $_.name }) }
    }
    if ($list.Count -gt 0) { return $list }
  }
  $body = & $GhBin issue view $e --json body --jq .body 2>$null
  $seen = @{}
  foreach ($m in [regex]::Matches(($body -join "`n"), '#(\d+)')) {
    $n = $m.Groups[1].Value
    if ($n -eq $e -or $seen.ContainsKey($n)) { continue }
    $seen[$n] = $true
    $iv = & $GhBin issue view $n --json 'state,labels' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $iv) { continue }
    $o = ($iv -join "`n") | ConvertFrom-Json
    $list += [pscustomobject]@{ Number = $n; State = ([string]$o.state).ToLower(); Labels = @($o.labels | ForEach-Object { $_.name }) }
  }
  return $list
}

function Test-OpenBlocker([string]$n) {
  $api = & $GhBin api "repos/{owner}/{repo}/issues/$n/dependencies/blocked_by" 2>$null
  if ($LASTEXITCODE -eq 0 -and $api) {
    $deps = ($api -join "`n") | ConvertFrom-Json
    if (@($deps | Where-Object { $_.state -eq 'open' }).Count -gt 0) { return $true }
  }
  $body = & $GhBin issue view $n --json body --jq .body 2>$null
  foreach ($m in [regex]::Matches(($body -join "`n"), '(?i)blocked by #(\d+)')) {
    $st = & $GhBin issue view $m.Groups[1].Value --json state --jq .state 2>$null
    if (([string]($st -join '')).Trim().ToLower() -eq 'open') { return $true }
  }
  return $false
}

function Get-TriesOf([string]$n) { @($Tries | Where-Object { $_ -eq $n }).Count }

function Get-NextStory {
  foreach ($c in (Get-ChildIssue $Epic)) {
    if ($c.State -ne 'open') { continue }
    if ($c.Labels -contains 'autopilot:blocked') { continue }
    if ((Get-TriesOf $c.Number) -ge $MaxAttempts) { continue }
    if (Test-OpenBlocker $c.Number) { continue }
    return $c.Number
  }
  return ''
}

function Get-OpenChildList {
  $s = foreach ($c in (Get-ChildIssue $Epic)) {
    if ($c.State -eq 'open') { '#' + $c.Number + $(if ($c.Labels -contains 'autopilot:blocked') { ' (blocked)' } else { '' }) }
  }
  return (@($s) -join ' ')
}

function Set-Blocked([string]$n, [string]$why) {
  & $GhBin issue edit $n --add-label 'autopilot:blocked' *> $null
  if ($LASTEXITCODE -ne 0) { Say "warning: could not label #$n" }
  & $GhBin issue comment $n --body "autopilot run $($RunId): blocked — $why. Logs: .process/autopilot/logs/$RunId-$n.*" *> $null
}

# ---------- base branch hygiene ----------
function Sync-Base {
  if (& git status --porcelain) {
    & git stash push -u -m "autopilot $RunId leftover after task $($script:Tasks)" *> $null
    Say 'stashed leftover changes (git stash list)'
  }
  & git switch -q $Base
  if ($LASTEXITCODE -eq 0) { & git pull -q --ff-only origin $Base }
  if ($LASTEXITCODE -ne 0) { $script:StopReason = "cannot switch/pull $Base"; return 'fail' }
  if ($VerifyMain -eq '1' -and (Test-Path 'scripts/verify.sh')) {
    $vlog = "$Logs/$RunId-verify-$($script:Tasks).log"
    & bash scripts/verify.sh --fast *> $vlog
    if ($LASTEXITCODE -ne 0) { $script:MainRedLog = $vlog; return 'red' }
  }
  return 'ok'
}

# ---------- repair main ----------
# Creates a "Repair main" issue and runs /feature on it until main is green. $false after max_attempts repairs.
function Invoke-RepairMain {
  while ($true) {
    $script:Repairs++
    if ($script:Repairs -gt $MaxAttempts) { $script:StopReason = "main still red after $MaxAttempts repair tasks — see $($script:MainRedLog)"; return $false }
    $sha = (& git rev-parse --short HEAD)
    $tail = if ($script:MainRedLog -and (Test-Path $script:MainRedLog)) { (Get-Content $script:MainRedLog -Tail 60) -join "`n" } else { '' }
    $body = "Autopilot: scripts/verify.sh --fast fails on $Base at $sha.`nFix main so verify passes. Smallest change; no feature work.`n`nLast lines of the failure:`n`n$tail"
    $url = (& $GhBin issue create --title "Repair main: verify failing at $sha" --body $body --label 'autopilot:repair' 2>$null) | Select-Object -Last 1
    $m = [regex]::Match([string]$url, '(\d+)$')
    if (-not $m.Success) { $script:StopReason = 'main red and could not create a repair issue'; return $false }
    $rn = $m.Groups[1].Value
    if ($Epic) {
      $id = (& $GhBin api "repos/{owner}/{repo}/issues/$rn" --jq .id 2>$null)
      if ($id) { & $GhBin api -X POST "repos/{owner}/{repo}/issues/$Epic/sub_issues" -F "sub_issue_id=$id" *> $null }
    }
    Say "main red at $sha → repair task #$rn (repair $($script:Repairs)/$MaxAttempts)"
    $script:Tasks++
    Invoke-Claude "repair-$rn" "/feature #$rn --autopilot --deadline $DeadlineIso"
    $r = [string](@(Sync-Base)[-1])
    if ($r -eq 'ok') { Say 'main green again'; return $true }
    if ($r -eq 'fail') { return $false }
  }
}

# Sync-BaseOrRepair → $true green · $false the run must end
function Sync-BaseOrRepair {
  $r = [string](@(Sync-Base)[-1])
  if ($r -eq 'red') { return [bool](@(Invoke-RepairMain)[-1]) }
  return ($r -eq 'ok')
}

# ---------- report ----------
function Write-Report {
  $end = Get-Date
  if (-not $script:StopReason) { $script:StopReason = 'interrupted' }
  $open = if ($Epic) { Get-OpenChildList } else { '' }
  $lines = @(
    "# Autopilot report — run $RunId", '',
    '| Field | Value |', '|---|---|',
    "| Epic | #$Epic |",
    "| Started / ended (UTC) | $(Iso $Start) / $(Iso $end) |",
    ("| Wall time | {0} h of {1} h |" -f ($end - $Start).TotalHours.ToString('0.00', $Inv), $BudgetHours.ToString($Inv)),
    ("| Cost (reported by claude) | `${0} (cap: {1}) |" -f $script:Spent.ToString('0.0000', $Inv), $(if ($BudgetUsd -le 0) { 'none' } else { '$' + $BudgetUsd.ToString($Inv) })),
    "| /feature runs | $($script:Tasks) (merged $($script:Merged) · blocked $($script:Blocked) · other $($script:Other)) |",
    "| Stop reason | $($script:StopReason) |",
    "| Still open | $open |", '',
    '## Runs', '',
    '| Run | # | Issue | Started (UTC) | Minutes | Cost $ | Exit | Result |',
    '|-----|---|-------|---------------|---------|--------|------|--------|'
  )
  $lines += @(Get-Content -LiteralPath $Ledger | Where-Object { $_.StartsWith("| $RunId |") })
  $lines += '', "Logs: ``$Logs/$RunId-*`` · per-task detail: ``.process/<issue>-<slug>/00-status.md`` and ``metrics.md``"
  Set-Content -LiteralPath $Report -Value $lines
  Say "stopped: $($script:StopReason) — report: $Report"
}

# ---------- run ----------
try {
  Say ("AUTOPILOT (POC) run {0} · base {1} · {3}h (deadline {4}) · attempts {5} · task timeout {6}m" -f $RunId, $Base, '', $BudgetHours.ToString($Inv), $DeadlineIso, $MaxAttempts, $TimeoutMin.ToString($Inv))
  & $GhBin label create 'autopilot:blocked' --color B60205 --description 'autopilot gave up on this issue' --force *> $null
  & $GhBin label create 'autopilot:repair' --color FBCA04 --description 'autopilot: fix a red main' --force *> $null

  if (-not (Sync-BaseOrRepair)) { return }

  if ($Prd) {
    if (-not (Test-Path $Prd)) { $script:StopReason = "no PRD at $Prd"; return }
    $script:Tasks++
    Invoke-Claude 'product' "/product $Prd --autopilot"
    $m = [regex]::Match($script:Result, 'epic #(\d+)')
    if (-not $m.Success) { $script:StopReason = "/product did not create an epic: $($script:Result)"; return }
    $Epic = $m.Groups[1].Value
    Say "epic #$Epic"
    if (-not (Sync-BaseOrRepair)) { return }
  }

  while ($true) {
    if ((Get-Date) -ge $Deadline) { $script:StopReason = "hours budget reached ($($BudgetHours)h)"; break }
    if ($BudgetUsd -gt 0 -and $script:Spent -ge $BudgetUsd) { $script:StopReason = "usd budget reached (`$$($script:Spent) of `$$BudgetUsd)"; break }
    if ($MaxTasks -gt 0 -and $script:Tasks -ge $MaxTasks) { $script:StopReason = "-MaxTasks $MaxTasks reached"; break }

    $n = Get-NextStory
    if (-not $n) {
      $left = Get-OpenChildList
      $script:StopReason = if (-not $left) { 'all done' } else { "nothing runnable (open: $left)" }
      break
    }

    $script:Tasks++
    $Tries.Add($n)
    Invoke-Claude $n "/feature #$n --autopilot --deadline $DeadlineIso"
    $res = $script:Result
    $short = $res -replace '^RESULT: ', ''

    if ($res -eq 'RESULT: stopped rate-limit') {
      # subscription usage limit: not the task's fault — undo the attempt, wait, retry the same task
      [void]$Tries.Remove($n)
      Say "usage limit hit — sleeping $($RateWaitMin)m, then retrying #$n"
      Start-Sleep -Seconds ($RateWaitMin * 60)
      if (-not (Sync-BaseOrRepair)) { break }
      continue
    }

    if ($res -match '^RESULT: (merged|done)') {
      $script:Merged++; $script:Consec = 0
      $Tries.RemoveAll([Predicate[string]] { param($x) $x -eq $n }) | Out-Null
    } elseif ($res -match '^RESULT: blocked') {
      $script:Blocked++; $script:Consec++
      Set-Blocked $n ($res -replace '^RESULT: blocked ', '')
      if ($OnBlocked -eq 'stop') { $script:StopReason = "#$n blocked and on_blocked: stop — $short"; break }
    } elseif ($res -match '^RESULT: stopped main-red') {
      $script:Other++; $script:MainRedLog = "$Logs/$RunId-$n.json"
      if (-not [bool](@(Invoke-RepairMain)[-1])) { break }
    } elseif ($res -match '^RESULT: stopped deadline') {
      $script:Other++; $script:StopReason = "deadline reached inside #$n"; break
    } elseif ($res -match '^RESULT: stopped autopilot-off') {
      $script:Other++; $script:StopReason = '/feature says autopilot is off'; break
    } else {
      $script:Other++; $script:Consec++
      $t = Get-TriesOf $n
      if ($t -ge $MaxAttempts) { Set-Blocked $n "runner: $t non-merged runs ($short)"; $script:Blocked++ }
    }

    # no stop on consecutive failures: blocked tasks are skipped, the run keeps going
    if (-not (Sync-BaseOrRepair)) { break }
  }
} finally {
  Write-Report
}
