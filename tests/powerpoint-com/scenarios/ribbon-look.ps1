# Scenario: the InventaSketch ribbon loads (an invalid ribbon XML is dropped silently) and shows the tab for a visual check:
# Tools, Structure and Colour as pictograms only, the "Modules" button, the line weights pictogram, the Review group.
param($S)
$addin = $S.AddIn
Assert-Isk $S ($addin.RibbonLoaded()) 'the ribbon is loaded'
Assert-Isk $S ($addin.ShowTab()) 'the InventaSketch tab is shown'
Start-Sleep 3
Save-IskShot $S '1-ribbon'
