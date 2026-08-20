[CmdletBinding()]
param(
    [string]$DeviceSerial,
    [switch]$BackendOnly,
    [switch]$ReuseBackend
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repositoryRoot 'WorkoutPlanner.Web\WorkoutPlanner.Web.csproj'
$mobileProject = Join-Path $repositoryRoot 'GymPlanner.Mobile\GymPlanner.Mobile.csproj'
$androidSdkCandidates = @(
    $env:ANDROID_HOME,
    $env:ANDROID_SDK_ROOT,
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
    'C:\Program Files (x86)\Android\android-sdk'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

function Get-AdbPath {
    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    foreach ($sdkPath in $androidSdkCandidates) {
        $candidate = Join-Path $sdkPath 'platform-tools\adb.exe'
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'Android SDK Platform Tools не найден. Установите Android workload / SDK или задайте ANDROID_HOME.'
}

function Get-ConnectedDevice([string]$adbPath, [string]$requestedSerial) {
    $devices = @(& $adbPath devices | Select-Object -Skip 1 |
        ForEach-Object {
            $parts = $_ -split '\s+'
            if ($parts.Count -ge 2 -and $parts[1] -eq 'device') { $parts[0] }
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if (-not [string]::IsNullOrWhiteSpace($requestedSerial)) {
        if ($requestedSerial -notin $devices) {
            throw "Устройство '$requestedSerial' не подключено или не разрешило USB-отладку."
        }
        return $requestedSerial
    }

    if ($devices.Count -eq 0) {
        throw 'Android-устройство не найдено. Подключите телефон и подтвердите USB-отладку.'
    }
    if ($devices.Count -gt 1) {
        throw "Найдено несколько устройств: $($devices -join ', '). Повторите с -DeviceSerial <серийный_номер>."
    }
    return $devices[0]
}

function Get-ApiListener {
    Get-NetTCPConnection -LocalPort 5121 -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1
}

function Wait-ForApi {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        try {
            $response = Invoke-WebRequest -Uri 'http://127.0.0.1:5121/api/v1/profile' `
                -UseBasicParsing -MaximumRedirection 0 -SkipHttpErrorCheck
            if ($response.StatusCode -in 200, 401, 403) {
                return
            }
        }
        catch {
            # API is still starting.
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw 'Backend не стал доступен на http://127.0.0.1:5121 за 60 секунд.'
}

Push-Location $repositoryRoot
try {
    $listener = Get-ApiListener
    if ($listener -and -not $ReuseBackend) {
        $process = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
        if ($process.ProcessName -ne 'WorkoutPlanner.Web') {
            throw "Порт 5121 занят процессом '$($process.ProcessName)'. Используйте -ReuseBackend или освободите порт."
        }
        Write-Host 'Останавливаем предыдущий экземпляр backend…' -ForegroundColor Yellow
        Stop-Process -Id $process.Id
        Start-Sleep -Milliseconds 500
        $listener = $null
    }

    if ($null -eq $listener) {
        Write-Host 'Собираем backend…' -ForegroundColor Cyan
        & dotnet build $webProject -c Debug
        if ($LASTEXITCODE -ne 0) { throw 'Сборка backend завершилась ошибкой.' }

        Write-Host 'Запускаем backend…' -ForegroundColor Cyan
        $backendArguments = "run --project `"$webProject`" --no-build --launch-profile http"
        Start-Process -FilePath dotnet -ArgumentList $backendArguments `
            -WorkingDirectory $repositoryRoot -WindowStyle Hidden
        Wait-ForApi
    }

    Write-Host 'Backend готов: http://127.0.0.1:5121' -ForegroundColor Green
    if ($BackendOnly) { return }

    $adbPath = Get-AdbPath
    $device = Get-ConnectedDevice $adbPath $DeviceSerial
    $androidSdkPath = Split-Path -Parent (Split-Path -Parent $adbPath)
    Write-Host "Используем Android-устройство: $device" -ForegroundColor Cyan

    Write-Host 'Собираем Android-приложение…' -ForegroundColor Cyan
    & dotnet build $mobileProject -f net10.0-android -c Debug `
        "-p:AndroidSdkDirectory=$androidSdkPath"
    if ($LASTEXITCODE -ne 0) { throw 'Сборка Android-приложения завершилась ошибкой.' }

    $apkPath = Join-Path $repositoryRoot 'GymPlanner.Mobile\bin\Debug\net10.0-android\android-arm64\com.gymplanner.mobile-Signed.apk'
    if (-not (Test-Path -LiteralPath $apkPath)) {
        throw "Не найден APK: $apkPath"
    }

    Write-Host 'Настраиваем USB-проброс и устанавливаем приложение…' -ForegroundColor Cyan
    & $adbPath -s $device reverse tcp:5121 tcp:5121
    & $adbPath -s $device install -r $apkPath
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось установить Android-приложение.' }

    & $adbPath -s $device shell am force-stop com.gymplanner.mobile
    & $adbPath -s $device shell monkey -p com.gymplanner.mobile 1 | Out-Null
    Write-Host 'GPlanner запущен на телефоне.' -ForegroundColor Green
}
finally {
    Pop-Location
}
