#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ProjectPath = "src/MitmiCsv/MitmiCsv.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$WorkingRoot,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForHttpText {
    param(
        [Parameter(Mandatory = $true)]
        [System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedStatusCode,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedText
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
    $lastError = $null

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = $Client.GetAsync($Uri).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ([int]$response.StatusCode -eq $ExpectedStatusCode -and $body.Contains($ExpectedText)) {
                return
            }

            $lastError = "Unexpected response from $Uri`: status $([int]$response.StatusCode), body '$body'"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for $Uri. Last error: $lastError"
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedProjectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ProjectPath))
$hasProvidedWorkingRoot = $PSBoundParameters.ContainsKey("WorkingRoot") -and ![string]::IsNullOrWhiteSpace($WorkingRoot)

if ($hasProvidedWorkingRoot) {
    $workingRootPath = [System.IO.Path]::GetFullPath($WorkingRoot)
}
else {
    $workingRootPath = Join-Path ([System.IO.Path]::GetTempPath()) ("mitmi-csv-release-smoke-" + [Guid]::NewGuid().ToString("N"))
}

if (Test-Path -LiteralPath $workingRootPath) {
    $existingChild = Get-ChildItem -LiteralPath $workingRootPath -Force | Select-Object -First 1
    if ($existingChild) {
        throw "Working root '$workingRootPath' must be empty."
    }
}
else {
    New-Item -ItemType Directory -Path $workingRootPath | Out-Null
}

$publishDirectory = Join-Path $workingRootPath "publish"
$runDirectory = Join-Path $workingRootPath "run"
New-Item -ItemType Directory -Path $publishDirectory, $runDirectory | Out-Null

$succeeded = $false
$process = $null
try {
    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("publish", $resolvedProjectPath, "--configuration", $Configuration, "--runtime", $RuntimeIdentifier, "--self-contained", "false", "--output", $publishDirectory) `
        -WorkingDirectory $repoRoot

    $executablePath = Join-Path $publishDirectory "mitmi-csv.exe"
    if (!(Test-Path -LiteralPath $executablePath)) {
        throw "Published mitmi-csv executable was not found in '$publishDirectory'."
    }

    $port = Get-FreeTcpPort
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executablePath
    $startInfo.WorkingDirectory = $runDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://127.0.0.1:$port"

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start published mitmi-csv executable."
    }

    Add-Type -AssemblyName System.Net.Http
    $client = [System.Net.Http.HttpClient]::new()
    try {
        Wait-ForHttpText -Client $client -Uri "http://127.0.0.1:$port/" -ExpectedStatusCode 200 -ExpectedText "GET /read"
        Wait-ForHttpText -Client $client -Uri "http://127.0.0.1:$port/read" -ExpectedStatusCode 400 -ExpectedText "Missing required parameter 'host'."
    }
    finally {
        $client.Dispose()
    }

    $succeeded = $true
    Write-Host "Release smoke test passed."
    if ($KeepArtifacts -or $hasProvidedWorkingRoot) {
        Write-Host "Artifacts: $workingRootPath"
    }
}
finally {
    if ($null -ne $process -and !$process.HasExited) {
        $process.Kill()
        $process.WaitForExit(5000) | Out-Null
    }

    if ($succeeded -and !$KeepArtifacts -and !$hasProvidedWorkingRoot) {
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $resolvedWorkingRoot = [System.IO.Path]::GetFullPath($workingRootPath)
        $leafName = Split-Path -Path $resolvedWorkingRoot -Leaf
        if ($resolvedWorkingRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and $leafName.StartsWith("mitmi-csv-release-smoke-", [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedWorkingRoot -Recurse -Force
        }
    }

    if (!$succeeded) {
        Write-Warning "Release smoke artifacts were left at '$workingRootPath'."
    }
}
