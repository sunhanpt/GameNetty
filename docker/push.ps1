# 构建 linux/amd64 镜像并推送到腾讯云容器镜像服务个人版（广州）
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$EnvFile = Join-Path $PSScriptRoot "tcr.env"
$DockerExe = Join-Path $env:LOCALAPPDATA "Programs\DockerDesktop\resources\bin\docker.exe"
$PluginDir = Join-Path $env:LOCALAPPDATA "Programs\DockerDesktop\resources\cli-plugins"

if (-not (Test-Path $DockerExe)) {
    throw "找不到 Docker CLI: $DockerExe 。请安装并启动 Docker Desktop。"
}

$env:DOCKER_CLI_PLUGIN_EXTRA_DIRS = $PluginDir

Get-Content $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq "" -or $line.StartsWith("#")) { return }
    $pair = $line.Split("=", 2)
    if ($pair.Length -ne 2) { return }
    Set-Item -Path "Env:$($pair[0].Trim())" -Value $pair[1].Trim()
}

function Invoke-Docker {
    param([string[]]$DockerArgs)
    Write-Host "> docker $($DockerArgs -join ' ')"
    $p = Start-Process -FilePath $DockerExe -ArgumentList $DockerArgs -Wait -PassThru -NoNewWindow
    if ($null -eq $p -or $p.ExitCode -ne 0) {
        $code = if ($null -eq $p) { "null" } else { $p.ExitCode }
        throw "docker 失败 (exit $code): $($DockerArgs -join ' ')"
    }
}

$Image = "$($env:TCR_REGISTRY)/$($env:TCR_NAMESPACE)/$($env:TCR_REPOSITORY):$($env:TCR_TAG)"
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$StampImage = "$($env:TCR_REGISTRY)/$($env:TCR_NAMESPACE)/$($env:TCR_REPOSITORY):$Stamp"

Write-Host "Login: $($env:TCR_REGISTRY)  user=$($env:TCR_USERNAME)  region=$($env:TCR_REGION)"

if (-not $env:TCR_PASSWORD) {
    throw "未设置 TCR_PASSWORD。请用 DockerPush.bat，或先 `$env:TCR_PASSWORD = '密码'`"
}

$loginArgs = @(
    "login", $env:TCR_REGISTRY,
    "-u", $env:TCR_USERNAME,
    "--password-stdin"
)
$loginInfo = New-Object System.Diagnostics.ProcessStartInfo
$loginInfo.FileName = $DockerExe
$loginInfo.Arguments = ($loginArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
$loginInfo.UseShellExecute = $false
$loginInfo.RedirectStandardInput = $true
$loginInfo.RedirectStandardOutput = $false
$loginInfo.RedirectStandardError = $false
$proc = [System.Diagnostics.Process]::Start($loginInfo)
$proc.StandardInput.WriteLine($env:TCR_PASSWORD)
$proc.StandardInput.Close()
$proc.WaitForExit()
if ($proc.ExitCode -ne 0) {
    throw "docker login 失败 (exit $($proc.ExitCode))"
}

Write-Host "Build $Image"
Invoke-Docker @("build", "-t", $Image, "-t", $StampImage, $RepoRoot)

Write-Host "Push $Image"
Invoke-Docker @("push", $Image)
Invoke-Docker @("push", $StampImage)

Write-Host "SUCCESS"
Write-Host "  $Image"
Write-Host "  $StampImage"
