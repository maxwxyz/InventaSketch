# Scenario (maintainer report 2026-11-25): the text prompt of a flowchart step lies over the selected element; OK and
# Cancel must work with the real mouse (the add-in's hooks used to swallow those clicks because they were "inside the
# element"). The prompt is opened with the bar command "text", its buttons are found with UI Automation and clicked.
param($S)
$addin = $S.AddIn
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

function Find-IskPromptQuick {
    # Owned dialogs appear below their owner (PowerPoint's frame) in UI Automation, so look one level deeper too.
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'InventaSketch')
    $w = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($w) { return $w }
    $frames = $root.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, 'PPTFrameClass')))
    foreach ($f in $frames) {
        $w = $f.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($w) { return $w }
    }
    $null
}

function Find-IskPrompt {
    for ($i = 0; $i -lt 40; $i++) {
        $w = Find-IskPromptQuick
        if ($w) { return $w }
        Start-Sleep -Milliseconds 250
    }
    $null
}

function Click-IskButton($window, [string[]]$names) {
    foreach ($n in $names) {
        $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $n)
        $b = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
        if ($b) {
            $r = $b.Current.BoundingRectangle
            Invoke-IskClick ($r.X + $r.Width / 2) ($r.Y + $r.Height / 2) 1500
            return $true
        }
    }
    $false
}

$S.Window.Selection.Unselect()
# A step in the middle of the slide, wide enough that the centred prompt lies over it.
$name = $addin.Insert('ann.flow.process'); $shape = $S.Slide.Shapes.Item($name)
$shape.Width = 600; $shape.Height = 300
$shape.Left = ($S.App.ActivePresentation.PageSetup.SlideWidth - $shape.Width) / 2; $shape.Top = ($S.App.ActivePresentation.PageSetup.SlideHeight - $shape.Height) / 2
$shape.Select(); Start-Sleep 2

# OK with the mouse.
Write-IskInfo ("bar before: " + $addin.BarIds())
$ran = $addin.Command('text')
Write-IskInfo ("command text: $ran")
$prompt = Find-IskPrompt
if (-not $prompt) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-IskInfo ("top windows: " + (($all | ForEach-Object { $_.Current.Name }) -join ' | '))
}
Assert-Isk $S ($null -ne $prompt) 'the text prompt opens'
if ($prompt) {
    $edit = $prompt.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
    if ($edit) { ($edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue('Schritt aus dem Szenario') }
    Save-IskShot $S '1-prompt-over-unit'
    Assert-Isk $S (Click-IskButton $prompt @('OK')) 'OK is found'
    Start-Sleep 2
    Assert-Isk $S ($null -eq (Find-IskPromptQuick)) 'the prompt closes with OK'
}
$models = @(Get-IskShapes $S | ForEach-Object { Get-IskModel $_ }) -join ' '
Assert-Isk $S ($models -match 'Schritt aus dem Szenario') 'the entered text is in the element'

# Cancel with the mouse.
(Get-IskShapes $S | Select-Object -First 1).Select(); Start-Sleep 2
$addin.Command('text') | Out-Null
$prompt = Find-IskPrompt
if ($prompt) {
    Assert-Isk $S (Click-IskButton $prompt @('Abbrechen', 'Cancel')) 'Cancel is found'
    Start-Sleep 2
    Assert-Isk $S ($null -eq (Find-IskPromptQuick)) 'the prompt closes with Cancel'
}
Save-IskShot $S '2-after-cancel'
Send-IskEscape; Start-Sleep 1
