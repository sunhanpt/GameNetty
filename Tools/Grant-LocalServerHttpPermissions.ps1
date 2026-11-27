[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Account = [Security.Principal.WindowsIdentity]::GetCurrent().Name
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $WhatIfPreference -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as administrator to configure the local server URL reservations.'
}

$sid = ([Security.Principal.NTAccount]::new($Account)).Translate([Security.Principal.SecurityIdentifier]).Value
$sddl = "D:(A;;GX;;;$sid)"
$netsh = Join-Path $env:SystemRoot 'System32\netsh.exe'
$failedUrls = @()

# HTTP and WebSocket ports used by StartConfig/Localhost, process 1.
foreach ($port in 30002, 30003, 30004, 30300, 30301, 30302, 30303, 30304) {
    $url = "http://127.0.0.1:${port}/"
    if (-not $PSCmdlet.ShouldProcess($url, "Grant listener permission to $Account")) {
        continue
    }

    $reservation = & $netsh http show urlacl "url=$url"
    if ($LASTEXITCODE -eq 0 -and ($reservation -join "`n").Contains($sddl)) {
        Write-Output "Already configured: $url"
        continue
    }

    & $netsh http add urlacl "url=$url" "sddl=$sddl"
    if ($LASTEXITCODE -ne 0) {
        $failedUrls += $url
    }
}

if ($failedUrls.Count -gt 0) {
    throw "Could not configure $($failedUrls -join ', '). Check the netsh output and any existing reservations for those URLs."
}
