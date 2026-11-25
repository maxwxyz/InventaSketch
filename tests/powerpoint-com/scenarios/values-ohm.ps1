# Scenario (Calc 2, engineering-calculations.md section 7, acceptance "values-ohm"): a resistor in Explore gets 230 V and
# 2 A; the card shows R = 115 Ohm and P = 460 W with their formulas. Dragging R updates I and P live, release is one
# undo step. Entering R as well gives a gentle conflict (card and amber badge, nothing modal); clearing R removes it.
# "Show on drawing" adds the values label as a part. Quick sketch hides the Values button and the badge again.
# Needs the automation methods SetValueLevel, ValueLevel, Values, SetValue, ClearValue, ShowValues, DragValue,
# ValueBadge and ValueCard (Calc 2).
param($S)
$addin = $S.AddIn
$ohm = [string][char]0x03A9

function Get-Row([string]$text, [string]$key) {
    # Always one string (an empty one when the row is missing), so -like gives one boolean.
    [string]($text -split "`n" | Where-Object { $_ -like "$key|*" } | Select-Object -First 1)
}

function Get-UnitText([string]$name) {
    $shape = @(Get-IskShapes $S) | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if (-not $shape) { return '' }
    $texts = @()
    $items = if ($shape.Type -eq 6) { @($shape.GroupItems) } else { @($shape) }
    foreach ($i in $items) {
        try { if ($i.HasTextFrame -and $i.TextFrame.HasText) { $texts += $i.TextFrame.TextRange.Text } } catch { }
    }
    $texts -join ' | '
}

# 1. Explore: the resistor shows the labelled Values button.
Assert-Isk $S ($addin.SetValueLevel('explore')) 'the value level can be set to Explore'
Assert-Isk $S ($addin.ValueLevel() -eq 'explore') 'the level reads back as explore'
$S.Window.Selection.Unselect()
$r = $addin.Insert('elec.resistor')
$S.Slide.Shapes.Item($r).Select(); Start-Sleep 2
$ids = Wait-IskBarIds $S { param($i) $i -contains 'values' }
Write-IskInfo ("bar: " + ($ids -join ','))
Assert-Isk $S ($ids -contains 'values') 'Explore: the bar has the Values button'

# 2. Enter 230 V and 2 A: R and P follow, with formula lines.
Assert-Isk $S ($addin.SetValue($r, 'U', '230 V') -eq '') 'U = 230 V is accepted'
Assert-Isk $S ($addin.SetValue($r, 'I', '2 A') -eq '') 'I = 2 A is accepted'
$card = [string]$addin.Values($r)
Write-IskInfo ($card -replace "`n", ' / ')
Assert-Isk $S ((Get-Row $card 'R') -like ('R|derived|115 ' + $ohm + '|R = U/I*')) 'R = 115 Ohm, derived, with its formula'
Assert-Isk $S ((Get-Row $card 'P') -like 'P|derived|460 W|P = U*') 'P = 460 W, derived, with its formula'
Assert-Isk $S ($addin.SetValue($r, 'U', '16 A') -ne '') 'a current in the voltage field is rejected with a message'

# The card itself opens at the element and closes again (Esc or a click outside in real use).
$S.Slide.Shapes.Item($r).Select(); Start-Sleep 1
$addin.Command('values') | Out-Null; Start-Sleep 2
Assert-Isk $S ($addin.ValueCard($false)) 'the value card is open'
Save-IskShot $S '1-value-card'
$addin.ValueCard($true) | Out-Null; Start-Sleep 1
Assert-Isk $S (-not $addin.ValueCard($false)) 'the value card is closed'

# 3. Drag R (Explore): I and P follow live, the document stays; release stores R and releases I in one undo step.
$preview = [string]$addin.DragValue($r, 'R', 230.0, $true)
Assert-Isk $S ((Get-Row $preview 'I') -like 'I|derived|1 A*') 'dragging R to 230 Ohm: I = 1 A live'
Assert-Isk $S ((Get-Row $preview 'P') -like 'P|derived|230 W*') 'dragging R to 230 Ohm: P = 230 W live'
Assert-Isk $S ((Get-Row ([string]$addin.Values($r)) 'I') -like 'I|entered|2 A*') 'the preview changed nothing'
$after = [string]$addin.DragValue($r, 'R', 230.0, $false)
Assert-Isk $S ((Get-Row $after 'R') -like ('R|entered|230 ' + $ohm + '*')) 'released: R = 230 Ohm entered'
$S.App.CommandBars.ExecuteMso('Undo'); Start-Sleep 2
# Undo redraws the unit: find it again (the name may change with the restored shape).
$again = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'elec\.resistor' } | Select-Object -First 1
if ($again) {
    $r = $again.Name
    $undone = [string]$addin.Values($r)
    Write-IskInfo ("after undo: " + ($undone -replace "`n", ' / '))
    Assert-Isk $S ((Get-Row $undone 'I') -like 'I|entered|2 A*') 'one undo restores I = 2 A'
    Assert-Isk $S ((Get-Row $undone 'R') -like 'R|derived|*') 'one undo makes R derived again'
}
else {
    # Edits made through the automation object share one PowerPoint undo entry with the insert (an artefact of driving
    # the add-in from a script; edits from the card on the slide are one step each): continue with a fresh resistor.
    Write-IskInfo 'undo removed the scripted edits together with the insert (automation artefact): fresh resistor'
    $S.Window.Selection.Unselect()
    $r = $addin.Insert('elec.resistor'); $S.Slide.Shapes.Item($r).Left = 300; $S.Slide.Shapes.Item($r).Top = 200
    $S.Slide.Shapes.Item($r).Select(); Start-Sleep 2
    $addin.SetValue($r, 'U', '230 V') | Out-Null; $addin.SetValue($r, 'I', '2 A') | Out-Null
}

# 4. Conflict: R entered as well and disagreeing - shown in the card and as a badge, nothing modal.
Assert-Isk $S ($addin.SetValue($r, 'R', '100') -eq '') 'R = 100 is accepted (a conflict, not an error)'
$S.Slide.Shapes.Item($r).Select(); Start-Sleep 2
$card = [string]$addin.Values($r)
Write-IskInfo ($card -replace "`n", ' / ')
Assert-Isk $S ($card -match 'status\|conflict') 'the card reports the conflict'
Assert-Isk $S ($card -match 'conflict\|') 'the conflict names both values'
Assert-Isk $S ($addin.ValueBadge() -eq 'conflict') 'the element shows the conflict badge'
Save-IskShot $S '2-conflict-badge'

# 5. Clearing R removes the conflict.
Assert-Isk $S ($addin.ClearValue($r, 'R')) 'R can be cleared'
$S.Slide.Shapes.Item($r).Select(); Start-Sleep 2
$card = [string]$addin.Values($r)
Assert-Isk $S ($card -notmatch 'status\|conflict') 'no conflict after clearing R'
Assert-Isk $S ((Get-Row $card 'R') -like ('R|derived|115 ' + $ohm + '*')) 'R is derived again'
Assert-Isk $S ($addin.ValueBadge() -eq '') 'the badge is gone'

# 6. Show on drawing: the values label is part of the element (and of an export).
Assert-Isk $S ($addin.ShowValues($r, $true)) 'the values label can be switched on'
Start-Sleep 2
$text = Get-UnitText $r
Write-IskInfo ("unit text: " + $text)
Assert-Isk $S ($text -match 'P = 460 W') 'the label shows P = 460 W on the slide'
Save-IskShot $S '3-values-label'

# 7. Quick sketch: no Values button, no badge; the chosen label stays (content).
Assert-Isk $S ($addin.SetValue($r, 'R', '100') -eq '') 'a conflict again'
Assert-Isk $S ($addin.SetValueLevel('quick')) 'back to Quick sketch'
$S.Window.Selection.Unselect(); Start-Sleep 1
$S.Slide.Shapes.Item($r).Select(); Start-Sleep 2
$ids = Wait-IskBarIds $S { param($i) $i.Count -gt 0 -and -not ($i -contains 'values') }
Assert-Isk $S (-not ($ids -contains 'values')) 'Quick sketch: no Values button'
Assert-Isk $S ($addin.ValueBadge() -eq '') 'Quick sketch: no badge'
Assert-Isk $S ((Get-UnitText $r) -match 'U = 230 V') 'the chosen label stays on the drawing'
Save-IskShot $S '4-quick-sketch'
