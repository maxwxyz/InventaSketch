# Scenario (maintainer 2026-11-25): the insert box (Ctrl+Space or the bar's Insert) closes when the user clicks outside
# it, and with Esc. The box is found through UI Automation by its title ("type to search" / "tippen zum Suchen").
param($S)
$addin = $S.AddIn
$k = Get-IskScale
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

function Find-InsertBoxQuick {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $scopes = @($root) + @($root.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'PPTFrameClass'))))
    foreach ($scope in $scopes) {
        foreach ($w in $scope.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
            $n = $w.Current.Name
            if ($n -like '*type to search*' -or $n -like '*tippen zum Suchen*') { return $w }
        }
    }
    $null
}

function Find-InsertBox {
    for ($i = 0; $i -lt 30; $i++) {
        $w = Find-InsertBoxQuick
        if ($w) { return $w }
        Start-Sleep -Milliseconds 250
    }
    $null
}

function Get-LogTail([string]$pattern) {
    $log = Get-IskLog $S
    Get-Content $log | Where-Object { $_ -match $pattern } | Select-Object -Last 1
}

$sh = $addin.Insert('mech.shaft.line'); $shape = $S.Slide.Shapes.Item($sh); $shape.Left = 200; $shape.Top = 200
$shape.Select(); Start-Sleep 3
$r = Get-IskRect $S $sh

# 1) Ctrl+Space, then a click on the empty slide far from the box: it closes, nothing is inserted.
$before = @(Get-IskShapes $S).Count
Move-IskMouse ($r.CX + 40 * $k) ($r.CY - 120 * $k); Start-Sleep -Milliseconds 400
Send-IskCtrlKey 0x20
$box = Find-InsertBox
Assert-Isk $S ($null -ne $box) 'Ctrl+Space opens the insert box'
if ($box) {
    $b = $box.Current.BoundingRectangle
    Save-IskShot $S '1-box-open' -Around ($b.X - 100), ($b.Y - 100), ($b.Width + 200), ($b.Height + 200)
    # A point on the slide well outside the box (left of it, or right if there is no room).
    $x = if ($b.X -gt 200 * $k) { $b.X - 120 * $k } else { $b.X + $b.Width + 120 * $k }
    Invoke-IskClick $x ($b.Y + 40 * $k) -WaitMs 1500
    Assert-Isk $S ($null -eq (Find-InsertBoxQuick)) 'a click outside closes the insert box'
    Assert-Isk $S ((Get-LogTail 'Insert box closed') -match 'click outside|another window') 'the add-in logs the light dismiss'
    Assert-Isk $S (@(Get-IskShapes $S).Count -eq $before) 'nothing was inserted'
}

# 2) The bar's Insert, then Esc.
(Get-IskShapes $S 'ISK:mech.shaft*' | Select-Object -First 1).Select(); Start-Sleep 2
$addin.Command('insert') | Out-Null
$box = Find-InsertBox
Assert-Isk $S ($null -ne $box) 'the bar button Insert opens the insert box'
if ($box) {
    Send-IskEscape; Start-Sleep 1
    Assert-Isk $S ($null -eq (Find-InsertBoxQuick)) 'Esc closes the insert box'
}
Save-IskShot $S '2-after'
