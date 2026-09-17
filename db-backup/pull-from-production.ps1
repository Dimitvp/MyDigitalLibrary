# Pulls the newest production database backup down to this folder — the
# one genuinely off-site copy of the live data (everything else, including
# Hetzner's own weekly VM snapshot, still lives on/behind the same Hetzner
# account). Safe to run repeatedly: skips the download if the newest
# production file is already present locally by name (timestamps are
# unique, so "already have this filename" == "already have this snapshot").
#
# Reuses the existing local disaster-recovery path for free: if this
# machine's own `pgdata` volume is ever wiped, docker/db-init/10-restore-if-exists.sh
# restores from whichever .dump in this folder sorts newest by filename —
# a pulled-down production snapshot included, no separate restore logic
# needed.
#
# Registered as a daily Windows Scheduled Task — see db-backup/README.md.

$ErrorActionPreference = 'Stop'

$Server = 'root@46.62.253.87'
$RemoteDir = '/opt/biblioteka/db-backup'
$LocalDir = $PSScriptRoot
$LogFile = Join-Path $LocalDir 'pull-from-production.log'

function Write-Log {
    param([string]$Message)
    $line = "$(Get-Date -Format o) $Message"
    Add-Content -Path $LogFile -Value $line
    Write-Output $line
}

try {
    $latestRemotePath = (ssh $Server "ls -1 $RemoteDir/mydigitallibrary_*.dump 2>/dev/null | sort | tail -n 1").Trim()

    if ([string]::IsNullOrWhiteSpace($latestRemotePath)) {
        Write-Log 'No backup files found on production — nothing to pull.'
        exit 0
    }

    $fileName = Split-Path $latestRemotePath -Leaf
    $localPath = Join-Path $LocalDir $fileName

    if (Test-Path $localPath) {
        Write-Log "Already have the newest production snapshot ($fileName) — nothing to do."
        exit 0
    }

    Write-Log "Pulling $fileName from production..."
    scp "${Server}:${latestRemotePath}" $LocalDir
    if ($LASTEXITCODE -ne 0) {
        throw "scp exited with code $LASTEXITCODE"
    }

    Write-Log "Pulled $fileName successfully."
}
catch {
    Write-Log "ERROR: $_"
    exit 1
}
