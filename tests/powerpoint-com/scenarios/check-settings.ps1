# Scenario (Calc 6), ASCII only: a resistor above its rated power shows the plausibility warning in its value card with
# rule and source; "Mute" hides it for the presentation; a unit preference (power in kW) changes how values are shown.
param($S)
$addin = $S.AddIn
[void]$addin.SetValueLevel('explore')
$S.Window.Selection.Unselect()
$name = $addin.Insert('elec.resistor'); Start-Sleep 1
function Get-Res { @(Get-IskShapes $S 'ISK:elec.resistor*')[0] }
[void]$addin.SetValue((Get-Res).Name, 'U', '230 V'); Start-Sleep 1
[void]$addin.SetValue((Get-Res).Name, 'R', '100'); Start-Sleep 1
# The rating as a parameter (prated), set with the parameter command of the bar/pie.
(Get-Res).Select(); Start-Sleep 2
[void]$addin.Command('param:prated:0.25 W'); Start-Sleep 2
$checks = [string]$addin.Checks((Get-Res).Name)
Write-IskInfo ("checks: $checks")
Assert-Isk $S ($checks -match 'elec\.r\.power') 'the value card lists the power warning'
Assert-Isk $S ($checks -match '\[S7\]') 'the warning names its source'
(Get-Res).Select(); Start-Sleep 2
Assert-Isk $S ([string]$addin.ValueBadge() -eq 'conflict') 'the element shows the warning badge'
Assert-Isk $S ($addin.MuteCheck('elec.r.power', $true)) 'the check can be muted'
Start-Sleep 1
Assert-Isk $S (-not (([string]$addin.Checks((Get-Res).Name)) -match 'elec\.r\.power')) 'a muted check is not listed'
$v0 = [string]$addin.Values((Get-Res).Name)
Assert-Isk $S ($addin.SetUnitPreference('power', 'kW')) 'power can be shown in kW'
Start-Sleep 1
$v1 = [string]$addin.Values((Get-Res).Name)
Write-IskInfo ("values: $v0 -> $v1")
Assert-Isk $S ($v1 -match 'kW') 'the power is shown in kW'
[void]$addin.SetUnitPreference('power', '')
[void]$addin.MuteCheck('elec.r.power', $false)
[void]$addin.SetValueLevel('quick')
