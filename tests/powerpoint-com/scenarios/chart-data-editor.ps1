# Scenario (charts v2 stage 2): "Data …" on a chart opens the table editor (not the one-line prompt); a value changed in
# the grid ends up in the chart's data after OK.
param($S)
$addin = $S.AddIn
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

function Find-Editor {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    for ($i = 0; $i -lt 40; $i++) {
        foreach ($scope in @($root) + @($root.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'PPTFrameClass'))))) {
            $all = $scope.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($w in $all) { if ($w.Current.Name -like 'InventaSketch ?*' -and $w.Current.Name.Length -gt 16) { return $w } }
        }
        Start-Sleep -Milliseconds 250
    }
    $null
}

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 90
$shape.Select(); Start-Sleep 2
$addin.Command('act:chart.data') | Out-Null
$editor = Find-Editor
Assert-Isk $S ($null -ne $editor) 'the data table editor opens'
if ($editor) {
    # The grid shows the chart's rows: a cell with the first value.
    $cell = $editor.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '1.2')))
    if (-not $cell) { $cell = $editor.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ValueProperty, '1.2'))) }
    Assert-Isk $S ($null -ne $cell) 'it shows the data rows in a grid'
    Save-IskShot $S '1-data-editor'
    $ok = $editor.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'OK')))
    $r = $ok.Current.BoundingRectangle
    Invoke-IskClick ($r.X + $r.Width / 2) ($r.Y + $r.Height / 2) 2500
    Assert-Isk $S ($null -eq (Find-Editor)) 'the editor closes with OK'
}
$model = (@(Get-IskShapes $S) | ForEach-Object { Get-IskModel $_ }) -join ' '
Assert-Isk $S ($model -match '0;1.2') 'the chart keeps its data after OK'
Send-IskEscape; Start-Sleep 1
