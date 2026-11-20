// Writes the add-in-only XML manifest for PowerPoint to dist/manifest.xml (and manifest.xml in the package root).
// Base URL: first CLI argument or ISK_BASE_URL, default https://localhost:3000/.
// <EquivalentAddins> names the COM add-in so Office on Windows hides this web add-in while the COM add-in is
// installed (ADR-0004).
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
let base = process.argv[2] ?? process.env.ISK_BASE_URL ?? "https://localhost:3000/";
if (!base.endsWith("/")) base += "/";
const id = "5d1f2c7e-8a43-4f0b-9c61-2b7e4a9d3f10";
const version = "0.1.0.0";
const esc = (s) => s.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
const u = (p) => esc(base + p);

export const manifest = `<?xml version="1.0" encoding="UTF-8"?>
<OfficeApp xmlns="http://schemas.microsoft.com/office/appforoffice/1.1"
           xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
           xmlns:bt="http://schemas.microsoft.com/office/officeappbasictypes/1.0"
           xmlns:ov="http://schemas.microsoft.com/office/taskpaneappversionoverrides"
           xsi:type="TaskPaneApp">
  <Id>${id}</Id>
  <Version>${version}</Version>
  <ProviderName>InventaSketch contributors</ProviderName>
  <DefaultLocale>en-US</DefaultLocale>
  <DisplayName DefaultValue="InventaSketch">
    <Override Locale="de-DE" Value="InventaSketch" />
  </DisplayName>
  <Description DefaultValue="Quick technical sketches, schematic gearboxes and patent-style drawings.">
    <Override Locale="de-DE" Value="Schnelle technische Skizzen, Getriebeschemata und Patentzeichnungen." />
  </Description>
  <IconUrl DefaultValue="${u("assets/icon-32.png")}" />
  <HighResolutionIconUrl DefaultValue="${u("assets/icon-64.png")}" />
  <SupportUrl DefaultValue="https://github.com/maxwxyz/InventaSketch" />
  <AppDomains>
    <AppDomain>${esc(new URL(base).origin)}</AppDomain>
  </AppDomains>
  <Hosts>
    <Host Name="Presentation" />
  </Hosts>
  <DefaultSettings>
    <SourceLocation DefaultValue="${u("taskpane.html")}" />
  </DefaultSettings>
  <Permissions>ReadWriteDocument</Permissions>
  <VersionOverrides xmlns="http://schemas.microsoft.com/office/taskpaneappversionoverrides" xsi:type="VersionOverridesV1_0">
    <Hosts>
      <Host xsi:type="Presentation">
        <DesktopFormFactor>
          <GetStarted>
            <Title resid="Isk.GetStarted.Title" />
            <Description resid="Isk.GetStarted.Description" />
            <LearnMoreUrl resid="Isk.Help.Url" />
          </GetStarted>
          <ExtensionPoint xsi:type="PrimaryCommandSurface">
            <CustomTab id="Isk.Tab">
              <Group id="Isk.Group.Sketch">
                <Label resid="Isk.Group.Label" />
                <Icon>
                  <bt:Image size="16" resid="Isk.Icon16" />
                  <bt:Image size="32" resid="Isk.Icon32" />
                  <bt:Image size="80" resid="Isk.Icon80" />
                </Icon>
                <Control xsi:type="Button" id="Isk.Open">
                  <Label resid="Isk.Open.Label" />
                  <Supertip>
                    <Title resid="Isk.Open.Label" />
                    <Description resid="Isk.Open.Tip" />
                  </Supertip>
                  <Icon>
                    <bt:Image size="16" resid="Isk.Icon16" />
                    <bt:Image size="32" resid="Isk.Icon32" />
                    <bt:Image size="80" resid="Isk.Icon80" />
                  </Icon>
                  <Action xsi:type="ShowTaskpane">
                    <TaskpaneId>Isk.Taskpane</TaskpaneId>
                    <SourceLocation resid="Isk.Taskpane.Url" />
                  </Action>
                </Control>
              </Group>
              <Label resid="Isk.Tab.Label" />
            </CustomTab>
          </ExtensionPoint>
        </DesktopFormFactor>
      </Host>
    </Hosts>
    <Resources>
      <bt:Images>
        <bt:Image id="Isk.Icon16" DefaultValue="${u("assets/icon-16.png")}" />
        <bt:Image id="Isk.Icon32" DefaultValue="${u("assets/icon-32.png")}" />
        <bt:Image id="Isk.Icon80" DefaultValue="${u("assets/icon-80.png")}" />
      </bt:Images>
      <bt:Urls>
        <bt:Url id="Isk.Taskpane.Url" DefaultValue="${u("taskpane.html")}" />
        <bt:Url id="Isk.Help.Url" DefaultValue="https://github.com/maxwxyz/InventaSketch" />
      </bt:Urls>
      <bt:ShortStrings>
        <bt:String id="Isk.Tab.Label" DefaultValue="InventaSketch" />
        <bt:String id="Isk.Group.Label" DefaultValue="Sketch">
          <bt:Override Locale="de-DE" Value="Skizze" />
        </bt:String>
        <bt:String id="Isk.Open.Label" DefaultValue="Open editor">
          <bt:Override Locale="de-DE" Value="Editor öffnen" />
        </bt:String>
        <bt:String id="Isk.GetStarted.Title" DefaultValue="InventaSketch is ready" />
      </bt:ShortStrings>
      <bt:LongStrings>
        <bt:String id="Isk.Open.Tip" DefaultValue="Opens the InventaSketch editor with the library of machine elements, arrows and patent marks.">
          <bt:Override Locale="de-DE" Value="Öffnet den InventaSketch-Editor mit Bibliothek für Maschinenelemente, Pfeile und Patentzeichen." />
        </bt:String>
        <bt:String id="Isk.GetStarted.Description" DefaultValue="Open the InventaSketch tab to start sketching." />
      </bt:LongStrings>
    </Resources>
    <EquivalentAddins>
      <EquivalentAddin>
        <ProgId>InventaSketch.AddIn.Connect</ProgId>
        <Type>COM</Type>
      </EquivalentAddin>
    </EquivalentAddins>
  </VersionOverrides>
</OfficeApp>
`;

if (import.meta.url === `file:///${process.argv[1].replace(/\\/g, "/")}` || process.argv[1]?.endsWith("manifest.mjs")) {
  mkdirSync(join(root, "dist"), { recursive: true });
  writeFileSync(join(root, "dist", "manifest.xml"), manifest, "utf8");
  writeFileSync(join(root, "manifest.xml"), manifest, "utf8");
  console.log(`manifest: base ${base} -> dist/manifest.xml, manifest.xml`);
}
