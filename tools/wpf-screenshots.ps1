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
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
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
$backup = Join-Path $env:TEMP ("yamb-screenshot-backup-" + [Guid]::NewGuid().ToString('N'))
$shotDir = Join-Path $repoRoot 'docs\screenshots'

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
function Find-Prefix($element, [string]$prefix) {
    $all = $element.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $all) {
        if ($item.Current.AutomationId -like "$prefix*") { return $item }
    }
    return $null
}
function Wait-El([scriptblock]$probe, [int]$seconds = 10) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $value = & $probe
        if ($null -ne $value) { return $value }
        Start-Sleep -Milliseconds 120
    }
    return $null
}
function Invoke-El($element) {
    if (-not $element) { throw 'cannot invoke null element' }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return
    }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        return
    }
    throw "element '$($element.Current.AutomationId)' is not invokable"
}
function Select-El($element) {
    if (-not $element) { throw 'cannot select null element' }
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
}
function Invoke-ById($root, [string]$id) {
    $el = Wait-El { Find-ById $root $id }
    if (-not $el) { throw "element '$id' not found" }
    Invoke-El $el
}
function Set-Value($element, [string]$value) {
    if (-not $element) { throw 'cannot set value on null element' }
    $element.SetFocus()
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($value)
}
function Save-Shot([long]$hwnd, [string]$path) {
    $rect = New-Object RECT
    [void][Native]::GetWindowRect([IntPtr]$hwnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
function Capture($main, [string]$name) {
    $path = Join-Path $shotDir $name
    $hwnd = [IntPtr]$main.Hwnd
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        [void][Native]::ShowWindow($hwnd, 9)
        [void][Native]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0043)
        [void][Native]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 300
        if ([Native]::GetForegroundWindow() -eq $hwnd) { break }
    }
    Start-Sleep -Milliseconds 350
    Save-Shot $main.Hwnd $path
    [void][Native]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0043)
    Write-Host "Saved $name"
}
function Set-UpGame($mainEl, [string]$name) {
    Invoke-ById $mainEl 'MenuNewGame'
    if (-not (Wait-El { Find-ById $mainEl 'ColumnSeven' })) { throw 'column step missing' }
    Invoke-ById $mainEl 'NewGameNext'
    if (-not (Wait-El { Find-ById $mainEl 'ModeSingle' })) { throw 'player step missing' }
    Invoke-ById $mainEl 'NewGameNext'
    $nameBox = Wait-El { Find-ById $mainEl 'Player1Name' }
    if (-not $nameBox) { throw 'name field missing' }
    Set-Value $nameBox $name
    Start-Sleep -Milliseconds 300
}
function Start-Round($mainEl, [string]$name) {
    Set-UpGame $mainEl $name
    Invoke-ById $mainEl 'StartGame'
    if (-not (Wait-El { Find-ById $mainEl 'RollButton' })) { throw 'game did not start' }
}
function Play-One-Cell($mainEl) {
    Invoke-ById $mainEl 'RollButton'
    Start-Sleep -Milliseconds 1200
    Invoke-ById $mainEl 'Cell_0_Downward_Ones'
    Start-Sleep -Milliseconds 900
}
function Stop-Game($mainEl) {
    Invoke-ById $mainEl 'MenuButton'
    $yes = Wait-El { Find-ByName $mainEl 'Da' }
    if ($yes) { Invoke-El $yes }
    Start-Sleep -Milliseconds 700
}

if (Test-Path $backup) { Remove-Item -Recurse -Force $backup }
if (Test-Path $dataDir) { Move-Item -LiteralPath $dataDir -Destination $backup }
if (Test-Path $shotDir) { Remove-Item -Recurse -Force $shotDir }
New-Item -ItemType Directory -Path $shotDir -Force | Out-Null

$p = Start-Process -FilePath $exe -PassThru
$ok = $false
try {
    $main = Wait-Window $p.Id 'Jamb'
    if (-not $main) { throw 'main window did not appear' }
    [void][Native]::ShowWindow([IntPtr]$main.Hwnd, 9)
    [void][Native]::SetForegroundWindow([IntPtr]$main.Hwnd)
    Start-Sleep -Milliseconds 800
    $mainEl = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$main.Hwnd)

    Invoke-ById $mainEl 'MenuSettings'
    $dark = Wait-El { Find-ById $mainEl 'ThemeDark' }
    if (-not $dark) { throw 'dark theme option missing' }
    Select-El $dark
    Start-Sleep -Milliseconds 800
    Invoke-ById $mainEl 'SettingsBack'
    if (-not (Wait-El { Find-ById $mainEl 'MenuNewGame' })) { throw 'menu did not return' }
    Capture $main '01-menu-dark.png'

    Start-Round $mainEl 'Marko'
    Play-One-Cell $mainEl
    Capture $main '02-game-dark.png'
    Stop-Game $mainEl

    Invoke-ById $mainEl 'MenuSettings'
    $light = Wait-El { Find-ById $mainEl 'ThemeLight' }
    if (-not $light) { throw 'light theme option missing' }
    Select-El $light
    Start-Sleep -Milliseconds 900
    Capture $main '05-settings-light.png'
    Invoke-ById $mainEl 'SettingsBack'
    if (-not (Wait-El { Find-ById $mainEl 'MenuNewGame' })) { throw 'menu did not return after light theme' }
    Capture $main '03-menu-light.png'

    Set-UpGame $mainEl 'Jovana'
    Capture $main '04-new-game-light.png'
    Invoke-ById $mainEl 'StartGame'
    if (-not (Wait-El { Find-ById $mainEl 'RollButton' })) { throw 'light game did not start' }
    Play-One-Cell $mainEl
    Capture $main '06-game-light.png'
    Stop-Game $mainEl

    Start-Round $mainEl 'Nikola'
    Play-One-Cell $mainEl
    Stop-Game $mainEl

    Invoke-ById $mainEl 'MenuResume'
    if (-not (Wait-El { Find-ById $mainEl 'SavedGamesBack' })) { throw 'saved games view missing' }
    Start-Sleep -Milliseconds 600
    Capture $main '07-saved-games.png'
    Invoke-ById $mainEl 'SavedGamesBack'
    if (-not (Wait-El { Find-ById $mainEl 'MenuNewGame' })) { throw 'menu did not return from saved games' }

    Invoke-ById $mainEl 'MenuHelp'
    if (-not (Wait-El { Find-ById $mainEl 'HelpBack' })) { throw 'help view missing' }
    Start-Sleep -Milliseconds 500
    Capture $main '08-help-rules.png'
    $search = Wait-El { Find-ById $mainEl 'HelpSearch' }
    if ($search) {
        Set-Value $search 'najava'
        Start-Sleep -Milliseconds 700
        Capture $main '09-help-search.png'
    }
    Invoke-ById $mainEl 'HelpBack'
    if (-not (Wait-El { Find-ById $mainEl 'MenuNewGame' })) { throw 'menu did not return from help' }

    Invoke-ById $mainEl 'MenuSettings'
    $rose = Wait-El { Find-ById $mainEl 'ThemeRose' }
    if (-not $rose) { throw 'rose theme option missing' }
    Select-El $rose
    Start-Sleep -Milliseconds 900
    Capture $main '10-settings-rose.png'
    Invoke-ById $mainEl 'SettingsBack'
    if (-not (Wait-El { Find-ById $mainEl 'MenuNewGame' })) { throw 'menu did not return after rose theme' }
    Capture $main '11-menu-rose.png'

    Invoke-ById $mainEl 'MenuNewGame'
    Invoke-ById $mainEl 'NewGameNext'
    $botMode = Wait-El { Find-ById $mainEl 'ModeBot' }
    if (-not $botMode) { throw 'bot mode option missing' }
    Select-El $botMode
    Start-Sleep -Milliseconds 700
    Capture $main '12-bot-mode.png'
    Invoke-ById $mainEl 'NewGameNext'
    Invoke-ById $mainEl 'StartGame'
    if (-not (Wait-El { Find-ById $mainEl 'RollButton' })) { throw 'bot game did not start' }
    Capture $main '13-bot-game.png'
    Stop-Game $mainEl

    Invoke-ById $mainEl 'MenuExit'
    if (-not $p.WaitForExit(15000)) { throw 'app did not exit' }
    Write-Host "Screenshots: $shotDir"
    $ok = $true
} catch {
    Write-Host "ERROR $($_.Exception.Message)" -ForegroundColor Magenta
} finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Start-Sleep -Milliseconds 500
    if (Test-Path $dataDir) { Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue }
    if (Test-Path $backup) { Move-Item -LiteralPath $backup -Destination $dataDir }
}
if (-not $ok) { exit 1 }
