# Run from the Rider "Deploy ScheduleBot (Production)" configuration.
[CmdletBinding()]
param(
    [switch]$PublishOnly,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
if ($PublishOnly -and $PreflightOnly) {
    throw 'Choose only one of -PublishOnly and -PreflightOnly.'
}

$repoDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$linuxScript = Join-Path $PSScriptRoot 'deploy-schedule-bot.sh'
$releaseFiles = Join-Path $PSScriptRoot 'schedule-bot-release-files.py'
$target = 'alireza@18.221.201.190'
$sshOptions = @('-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes',
    '-o', 'UpdateHostKeys=no', '-o', 'ConnectTimeout=10',
    '-o', 'ServerAliveInterval=15', '-o', 'ServerAliveCountMax=3')
# Only preflight reads stdin. The other SSH commands must leave Rider's input pipe alone.
$sshCommandOptions = @('-n', '-T', '-p', '22') + $sshOptions

function Invoke-Checked {
    param([string]$Program, [string[]]$Arguments)

    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Program exited with code $LASTEXITCODE."
    }
}

if (-not $PublishOnly) {
    Write-Host 'Checking the existing production service, runtime, proxy, and health endpoints...'
    Get-Content -LiteralPath $linuxScript -Raw |
        ssh.exe -T -p 22 @sshOptions $target "tr -d '\r' | bash -s -- --check"
    if ($LASTEXITCODE -ne 0) {
        throw 'Production preflight failed. Check the server output and your SSH key and known_hosts.'
    }
}
if ($PreflightOnly) { return }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('schedule-bot-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'
$publish = Join-Path $temp 'publish'
$manifest = Join-Path $temp 'publish.json'
$delta = Join-Path $temp 'delta.tar.gz'
$utf8 = [Text.UTF8Encoding]::new($false)
$stage = $null
$handedOff = $false

try {
    New-Item -ItemType Directory -Path $source | Out-Null
    $baseRevision = Invoke-Checked git.exe @('-C', $repoDirectory, 'rev-parse', 'HEAD')
    Write-Host "Publishing saved working-tree files (base commit $baseRevision, Release, linux-x64, framework-dependent)..."

    # Copy the actual files from disk so saved changes and new project files are included.
    # Git's ignore rules keep local build outputs and Telegram session files out of the snapshot.
    $previousEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = $utf8
        $listedPaths = Invoke-Checked git.exe @('-C', $repoDirectory, 'ls-files', '--cached',
            '--others', '--exclude-standard', '--deduplicate', '-z')
    }
    finally {
        [Console]::OutputEncoding = $previousEncoding
    }
    $paths = ($listedPaths -join [char]10).Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)
    foreach ($path in $paths) {
        $original = Join-Path $repoDirectory $path
        if ([IO.File]::Exists($original)) {
            $destination = Join-Path $source $path
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.File]::Copy($original, $destination)
        }
    }

    Push-Location $source
    try {
        Invoke-Checked dotnet.exe @('publish', 'ScheduleBot/ScheduleBot.csproj', '-c', 'Release',
            '-r', 'linux-x64', '--self-contained', 'false', '-o', $publish)
    }
    finally {
        Pop-Location
    }

    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -Force -File | ForEach-Object {
        @{
            Path = $_.FullName.Substring($publish.Length + 1).Replace('\', '/')
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    $directories = @(Get-ChildItem -LiteralPath $publish -Recurse -Force -Directory | ForEach-Object {
        $_.FullName.Substring($publish.Length + 1).Replace('\', '/')
    })
    if ($files.Count -eq 0) { throw 'The publish directory is empty.' }
    $inventory = @{ Files = $files; Directories = $directories } | ConvertTo-Json -Depth 4 -Compress
    [IO.File]::WriteAllText($manifest, $inventory, $utf8)
    Write-Host "Inventoried $($files.Count) published files."
    if ($PublishOnly) { return }

    Write-Host 'Creating a private staging directory over SSH...'
    $stage = Invoke-Checked ssh.exe ($sshCommandOptions + @($target,
        'umask 077; mktemp -d /home/alireza/dotnet/.schedule-bot-stage.XXXXXXXX'))
    if ($stage -cnotmatch '^/home/alireza/dotnet/\.schedule-bot-stage\.[a-zA-Z0-9]{8}$') {
        throw 'SSH returned an unexpected staging path.'
    }

    $remote = "${target}:$stage/"
    Copy-Item -LiteralPath @($linuxScript, $releaseFiles) -Destination $temp
    Push-Location $temp
    try {
        Write-Host "Uploading the publish inventory to $stage ..."
        Invoke-Checked scp.exe (@('-P', '22') + $sshOptions +
            @('publish.json', 'deploy-schedule-bot.sh', 'schedule-bot-release-files.py', $remote))
        Write-Host 'Comparing the publish inventory with the installed release...'
        $plan = Invoke-Checked ssh.exe ($sshCommandOptions +
            @($target, "python3 '$stage/schedule-bot-release-files.py' plan '$stage' /home/alireza/dotnet/telegramBot"))
        $needed = @(($plan | ConvertFrom-Json).Files)
        if ($needed.Count -gt 0) {
            $list = Join-Path $temp 'delta-files'
            [IO.File]::WriteAllText($list, (($needed -join [char]0) + [char]0), $utf8)
            Invoke-Checked tar.exe @('-czf', $delta, '-C', $publish, '--null', '-T', $list)
            Write-Host "Uploading $($needed.Count) new or changed publish files ($((Get-Item -LiteralPath $delta).Length) bytes compressed)."
            Invoke-Checked scp.exe (@('-P', '22') + $sshOptions + @('delta.tar.gz', $remote))
        } else {
            Write-Host 'The server already has all published file contents. Skipping the payload upload.'
        }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Running the server deployment. Server output follows after verification or rollback.'
    $handedOff = $true
    # The cutover keeps running if the IDE or SSH connection closes. Its log stays on the server on failure.
    $command = "cd '$stage' && umask 077 && tr -d '\r' < deploy-schedule-bot.sh > deploy-linux.sh && " +
        "(nohup bash deploy-linux.sh '$stage' '$baseRevision' > deploy.log 2>&1 < /dev/null; " +
        'result=$?; cat deploy.log; if [ "$result" -eq 0 ]; then ' +
        "rm -f -- publish.json delta.tar.gz deploy-schedule-bot.sh deploy-linux.sh schedule-bot-release-files.py deploy.log; cd ..; rmdir -- '$stage'; " +
        'fi; exit "$result")'
    Invoke-Checked ssh.exe ($sshCommandOptions + @($target, $command))
}
catch {
    if ($handedOff) {
        Write-Warning "Check $stage/deploy.log on the server before retrying an interrupted deployment."
    }
    throw
}
finally {
    if (-not $handedOff -and $stage -cmatch '^/home/alireza/dotnet/\.schedule-bot-stage\.[a-zA-Z0-9]{8}$') {
        & ssh.exe @sshCommandOptions $target "cd '$stage' && rm -f -- publish.json delta.tar.gz deploy-schedule-bot.sh schedule-bot-release-files.py && cd .. && rmdir -- '$stage'"
    }
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
