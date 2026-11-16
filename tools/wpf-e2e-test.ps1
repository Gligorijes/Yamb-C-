$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public static class Win32Enum {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'src\Yamb.Presentation.Wpf\bin\Release\net8.0-windows\Yamb.Presentation.Wpf.exe'

function Get-AppWindows([int]$procId) {
    [Win32Enum]::WindowsOf([uint32]$procId) | ForEach-Object {
        [pscustomobject]@{ Hwnd = [long]$_; Title = [Win32Enum]::Title($_) }
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

function Get-WindowElement($windowObj) {
    [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$windowObj.Hwnd)
}

function Find-In($element, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Find-ById($element, [string]$id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Get-Text($element, [string]$id) {
    $el = Find-ById $element $id
    if ($el) { return $el.Current.Name } else { return $null }
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

function Select-ById($element, [string]$id) {
    $el = Find-ById $element $id
    if (-not $el) { throw "element not found by id: $id" }
    Select-El $el
}

function Invoke-Btn($element, [string]$name) {
    $btn = Find-In $element $name
    if (-not $btn) { throw "button not found: $name" }
    Invoke-El $btn
}

function Wait-Text($winEl, [string]$id, [string]$text, [int]$seconds = 10) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if ((Get-Text $winEl $id) -eq $text) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
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

function Wait-Gone([scriptblock]$finder, [int]$seconds = 10) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $el = & $finder
        if (-not $el) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

function Get-CellsByIdPrefix($winEl, [string]$prefix) {
    $btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $buttons = $winEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    @($buttons | Where-Object { $_.Current.AutomationId -like "$prefix*" })
}

$results = New-Object System.Collections.ArrayList

function Check([string]$label, [bool]$ok) {
    if ($ok) {
        Write-Host "PASS  $label" -ForegroundColor Green
        [void]$results.Add(@($label, 'PASS'))
    } else {
        Write-Host "FAIL  $label" -ForegroundColor Red
        [void]$results.Add(@($label, 'FAIL'))
    }
}

$dataDir = Join-Path $env:LOCALAPPDATA 'Yamb'
$backupDir = $null
if (Test-Path $dataDir) {
    $backupDir = Join-Path $env:TEMP ("yamb-e2e-backup-" + [Guid]::NewGuid().ToString('N'))
    Copy-Item -LiteralPath $dataDir -Destination $backupDir -Recurse -Force
    Remove-Item -Recurse -Force $dataDir
}

$p = Start-Process -FilePath $exe -PassThru
$passed = $false
try {
    $main = Wait-Window $p.Id 'Jamb'
    Check 'main window appears' ($null -ne $main)
    if (-not $main) { throw 'main window never appeared - aborting' }

    $mainEl = Get-WindowElement $main

    $resumeBtn = Wait-El { Find-ById $mainEl 'MenuResume' }
    Check 'menu view appears' ($null -ne $resumeBtn)
    if (-not $resumeBtn) { throw 'menu never appeared - aborting' }

    Check 'resume disabled without saved game' (-not $resumeBtn.Current.IsEnabled)

    Invoke-ById $mainEl 'MenuNewGame'
    $columnSeven = Wait-El { Find-ById $mainEl 'ColumnSeven' }
    Check 'new game starts on column step' ($null -ne $columnSeven)
    $columnOptions = @(
        Find-ById $mainEl 'ColumnFour'
        Find-ById $mainEl 'ColumnSix'
        Find-ById $mainEl 'ColumnSeven'
        Find-ById $mainEl 'ColumnEight'
        Find-ById $mainEl 'ColumnNine'
        Find-ById $mainEl 'ColumnTen'
    ) | Where-Object { $_ }
    Check 'column step shows six options' ($columnOptions.Count -eq 6)
    Check 'all column options enabled' (($columnOptions | Where-Object { -not $_.Current.IsEnabled }).Count -eq 0)
    if ($columnSeven) {
        $selPattern = $null
        $isSelected = $columnSeven.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selPattern) -and $selPattern.Current.IsSelected
        Check 'seven columns preselected' $isSelected
    }

    Invoke-ById $mainEl 'NewGameNext'
    Check 'player step appears' ($null -ne (Wait-El { Find-ById $mainEl 'ModeSingle' }))
    Invoke-ById $mainEl 'NewGameNext'

    $nameBox = Wait-El { Find-ById $mainEl 'Player1Name' }
    if ($nameBox) {
        $namePattern = $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $namePattern.SetValue('Aleksandar')
        Start-Sleep -Milliseconds 300
        Check 'player name accepts typing' ($namePattern.Current.Value -eq 'Aleksandar')
    }

    Check 'start button appears on name step' ($null -ne (Wait-El { Find-ById $mainEl 'StartGame' }))
    Invoke-ById $mainEl 'StartGame'
    Check 'game view loads' ($null -ne (Wait-El { Find-ById $mainEl 'RollButton' }))

    $lastSixDie = Wait-El { Find-ById $mainEl 'Die0_5' }
    Check 'six dice rendered' ($null -ne $lastSixDie)
    if ($lastSixDie) {
        $diceTops = @(0..5 | ForEach-Object { (Find-ById $mainEl "Die0_$_").Current.BoundingRectangle.Top })
        Check 'six dice share one row' (($diceTops | Sort-Object -Unique).Count -eq 1)
    }

    Check 'single player header' (Wait-Text $mainEl 'HeaderText' 'igra za jednog igrača')
    Check 'typed name shown in game' ((Wait-El { Find-ById $mainEl 'PlayerBarName0' }) -and ((Find-ById $mainEl 'PlayerBarName0').Current.Name -eq 'Aleksandar'))
    Check 'initial rolls text' (Wait-Text $mainEl 'RollsText' '0 bacanja')
    Check 'initial progress text' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 0/91')
    Check '91 cells on sheet' ((Get-CellsByIdPrefix $mainEl 'Cell_0_').Count -eq 91)
    Check 'section totals rendered' (($null -ne (Find-ById $mainEl 'SectionTotal_0_Downward_Numbers')) -and ($null -ne (Find-ById $mainEl 'SectionGrandTotal_0_Numbers')) -and ($null -ne (Find-ById $mainEl 'PlayerTotal_0')))

    Invoke-ById $mainEl 'RollButton'
    Check 'roll counter shows one throw' (Wait-Text $mainEl 'RollsText' '1 bacanje')
    Start-Sleep -Milliseconds 900

    $hoverCell = Find-ById $mainEl 'Cell_0_Free_Yamb'
    $hoverRect = if ($hoverCell) { $hoverCell.Current.BoundingRectangle } else { $null }
    if ($hoverRect) {
        [void][Win32Enum]::SetForegroundWindow([IntPtr]$main.Hwnd)
        Start-Sleep -Milliseconds 300
        [void][Win32Enum]::SetCursorPos(
            [int]($hoverRect.Left + $hoverRect.Width / 2),
            [int]($hoverRect.Top + $hoverRect.Height / 2))
        $preview = Wait-El {
            $cell = Find-ById $mainEl 'Cell_0_Free_Yamb'
            if ($cell -and $cell.Current.Name -match '^\d+$') { return $cell }
            return $null
        }
        Check 'hover reveals point preview' ($null -ne $preview)
        [void][Win32Enum]::SetCursorPos(10, 10)
        $hidden = Wait-El {
            $cell = Find-ById $mainEl 'Cell_0_Free_Yamb'
            if ($cell -and [string]::IsNullOrEmpty($cell.Current.Name)) { return $cell }
            return $null
        }
        Check 'preview clears when cursor leaves' ($null -ne $hidden)
    } else {
        Check 'hover reveals point preview' $false
        Check 'preview clears when cursor leaves' $false
    }

    $die = Find-ById $mainEl 'Die0_0'
    Check 'die found for hold' ($null -ne $die)
    Invoke-El $die
    $held = Wait-El {
        $d = Find-ById $mainEl 'Die0_0'
        if ($d -and $d.Current.Name -like '*|True') { return $d }
        return $null
    }
    Check 'die becomes held' ($null -ne $held)
    if ($held) {
        Invoke-El $held
        $released = Wait-El {
            $d = Find-ById $mainEl 'Die0_0'
            if ($d -and $d.Current.Name -like '*|False') { return $d }
            return $null
        }
        Check 'die becomes released' ($null -ne $released)
        Start-Sleep -Milliseconds 400
    }

    Invoke-ById $mainEl 'Cell_0_Announcement_Yamb'
    Start-Sleep -Milliseconds 1500

    Invoke-ById $mainEl 'Cell_0_Downward_Ones'
    Check 'popup blocks non-announced cell' (Wait-Text $mainEl 'PopupText' 'Ne možete da upišete.')
    Invoke-Btn $mainEl 'U redu'
    Check 'popup dismissed' (Wait-Gone { Find-In $mainEl 'Ne možete da upišete.' })
    Start-Sleep -Milliseconds 300

    Invoke-ById $mainEl 'Cell_0_Announcement_Yamb'
    Check 'announced write fills a cell' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 1/91')
    Check 'rolls reset after write' (Wait-Text $mainEl 'RollsText' '0 bacanja')
    Start-Sleep -Milliseconds 300
    Check 'section total updates after write' ((Find-ById $mainEl 'SectionTotal_0_Announcement_Combinations').Current.Name -match '^\d+$')
    Check 'footer shows SVE UKUPNO' ($null -ne (Wait-El { Find-In $mainEl 'SVE UKUPNO' }))
    Check 'footer grand total updates after write' ((Find-ById $mainEl 'PlayerTotal_0').Current.Name -match '^\d+$')

    Invoke-ById $mainEl 'RollButton'
    Check 'second turn rolls' (Wait-Text $mainEl 'RollsText' '1 bacanje')
    Start-Sleep -Milliseconds 300
    Invoke-ById $mainEl 'Cell_0_Downward_Ones'
    Check 'normal write fills next cell' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 2/91')
    Check 'numbers section total updates' ((Find-ById $mainEl 'SectionTotal_0_Downward_Numbers').Current.Name -match '^\d+$')

    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $cellBefore = (Find-ById $mainEl 'Cell_0_Downward_Ones').Current.BoundingRectangle.Width
    $beforeFs = New-Object RECT
    [void][Win32Enum]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$beforeFs)
    Invoke-ById $mainEl 'WindowFullscreen'
    Start-Sleep -Milliseconds 1200
    $duringFs = New-Object RECT
    [void][Win32Enum]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$duringFs)
    $fsWidth = $duringFs.Right - $duringFs.Left
    $fsHeight = $duringFs.Bottom - $duringFs.Top
    Check 'fullscreen expands window' ($fsWidth -ge ($screen.Width - 40) -and $fsHeight -ge ($screen.Height - 40))
    $cellDuring = (Find-ById $mainEl 'Cell_0_Downward_Ones').Current.BoundingRectangle.Width
    Check 'sheet stays within scale cap in fullscreen' ($cellDuring -le ($cellBefore * 1.2) -and $cellDuring -gt 40)
    Invoke-ById $mainEl 'WindowFullscreen'
    Start-Sleep -Milliseconds 1200
    $afterFs = New-Object RECT
    [void][Win32Enum]::GetWindowRect([IntPtr]$main.Hwnd, [ref]$afterFs)
    $beforeWidth = $beforeFs.Right - $beforeFs.Left
    $beforeHeight = $beforeFs.Bottom - $beforeFs.Top
    $afterWidth = $afterFs.Right - $afterFs.Left
    $afterHeight = $afterFs.Bottom - $afterFs.Top
    Check 'fullscreen restores window' ([math]::Abs($afterWidth - $beforeWidth) -le 8 -and [math]::Abs($afterHeight - $beforeHeight) -le 8)

    Invoke-ById $mainEl 'MenuButton'
    Check 'stop popup shown' (Wait-Text $mainEl 'PopupTitle' 'Zaustavljeno')
    Invoke-Btn $mainEl 'Ne'
    Check 'stop popup cancelled' (Wait-Gone { Find-In $mainEl 'Zaustavljeno' })

    Invoke-ById $mainEl 'MenuButton'
    Invoke-Btn $mainEl 'Da'
    Check 'game returns to menu' (Wait-Gone { Find-ById $mainEl 'RollButton' })
    Check 'menu returns after game' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))

    $resumeBtn = Find-ById $mainEl 'MenuResume'
    Check 'resume enabled after saved game' ($null -ne $resumeBtn -and $resumeBtn.Current.IsEnabled)

    Invoke-ById $mainEl 'MenuResume'
    Check 'saved games chooser appears' ($null -ne (Wait-El { Find-ById $mainEl 'SavedGamesBack' }))
    Check 'saved game card has delete action' ($null -ne (Wait-El { @(Get-CellsByIdPrefix $mainEl 'DeleteGame_') | Select-Object -First 1 }))
    $resumeGame = Wait-El { @(Get-CellsByIdPrefix $mainEl 'ResumeGame_') | Select-Object -First 1 }
    Check 'saved game card has continue action' ($null -ne $resumeGame)
    if ($resumeGame) { Invoke-El $resumeGame }
    Check 'saved game reopens' (Wait-Text $mainEl 'HeaderText' 'igra za jednog igrača')
    Check 'progress persisted after resume' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 2/91')
    Invoke-ById $mainEl 'MenuButton'
    Invoke-Btn $mainEl 'Da'
    Check 'resumed game returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))

    Invoke-ById $mainEl 'MenuNewGame'
    Check 'new game column step reappears' ($null -ne (Wait-El { Find-ById $mainEl 'ColumnSeven' }))
    Invoke-ById $mainEl 'NewGameNext'
    $modeTwo = Wait-El { Find-ById $mainEl 'ModeTwo' }
    Check 'new game player step reappears' ($null -ne $modeTwo)
    if ($modeTwo) {
        Select-El $modeTwo
        Start-Sleep -Milliseconds 300
        Invoke-ById $mainEl 'NewGameNext'
        Check 'second name field appears for two players' ($null -ne (Wait-El { Find-ById $mainEl 'Player2Name' }))
        Invoke-ById $mainEl 'StartGame'

        Check 'two player header' (Wait-Text $mainEl 'HeaderText' 'igra za 2 igrača')
        Check 'two sheets built' ((Get-CellsByIdPrefix $mainEl 'Cell_0_').Count -eq 91 -and (Get-CellsByIdPrefix $mainEl 'Cell_1_').Count -eq 91)
        Check 'two dice trays built' (($null -ne (Find-ById $mainEl 'Die0_0')) -and ($null -ne (Find-ById $mainEl 'Die1_0')))
        Check 'two player bars built' (($null -ne (Find-ById $mainEl 'PlayerBarName0')) -and ($null -ne (Find-ById $mainEl 'PlayerBarName1')))

        Invoke-ById $mainEl 'RollButton'
        Check 'two player roll registers' (Wait-Text $mainEl 'RollsText' '1 bacanje')
        Start-Sleep -Milliseconds 400
        Invoke-ById $mainEl 'Cell_0_Downward_Ones'
        Check 'turn resets for second player' (Wait-Text $mainEl 'RollsText' '0 bacanja')
        Check 'turn passes to second player' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 0/91')

        Invoke-ById $mainEl 'MenuButton'
        Invoke-Btn $mainEl 'Da'
        Check 'two player game returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))
    }

    Invoke-ById $mainEl 'MenuNewGame'
    $columnTen = Wait-El { Find-ById $mainEl 'ColumnTen' }
    Check 'ten column option appears' ($null -ne $columnTen)
    if ($columnTen) {
        Select-El $columnTen
        Start-Sleep -Milliseconds 300
        Invoke-ById $mainEl 'NewGameNext'
        Invoke-ById $mainEl 'NewGameNext'
        Invoke-ById $mainEl 'StartGame'

        Check 'ten column game loads' ($null -ne (Wait-El { Find-ById $mainEl 'RollButton' }))
        Check 'ten column progress is 0/130' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 0/130')
        Check 'ten column sheet has 130 cells' ((Get-CellsByIdPrefix $mainEl 'Cell_0_').Count -eq 130)
        Check 'directed column rendered' ($null -ne (Find-ById $mainEl 'Cell_0_Directed_Yamb'))
        Check 'obligatory column rendered' ($null -ne (Find-ById $mainEl 'Cell_0_Obligatory_Yamb'))
        Check 'maximum column rendered' ($null -ne (Find-ById $mainEl 'Cell_0_Maximum_Yamb'))

        Invoke-ById $mainEl 'MenuButton'
        Invoke-Btn $mainEl 'Da'
        Check 'ten column game returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))
    }

    Invoke-ById $mainEl 'MenuNewGame'
    Invoke-ById $mainEl 'NewGameNext'
    Invoke-ById $mainEl 'NewGameNext'
    Invoke-ById $mainEl 'StartGame'
    Check 'manual announcement game loads' ($null -ne (Wait-El { Find-ById $mainEl 'RollButton' }))
    Invoke-ById $mainEl 'RollButton'
    Check 'manual announcement first roll registers' (Wait-Text $mainEl 'RollsText' '1 bacanje')
    Invoke-ById $mainEl 'Cell_0_Announcement_Yamb'
    Start-Sleep -Milliseconds 400
    Invoke-ById $mainEl 'RollButton'
    Check 'manual announcement second roll registers' (Wait-Text $mainEl 'RollsText' '2 bacanja')
    Start-Sleep -Milliseconds 400
    Invoke-ById $mainEl 'RollButton'
    Check 'third roll does not auto-write announced cell' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 0/91')
    Check 'third roll keeps announced turn open' (Wait-Text $mainEl 'RollsText' '3 bacanja')
    Check 'roll button disabled after third roll' ((Find-ById $mainEl 'RollButton').Current.IsEnabled -eq $false)
    Invoke-ById $mainEl 'Cell_0_Announcement_Yamb'
    Check 'manual announced write fills a cell' (Wait-Text $mainEl 'ProgressText' 'popunjeno polja: 1/91')
    Check 'manual announced write resets rolls' (Wait-Text $mainEl 'RollsText' '0 bacanja')
    Invoke-ById $mainEl 'MenuButton'
    Invoke-Btn $mainEl 'Da'
    Check 'manual announcement game returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))

    Invoke-ById $mainEl 'MenuLeaderboard'
    $lbBack = Wait-El { Find-ById $mainEl 'LeaderboardBack' }
    Check 'leaderboard view appears' ($null -ne $lbBack)
    if ($lbBack) {
        Check 'leaderboard empty state' ($null -ne (Find-In $mainEl 'Još nema rezultata.'))
        Invoke-ById $mainEl 'LeaderboardClear'
        Check 'clear confirm popup' ($null -ne (Wait-El { Find-In $mainEl 'Da li ste sigurni da želite da obrišete sve rezultate?' }))
        Invoke-ById $mainEl 'ClearNo'
        Check 'clear cancelled' (Wait-Gone { Find-In $mainEl 'Da li ste sigurni da želite da obrišete sve rezultate?' })
        Invoke-ById $mainEl 'LeaderboardClear'
        Invoke-ById $mainEl 'ClearYes'
        Check 'leaderboard still empty after clear' ($null -ne (Wait-El { Find-In $mainEl 'Još nema rezultata.' }))
        Invoke-ById $mainEl 'LeaderboardBack'
        Check 'leaderboard returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))
    }

    Invoke-ById $mainEl 'MenuSettings'
    $faceNumbers = Wait-El { Find-ById $mainEl 'FaceNumbers' }
    Check 'settings view appears' ($null -ne $faceNumbers)
    if ($faceNumbers) {
        Select-El $faceNumbers
        Start-Sleep -Milliseconds 500
        Check 'settings file written' (Test-Path (Join-Path $dataDir 'settings.json'))

        $themeLight = Wait-El { Find-ById $mainEl 'ThemeLight' }
        Check 'theme option present' ($null -ne $themeLight)
        if ($themeLight) {
            Select-El $themeLight
            Start-Sleep -Milliseconds 900
            $settingsJson = if (Test-Path (Join-Path $dataDir 'settings.json')) { Get-Content -Raw (Join-Path $dataDir 'settings.json') } else { '' }
            Check 'light theme persists' ($settingsJson -match '"Theme":"Light"')
            $themeDark = Find-ById $mainEl 'ThemeDark'
            if ($themeDark) {
                Select-El $themeDark
                Start-Sleep -Milliseconds 600
            }
        }

        $languageEnglish = Wait-El { Find-ById $mainEl 'LanguageEnglish' }
        Check 'language option present' ($null -ne $languageEnglish)
        if ($languageEnglish) {
            Select-El $languageEnglish
            Start-Sleep -Milliseconds 500
            $settingsJson = Get-Content -Raw (Join-Path $dataDir 'settings.json')
            Check 'english language persists' ($settingsJson -match '"Language":"English"')
            $backEn = Find-ById $mainEl 'SettingsBack'
            Check 'settings ui switches to english' ($backEn -and $backEn.Current.Name -eq 'Back')

            $autoCheck = Find-ById $mainEl 'AutoCalculate'
            Check 'auto-calculate option present' ($null -ne $autoCheck)
            if ($autoCheck) {
                $toggle = $autoCheck.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
                $toggle.Toggle()
                Start-Sleep -Milliseconds 500
                $settingsJson = Get-Content -Raw (Join-Path $dataDir 'settings.json')
                Check 'auto-calculate false persists' ($settingsJson -match '"AutoCalculate":false')
                $toggle.Toggle()
                Start-Sleep -Milliseconds 500
                $settingsJson = Get-Content -Raw (Join-Path $dataDir 'settings.json')
                Check 'auto-calculate true persists' ($settingsJson -match '"AutoCalculate":true')
            }

            Invoke-ById $mainEl 'SettingsBack'
            Check 'english menu title shown' ($null -ne (Wait-El { Find-In $mainEl 'Ready for a game?' }))
            Invoke-ById $mainEl 'MenuSettings'
            $languageSerbian = Wait-El { Find-ById $mainEl 'LanguageSerbian' }
            Select-El $languageSerbian
            Start-Sleep -Milliseconds 500
            $backSr = Find-ById $mainEl 'SettingsBack'
            Check 'settings ui returns to serbian' ($backSr -and $backSr.Current.Name -eq 'Nazad')

            $diceFive = Find-ById $mainEl 'DiceFive'
            Check 'five dice option present' ($null -ne $diceFive)
            if ($diceFive) {
                Select-El $diceFive
                Start-Sleep -Milliseconds 400
                $settingsJson = Get-Content -Raw (Join-Path $dataDir 'settings.json')
                Check 'five dice setting persists' ($settingsJson -match '"DiceCount":5')
            }
        }

        Invoke-ById $mainEl 'SettingsBack'
        Check 'settings returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))
        Check 'menu shows five dice chip' ($null -ne (Wait-El { Find-In $mainEl '5 kockica' }))

        Invoke-ById $mainEl 'MenuNewGame'
        Check 'five dice new game opens' ($null -ne (Wait-El { Find-ById $mainEl 'ColumnSeven' }))
        Invoke-ById $mainEl 'NewGameNext'
        Invoke-ById $mainEl 'NewGameNext'
        Invoke-ById $mainEl 'StartGame'
        Check 'five dice game loads' ($null -ne (Wait-El { Find-ById $mainEl 'RollButton' }))
        Check 'five dice rendered' (($null -ne (Wait-El { Find-ById $mainEl 'Die0_4' })) -and ($null -eq (Find-ById $mainEl 'Die0_5')))
        $fiveDiceTops = @(0..4 | ForEach-Object { (Find-ById $mainEl "Die0_$_").Current.BoundingRectangle.Top })
        Check 'five dice share one row' (($fiveDiceTops | Sort-Object -Unique).Count -eq 1)

        Invoke-ById $mainEl 'MenuButton'
        Invoke-Btn $mainEl 'Da'
        Check 'five dice game returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))
    }

    Invoke-ById $mainEl 'MenuHelp'
    Check 'help view appears' (Wait-Text $mainEl 'HelpPage' '1/5')
    Invoke-ById $mainEl 'HelpNext'
    Check 'help page advances' (Wait-Text $mainEl 'HelpPage' '2/5')
    Invoke-ById $mainEl 'HelpPrev'
    Check 'help page goes back' (Wait-Text $mainEl 'HelpPage' '1/5')
    $versionEl = Wait-El { Find-ById $mainEl 'HelpVersion' }
    Check 'help shows build version' ($versionEl -and $versionEl.Current.Name -match '1\.0\.0-alpha\.2' -and $versionEl.Current.Name -match 'build 2')
    for ($i = 0; $i -lt 4; $i++) {
        Invoke-ById $mainEl 'HelpNext'
        Start-Sleep -Milliseconds 250
    }
    Check 'help about page opens' (Wait-Text $mainEl 'HelpPage' '5/5')
    Invoke-ById $mainEl 'HelpBack'
    Check 'help returns to menu' ($null -ne (Wait-El { Find-ById $mainEl 'MenuNewGame' }))

    Invoke-ById $mainEl 'MenuExit'
    $exited = $p.WaitForExit(15000)
    Check 'app exits cleanly' $exited
    $finalSettings = if (Test-Path (Join-Path $dataDir 'settings.json')) { Get-Content -Raw (Join-Path $dataDir 'settings.json') } else { '' }
    Check 'settings saved on exit' ($finalSettings -match '"Language":"Serbian"' -and $finalSettings -match '"AutoCalculate":true')
    Check 'dice count saved on exit' ($finalSettings -match '"DiceCount":5')

    $passed = $true
} catch {
    Write-Host "ERROR  $($_.Exception.Message)" -ForegroundColor Magenta
} finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Start-Sleep -Milliseconds 500

    if ($backupDir -and (Test-Path $backupDir)) {
        if (Test-Path $dataDir) { Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue }
        Copy-Item -LiteralPath $backupDir -Destination $dataDir -Recurse -Force
        Remove-Item -Recurse -Force $backupDir -ErrorAction SilentlyContinue
        Write-Host "Restored user data from $backupDir" -ForegroundColor DarkGray
    } elseif ($passed -and $env:YAMB_E2E_CLEANUP -ne '0' -and (Test-Path $dataDir)) {
        Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue
    }
}

$fails = @($results | Where-Object { $_[1] -eq 'FAIL' })
Write-Host ''
if ($fails.Count -eq 0 -and $passed) {
    Write-Host 'ALL CHECKS PASSED' -ForegroundColor Green
    exit 0
} else {
    Write-Host "$($fails.Count) CHECK(S) FAILED" -ForegroundColor Red
    exit 1
}
