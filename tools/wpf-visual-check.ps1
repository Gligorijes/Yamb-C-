$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

public static class Native {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    public static List<IntPtr> WindowsOf(uint pid) {
        var result = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint wpid;
            GetWindowThreadProcessId(h, out wpid);
            if (wpid == pid && IsWindowVisible(h)) result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static string Title(IntPtr h) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        return sb.ToString();
    }
}
"@

[void][Native]::SetProcessDPIAware()

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'src\Yamb.Presentation.Wpf\bin\Release\net8.0-windows\Yamb.Presentation.Wpf.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'Yamb'
$backup = Join-Path $env:TEMP ("yamb-visual-backup-" + [Guid]::NewGuid().ToString('N'))
$shotDir = Join-Path $env:TEMP 'yamb-visual-shots'
$graphicsKey = 'HKCU:\Software\Microsoft\Avalon.Graphics'
$hwAccelName = 'DISABLEHWACCELERATION'
$hwAccelProperty = Get-ItemProperty -Path $graphicsKey -Name $hwAccelName -ErrorAction SilentlyContinue
$hadHwAccelProperty = $null -ne $hwAccelProperty
$oldHwAccelValue = if ($hadHwAccelProperty) { $hwAccelProperty.$hwAccelName } else { $null }

function Get-AppWindows([int]$procId) {
    [Native]::WindowsOf([uint32]$procId) | ForEach-Object {
        [pscustomobject]@{ Hwnd = [long]$_; Title = [Native]::Title($_) }
    }
}
function Find-Window([int]$procId, [string]$title) {
    Get-AppWindows $procId | Where-Object { $_.Title -eq $title } | Select-Object -First 1
}
function Wait-Window([int]$procId, [string]$title, [int]$seconds = 15) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $w = Find-Window $procId $title
        if ($w) { return $w }
        Start-Sleep -Milliseconds 150
    }
    return $null
}
function Find-ById($element, [string]$id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Find-ByName($element, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function Invoke-El($el) {
    if (-not $el) { throw 'cannot invoke null element' }
    $pat = $null
    if (-not $el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { throw 'no invoke pattern' }
    $pat.Invoke()
}
function Invoke-ById($element, [string]$id) {
    $el = Find-ById $element $id
    if (-not $el) { throw "element not found by id: $id" }
    Invoke-El $el
}
function Select-El($el) {
    if (-not $el) { throw 'cannot select null element' }
    $pat = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pat)) { $pat.Select(); return }
    if ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pat)) { $pat.Toggle(); return }
    if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke(); return }
    throw 'no selectable pattern'
}
function Wait-El([scriptblock]$finder, [int]$seconds = 10) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $el = & $finder
        if ($el) { return $el }
        Start-Sleep -Milliseconds 150
    }
    return $null
}
function Save-Shot([long]$hwnd, [string]$path) {
    $r = New-Object RECT
    [void][Native]::GetWindowRect([IntPtr]$hwnd, [ref]$r)
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    return @($r.Left, $r.Top, $w, $h)
}
function Get-AverageBrightness([string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $path
    $sum = 0.0
    $count = 0
    for ($y = 0; $y -lt $bmp.Height; $y += 6) {
        for ($x = 0; $x -lt $bmp.Width; $x += 6) {
            $c = $bmp.GetPixel($x, $y)
            $sum += ($c.R + $c.G + $c.B) / 3.0
            $count++
        }
    }
    $bmp.Dispose()
    return [math]::Round($sum / [math]::Max($count, 1), 1)
}
function Measure-TextPixels([string]$path, $el, [long]$winLeft, [long]$winTop, [bool]$darkTheme) {
    $bmp = New-Object System.Drawing.Bitmap $path
    $r = $el.Current.BoundingRectangle
    $x0 = [int]($r.X - $winLeft) + 8
    $y0 = [int]($r.Y - $winTop) + 6
    $x1 = [int]($r.Right - $winLeft) - 8
    $y1 = [int]($r.Bottom - $winTop) - 6
    $hits = 0
    for ($y = [math]::Max($y0, 0); $y -lt [math]::Min($y1, $bmp.Height); $y++) {
        for ($x = [math]::Max($x0, 0); $x -lt [math]::Min($x1, $bmp.Width); $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($darkTheme -and $c.R -gt 170 -and $c.G -gt 170 -and $c.B -gt 170) { $hits++ }
            if (-not $darkTheme -and $c.R -lt 90 -and $c.G -lt 90 -and $c.B -lt 90) { $hits++ }
        }
    }
    $bmp.Dispose()
    return $hits
}

if (Test-Path $backup) { Remove-Item -Recurse -Force $backup }
if (Test-Path $dataDir) { Move-Item -LiteralPath $dataDir -Destination $backup }
if (Test-Path $shotDir) { Remove-Item -Recurse -Force $shotDir }
New-Item -ItemType Directory -Path $shotDir | Out-Null

New-ItemProperty -Path $graphicsKey -Name $hwAccelName -PropertyType DWord -Value 1 -Force | Out-Null
$p = Start-Process -FilePath $exe -PassThru
$ok = $false
try {
    $main = Wait-Window $p.Id 'Jamb'
    if (-not $main) { throw 'main window did not appear' }
    [void][Native]::ShowWindow([IntPtr]$main.Hwnd, 9)
    [void][Native]::SetForegroundWindow([IntPtr]$main.Hwnd)
    Start-Sleep -Milliseconds 800
    $mainEl = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$main.Hwnd)

    Invoke-ById $mainEl 'MenuNewGame'
    if (-not (Wait-El { Find-ById $mainEl 'ColumnSeven' })) { throw 'column step missing' }
    Invoke-ById $mainEl 'NewGameNext'
    if (-not (Wait-El { Find-ById $mainEl 'ModeSingle' })) { throw 'player step missing' }
    Invoke-ById $mainEl 'NewGameNext'
    $start = Wait-El { Find-ById $mainEl 'StartGame' }
    if (-not $start) { throw 'new game view missing' }

    $nameBox = Find-ById $mainEl 'Player1Name'
    $nameBox.SetFocus()
    Start-Sleep -Milliseconds 300
    $vp = $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue('Aleksandar')
    Start-Sleep -Milliseconds 500

    $shot = Join-Path $shotDir '01-newgame-dark.png'
    $rect = Save-Shot $main.Hwnd $shot
    $hits = Measure-TextPixels $shot $nameBox $rect[0] $rect[1] $true
    Write-Host "name value: $($vp.Current.Value)"
    Write-Host "dark textbox bright text pixels: $hits"
    Write-Host "dark window brightness: $(Get-AverageBrightness $shot)"

    Invoke-ById $mainEl 'StartGame'
    if (-not (Wait-El { Find-ById $mainEl 'RollButton' })) { throw 'game view missing' }
    Invoke-ById $mainEl 'RollButton'
    Start-Sleep -Milliseconds 1200
    Invoke-ById $mainEl 'Cell_0_Downward_Ones'
    Start-Sleep -Milliseconds 700
    $gameDark = Join-Path $shotDir '02-game-dark.png'
    [void](Save-Shot $main.Hwnd $gameDark)
    $name0 = Find-ById $mainEl 'PlayerBarName0'
    Write-Host "player bar name: $($name0.Current.Name)"

    Invoke-ById $mainEl 'MenuButton'
    Start-Sleep -Milliseconds 400
    $yes = Wait-El { Find-ByName $mainEl 'Da' }
    if ($yes) { Invoke-El $yes }
    Start-Sleep -Milliseconds 600
    Invoke-ById $mainEl 'MenuSettings'
    $light = Wait-El { Find-ById $mainEl 'ThemeLight' }
    if (-not $light) { throw 'theme light radio missing' }
    Select-El $light
    Start-Sleep -Milliseconds 900
    $settingsLight = Join-Path $shotDir '03-settings-light.png'
    [void](Save-Shot $main.Hwnd $settingsLight)
    $settingsJson = Join-Path $dataDir 'settings.json'
    if (Test-Path $settingsJson) { Write-Host "settings: $(Get-Content -Raw $settingsJson)" }

    Invoke-ById $mainEl 'SettingsBack'
    Start-Sleep -Milliseconds 500
    Invoke-ById $mainEl 'MenuNewGame'
    if (-not (Wait-El { Find-ById $mainEl 'ColumnSeven' })) { throw 'column step missing after theme switch' }
    Invoke-ById $mainEl 'NewGameNext'
    if (-not (Wait-El { Find-ById $mainEl 'ModeSingle' })) { throw 'player step missing after theme switch' }
    Invoke-ById $mainEl 'NewGameNext'
    $lightStart = Wait-El { Find-ById $mainEl 'StartGame' }
    if (-not $lightStart) { throw 'new game view missing after theme switch' }
    $nameBox2 = Find-ById $mainEl 'Player1Name'
    $nameBox2.SetFocus()
    $vp2 = $nameBox2.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp2.SetValue('SvetlaTema')
    Start-Sleep -Milliseconds 400
    $newLight = Join-Path $shotDir '04-newgame-light.png'
    $rect2 = Save-Shot $main.Hwnd $newLight
    $hits2 = Measure-TextPixels $newLight $nameBox2 $rect2[0] $rect2[1] $false
    Write-Host "light textbox dark text pixels: $hits2"
    Write-Host "light window brightness: $(Get-AverageBrightness $newLight)"
    Invoke-ById $mainEl 'StartGame'
    if (-not (Wait-El { Find-ById $mainEl 'RollButton' })) { throw 'game view missing in light theme' }
    Invoke-ById $mainEl 'RollButton'
    Start-Sleep -Milliseconds 1200
    Invoke-ById $mainEl 'Cell_0_Downward_Ones'
    Start-Sleep -Milliseconds 700
    $gameLight = Join-Path $shotDir '05-game-light.png'
    [void](Save-Shot $main.Hwnd $gameLight)

    $before = New-Object RECT
    [void][Native]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$before)
    Invoke-ById $mainEl 'WindowFullscreen'
    Start-Sleep -Milliseconds 1000
    $fs = New-Object RECT
    [void][Native]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$fs)
    $screenW = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
    $screenH = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
    Write-Host "screen: ${screenW}x${screenH} fullscreen: $($fs.Right - $fs.Left)x$($fs.Bottom - $fs.Top)"
    $full = Join-Path $shotDir '06-fullscreen-light.png'
    [void](Save-Shot $main.Hwnd $full)
    $isFull = ($fs.Right - $fs.Left) -ge ($screenW - 12) -and ($fs.Bottom - $fs.Top) -ge ($screenH - 12)
    Write-Host "fullscreen size reached: $isFull"
    Invoke-ById $mainEl 'WindowFullscreen'
    Start-Sleep -Milliseconds 900
    $after = New-Object RECT
    [void][Native]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$after)
    $restored = [math]::Abs(($after.Right - $after.Left) - ($before.Right - $before.Left)) -le 4 -and [math]::Abs(($after.Bottom - $after.Top) - ($before.Bottom - $before.Top)) -le 4
    Write-Host "window restored: $restored"

    Invoke-ById $mainEl 'MenuButton'
    Start-Sleep -Milliseconds 400
    $yes2 = Wait-El { Find-ByName $mainEl 'Da' }
    if ($yes2) { Invoke-El $yes2 }
    Start-Sleep -Milliseconds 600
    Invoke-ById $mainEl 'MenuSettings'
    $dark = Wait-El { Find-ById $mainEl 'ThemeDark' }
    if ($dark) { Select-El $dark; Start-Sleep -Milliseconds 500 }
    Invoke-ById $mainEl 'SettingsBack'
    Start-Sleep -Milliseconds 400
    Invoke-ById $mainEl 'MenuExit'
    Write-Host "app exited: $($p.WaitForExit(15000))"

    Write-Host "shots: $shotDir"
    $ok = $true
} catch {
    Write-Host "ERROR $($_.Exception.Message)" -ForegroundColor Magenta
} finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Start-Sleep -Milliseconds 400
    if (Test-Path $dataDir) { Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue }
    if (Test-Path $backup) { Move-Item -LiteralPath $backup -Destination $dataDir }
    if ($hadHwAccelProperty) {
        New-ItemProperty -Path $graphicsKey -Name $hwAccelName -PropertyType DWord -Value $oldHwAccelValue -Force | Out-Null
    } else {
        Remove-ItemProperty -Path $graphicsKey -Name $hwAccelName -ErrorAction SilentlyContinue
    }
}
if (-not $ok) { exit 1 }
