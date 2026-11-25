using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Diagrams module: UML (class, use case, activity, state, sequence, deployment, relationships), ER (entities,
    /// Chen notation, crow's-foot connectors), system/architecture blocks, BPMN-lite with pools and lanes, control loops,
    /// computer systems and communication, data layouts, GUI wireframes and patent-style state circles. Every box symbol exposes
    /// connector ports; connectors are point objects whose ends attach to those ports. Simple flowchart starters
    /// remain in the annotation module (<c>ann.flow.*</c>).
    /// </summary>
    public sealed class DiagramsModule : IModule
    {
        private static readonly ObjectType[] AllTypes = CreateTypes();

        public string Id => "diagrams";
        public I18nText Name => new("Diagrams (UML, ER, flow)", "Diagramme (UML, ER, Ablauf)");
        public string Version => "0.3.0";
        public IEnumerable<ObjectType> Types => AllTypes;

        private static ObjectType[] CreateTypes() => new ObjectType[]
        {
            // UML structure
            new ClassType(ClassKind.Class), new ClassType(ClassKind.Interface), new ClassType(ClassKind.Enum),
            new PackageType(), new ComponentType(), new NodeType(), new NoteType(),
            // Use case
            new ActorType(), new UseCaseType(),
            // Activity
            new ActionType(), new ControlNodeType(NodeKind.Initial), new ControlNodeType(NodeKind.Final), new ControlNodeType(NodeKind.FlowFinal),
            new DecisionType(), new BarType(), new SwimlaneType(),
            // State machine
            new StateType(), new CompositeStateType(),
            // Sequence
            new LifelineType(), new ActivationType(), new MessageType(), new FragmentType(),
            // Relationships
            new UmlRelationType(),
            // ER
            new EntityType(), new ErAttributeType(), new ErRelationshipType(), new CrowsFootType(),
            // System / architecture
            new BlockType(), new ContainerType(), new CloudType(), new DatabaseType(), new QueueType(), new ServerType(),
            new ClientType(), new UserType(), new LollipopType(),
            // BPMN-lite
            new BpmnEventType(EventKind.Start), new BpmnEventType(EventKind.Intermediate), new BpmnEventType(EventKind.End),
            new BpmnTaskType(), new BpmnGatewayType(), new BpmnPoolType(),
            // Control loops
            new SumType(), new TransferType(), new BranchType(), new MultiplyType(), new SignalType(), new ControlLoopType(),
            // Computer systems and communication
            new SystemBusType(), new CommIconType(CommKind.Phone), new CommIconType(CommKind.BaseStation), new CommIconType(CommKind.Router),
            new CommIconType(CommKind.Switch), new CommIconType(CommKind.Satellite), new CommIconType(CommKind.Iot), new WirelessType(),
            // Data layouts
            new DataFieldsType(),
            // GUI wireframes
            new UiScreenType(), new UiButtonType(), new UiFieldType(), new UiListType(), new UiCheckboxType(), new UiToggleType(), new UiSliderType(),
            new UiImageType(), new UiIconType(), new UiTabsType(), new UiTouchType(), new UiCursorType(),
        };

        public IEnumerable<LibraryCategory> Categories => new[]
        {
            new LibraryCategory(DiagCategories.Structure, new I18nText("UML class & structure", "UML-Klassen & Struktur"), Id, 10),
            new LibraryCategory(DiagCategories.UseCase, new I18nText("UML use case", "UML-Anwendungsfall"), Id, 20),
            new LibraryCategory(DiagCategories.Activity, new I18nText("UML activity", "UML-Aktivität"), Id, 30),
            new LibraryCategory(DiagCategories.State, new I18nText("UML state machine", "UML-Zustandsautomat"), Id, 40),
            new LibraryCategory(DiagCategories.Sequence, new I18nText("UML sequence", "UML-Sequenz"), Id, 50),
            new LibraryCategory(DiagCategories.Relations, new I18nText("UML relationships", "UML-Beziehungen"), Id, 60),
            new LibraryCategory(DiagCategories.Er, new I18nText("Entity relationship (ER)", "Entity-Relationship (ER)"), Id, 70),
            new LibraryCategory(DiagCategories.System, new I18nText("System & architecture", "System & Architektur"), Id, 80),
            new LibraryCategory(DiagCategories.Bpmn, new I18nText("BPMN process", "BPMN-Prozess"), Id, 90),
            new LibraryCategory(DiagCategories.Control, new I18nText("Control loops", "Regelkreise"), Id, 100),
            new LibraryCategory(DiagCategories.Computer, new I18nText("Computer systems & communication", "Rechnersysteme & Kommunikation"), Id, 85),
            new LibraryCategory(DiagCategories.Data, new I18nText("Data layouts (packets, registers, memory)", "Datenformate (Pakete, Register, Speicher)"), Id, 110),
            new LibraryCategory(DiagCategories.Ui, new I18nText("GUI wireframes", "GUI-Wireframes"), Id, 120),
        };

        private static Dictionary<string, ParamValue> Ps(params (string Key, ParamValue Value)[] kv) => kv.ToDictionary(x => x.Key, x => x.Value);

        public IEnumerable<LibraryItem> Library
        {
            get
            {
                // One default item per type, plus presets.
                foreach (var t in AllTypes)
                    yield return new LibraryItem(t.Id, t.Id, t.Name, t.Category);

                LibraryItem Item(string id, string type, string en, string de, string category, params string[] keys)
                    => new(id, type, new I18nText(en, de), category) { Keywords = keys };

                // UML structure
                yield return Item("diag.uml.class.abstract", "diag.uml.class", "Abstract class", "Abstrakte Klasse", DiagCategories.Structure, "abstract", "abstrakt")
                    with { Params = Ps(("abstract", true)), Text = "Shape" };
                yield return Item("diag.uml.class.compact", "diag.uml.class", "Class (name only)", "Klasse (nur Name)", DiagCategories.Structure, "compact", "kompakt")
                    with { View = "compact", Size = new Size2(32, 10) };
                yield return Item("diag.uml.node.environment", "diag.uml.node", "Execution environment", "Ausführungsumgebung", DiagCategories.Structure, "runtime", "container", "jvm")
                    with { Params = Ps(("stereotype", "executionEnvironment")), Size = new Size2(50, 26) };

                // State machine: initial and final pseudo states reuse the activity control nodes.
                yield return Item("diag.state.initial", "diag.activity.initial", "Initial state", "Startzustand", DiagCategories.State, "initial", "start", "anfangszustand");
                yield return Item("diag.state.final", "diag.activity.final", "Final state", "Endzustand", DiagCategories.State, "final", "end", "ende");
                yield return Item("diag.state.activities", "diag.state.state", "State with entry/exit", "Zustand mit Ein-/Austritt", DiagCategories.State, "entry", "exit", "do")
                    with { Params = Ps(("entry", "open()"), ("exit", "close()")), Size = new Size2(40, 22) };
                yield return Item("diag.state.circle", "diag.state.state", "State (circle)", "Zustand (Kreis)", DiagCategories.State, "circle", "kreis", "patent", "bubble")
                    with { View = "circle", Size = new Size2(20, 20) };
                yield return Item("diag.state.transition", "diag.uml.relation", "Transition (event [guard] / action)", "Transition (Ereignis [Bedingung] / Aktion)", DiagCategories.State,
                        "transition", "übergang", "event", "ereignis", "guard", "bedingung")
                    with { Params = Ps(("kind", "directed")), Text = "event [guard] / action", Points = new[] { new Vec2(-25, 0), new Vec2(25, 0) } };

                // Sequence
                yield return Item("diag.seq.lifeline.actor", "diag.seq.lifeline", "Lifeline (actor)", "Lebenslinie (Akteur)", DiagCategories.Sequence, "actor", "akteur")
                    with { Params = Ps(("head", "actor"), ("headHeight", 16.0)), Text = "User" };
                yield return Item("diag.seq.message.async", "diag.seq.message", "Asynchronous message", "Asynchrone Nachricht", DiagCategories.Sequence, "async", "asynchron", "signal")
                    with { Params = Ps(("kind", "async")) };
                yield return Item("diag.seq.message.return", "diag.seq.message", "Reply message", "Antwortnachricht", DiagCategories.Sequence, "return", "reply", "antwort", "rückgabe")
                    with { Params = Ps(("kind", "return")), Points = new[] { new Vec2(20, 0), new Vec2(-20, 0) } };
                yield return Item("diag.seq.message.create", "diag.seq.message", "Create message", "Erzeugungsnachricht", DiagCategories.Sequence, "create", "new", "erzeugen")
                    with { Params = Ps(("kind", "create")) };
                yield return Item("diag.seq.message.self", "diag.seq.message", "Self message", "Selbstaufruf", DiagCategories.Sequence, "self", "selbst", "recursion", "rekursion")
                    with { Points = new[] { new Vec2(0, -4), new Vec2(0, 4) } };
                yield return Item("diag.seq.message.numbered", "diag.seq.message", "Message with step numeral", "Nachricht mit Schrittnummer", DiagCategories.Sequence, "step", "schritt", "numeral", "bezugszeichen", "302", "patent")
                    with { Params = Ps(("step", "302")) };
                yield return Item("diag.seq.lifeline.gap", "diag.seq.lifeline", "Lifeline with time gap", "Lebenslinie mit Zeitlücke", DiagCategories.Sequence, "time gap", "zeitlücke", "break", "unterbrechung", "//")
                    with { Params = Ps(("breaks", "50")) };
                foreach (var (kind, en, de, ops) in new[] { ("opt", "Optional fragment (opt)", "Optionales Fragment (opt)", 1), ("loop", "Loop fragment (loop)", "Schleifenfragment (loop)", 1),
                    ("par", "Parallel fragment (par)", "Paralleles Fragment (par)", 2), ("break", "Break fragment (break)", "Abbruchfragment (break)", 1) })
                    yield return Item("diag.seq.fragment." + kind, "diag.seq.fragment", en, de, DiagCategories.Sequence, kind)
                        with { Params = Ps(("kind", kind), ("operands", ops)), Size = new Size2(80, ops == 1 ? 26 : 40) };

                // Relationships
                foreach (var (value, en, de) in UmlRelationType.KindOptions.Skip(1))
                    yield return Item("diag.uml.relation." + value, "diag.uml.relation", en, de, DiagCategories.Relations, value) with { Params = Ps(("kind", value)) };
                yield return Item("diag.uml.relation.multiplicity", "diag.uml.relation", "Association with multiplicities", "Assoziation mit Multiplizitäten", DiagCategories.Relations, "multiplicity", "multiplizität", "1..*")
                    with { Params = Ps(("startMult", "1"), ("endMult", "0..*")), Points = new[] { new Vec2(-25, 0), new Vec2(25, 0) } };

                // ER
                yield return Item("diag.er.entity.table", "diag.er.entity", "Entity as table", "Entität als Tabelle", DiagCategories.Er, "table", "tabelle", "rows", "zeilen")
                    with { View = "table", Size = new Size2(50, 28) };
                yield return Item("diag.er.entity.chen", "diag.er.entity", "Entity (Chen)", "Entität (Chen)", DiagCategories.Er, "chen")
                    with { View = "chen", Size = new Size2(80, 46) };
                yield return Item("diag.er.entity.weak", "diag.er.entity", "Weak entity", "Schwache Entität", DiagCategories.Er, "weak", "schwach")
                    with { Params = Ps(("weak", true), ("attributes", "*#order_id: int\n*line_no: int\nqty: int")), Text = "OrderLine" };
                yield return Item("diag.er.relationship.identifying", "diag.er.relationship", "Identifying relationship", "Identifizierende Beziehung", DiagCategories.Er, "identifying", "weak", "schwach")
                    with { Params = Ps(("identifying", true)) };
                yield return Item("diag.er.attribute.key", "diag.er.attribute", "Key attribute", "Schlüsselattribut", DiagCategories.Er, "key", "primary", "primär")
                    with { Params = Ps(("key", true)), Text = "id" };
                yield return Item("diag.er.connector.onetoone", "diag.er.connector", "One-to-one (crow's foot)", "Eins-zu-eins (Krähenfuß)", DiagCategories.Er, "1:1", "one to one", "eins zu eins")
                    with { Params = Ps(("startEnd", "onlyOne"), ("endEnd", "zeroOrOne")) };
                yield return Item("diag.er.connector.manytomany", "diag.er.connector", "Many-to-many (crow's foot)", "Viele-zu-viele (Krähenfuß)", DiagCategories.Er, "n:m", "many to many", "viele zu viele")
                    with { Params = Ps(("startEnd", "oneOrMany"), ("endEnd", "zeroOrMany")) };

                // System / architecture
                yield return Item("diag.sys.block.sysml", "diag.sys.block", "SysML block", "SysML-Block", DiagCategories.System, "sysml", "block")
                    with { Params = Ps(("stereotype", "block"), ("portsLeft", 2), ("portsRight", 2)), Size = new Size2(44, 28) };
                yield return Item("diag.sys.lollipop.required", "diag.sys.lollipop", "Required interface (socket)", "Benötigte Schnittstelle (Buchse)", DiagCategories.System, "required", "socket", "buchse")
                    with { Params = Ps(("kind", "required")) };

                // BPMN
                yield return Item("diag.bpmn.gateway.parallel", "diag.bpmn.gateway", "Parallel gateway (BPMN)", "Paralleles Gateway (BPMN)", DiagCategories.Bpmn, "parallel", "and", "und")
                    with { Params = Ps(("marker", "parallel")) };
                yield return Item("diag.bpmn.gateway.inclusive", "diag.bpmn.gateway", "Inclusive gateway (BPMN)", "Inklusives Gateway (BPMN)", DiagCategories.Bpmn, "inclusive", "or", "oder")
                    with { Params = Ps(("marker", "inclusive")) };
                yield return Item("diag.bpmn.task.subprocess", "diag.bpmn.task", "Sub-process (BPMN)", "Teilprozess (BPMN)", DiagCategories.Bpmn, "subprocess", "teilprozess")
                    with { Params = Ps(("subprocess", true)), Text = "Sub-process" };

                // Control loops
                yield return Item("diag.ctrl.sum.cross", "diag.ctrl.sum", "Summing junction (with cross)", "Summierstelle (mit Kreuz)", DiagCategories.Control, "cross", "kreuz")
                    with { Params = Ps(("style", "cross")) };
                yield return Item("diag.ctrl.sum.three", "diag.ctrl.sum", "Summing junction (three inputs)", "Summierstelle (drei Eingänge)", DiagCategories.Control, "disturbance", "störgröße", "three", "drei")
                    with { Params = Ps(("signs", "+-+")) };
                foreach (var (role, en, de) in new[] { ("controller", "Controller (block)", "Regler (Block)"), ("plant", "Plant (block)", "Regelstrecke (Block)"),
                    ("actuator", "Actuator (block)", "Stellglied (Block)"), ("sensor", "Sensor (block)", "Messglied (Block)") })
                    yield return Item("diag.ctrl.tf." + role, "diag.ctrl.tf", en, de, DiagCategories.Control, role) with { Params = Ps(("role", role)) };
                yield return Item("diag.ctrl.tf.fraction", "diag.ctrl.tf", "Transfer function (fraction)", "Übertragungsfunktion (Bruch)", DiagCategories.Control, "fraction", "bruch", "laplace", "g(s)")
                    with { Params = Ps(("mode", "fraction")) };
                foreach (var (icon, en, de) in new[] { ("pid", "PID controller (step response)", "PID-Regler (Sprungantwort)"), ("pt1", "PT1 element", "PT1-Glied"),
                    ("pt2", "PT2 element", "PT2-Glied"), ("i", "I element (ramp)", "I-Glied (Rampe)"), ("d", "D element", "D-Glied"), ("deadtime", "Dead time element", "Totzeitglied"),
                    ("saturation", "Saturation (limiter)", "Begrenzung (Sättigung)"), ("integrator", "Integrator (∫)", "Integrierer (∫)") })
                    yield return Item("diag.ctrl.tf." + icon, "diag.ctrl.tf", en, de, DiagCategories.Control, icon, "kennlinie", "step response", "sprungantwort")
                        with { Params = Ps(("mode", "icon"), ("icon", icon)), Size = new Size2(16, 14) };
                yield return Item("diag.ctrl.mult.dot", "diag.ctrl.mult", "Multiplication point (dot)", "Multiplikationsstelle (Punkt)", DiagCategories.Control, "dot", "punkt")
                    with { Params = Ps(("symbol", "dot")) };
                yield return Item("diag.ctrl.loop.sensor", "diag.ctrl.loop", "Control loop with sensor", "Regelkreis mit Messglied", DiagCategories.Control, "sensor", "messglied", "measurement", "messung")
                    with { Params = Ps(("sensor", true)), Size = new Size2(120, 50) };

                // Computer systems (software patent FIG. 1): system blocks without port squares, named by their role.
                foreach (var (role, en, de) in new[] { ("processor", "Processor (block)", "Prozessor (Block)"), ("memory", "Memory (block)", "Speicher (Block)"),
                    ("storage", "Storage (block)", "Massenspeicher (Block)"), ("network", "Network interface (block)", "Netzwerkschnittstelle (Block)"),
                    ("io", "I/O interface (block)", "E/A-Schnittstelle (Block)"), ("display", "Display (block)", "Anzeige (Block)"),
                    ("sensor", "Sensor(s) (block)", "Sensor(en) (Block)"), ("input", "Input device (block)", "Eingabegerät (Block)") })
                    yield return Item("diag.sys.block." + role, "diag.sys.block", en, de, DiagCategories.Computer, role, "computer", "rechner", "fig. 1", "patent", "software")
                        with { Params = Ps(("role", role), ("portsLeft", 0), ("portsRight", 0)), Size = new Size2(40, 16) };
                yield return Item("diag.sys.bus.double", "diag.sys.bus", "System bus (double line)", "Systembus (Doppellinie)", DiagCategories.Computer, "double", "doppel", "hollow")
                    with { Params = Ps(("style", "double")) };
                yield return Item("diag.sys.wireless.arcs", "diag.sys.wireless", "Wireless link (radio arcs)", "Funkverbindung (Funkbögen)", DiagCategories.Computer, "arcs", "bögen", "dashed")
                    with { Params = Ps(("style", "arcs")) };

                // Data layouts
                yield return Item("diag.data.fields.register", "diag.data.fields", "Register / header (32-bit rows)", "Register / Header (32-Bit-Zeilen)", DiagCategories.Data, "register", "header", "32 bit", "rfc", "word", "wort")
                    with { Params = Ps(("layout", "wrapped"), ("rowBits", 32), ("ruler", true)), Size = new Size2(100, 30) };
                yield return Item("diag.data.fields.memory", "diag.data.fields", "Memory map", "Speicherbelegung", DiagCategories.Data, "memory", "speicher", "address", "adresse", "flash", "ram")
                    with { Params = Ps(("layout", "stack"), ("ruler", true), ("widths", "equal")), Size = new Size2(56, 60) };

                // GUI wireframes
                foreach (var (device, en, de, w, h) in new[] { ("tablet", "Screen: tablet", "Bildschirm: Tablet", 70.0, 50.0), ("monitor", "Screen: monitor", "Bildschirm: Monitor", 80.0, 62.0),
                    ("window", "Screen: application window", "Bildschirm: Programmfenster", 80.0, 55.0), ("browser", "Screen: browser", "Bildschirm: Browser", 90.0, 60.0) })
                    yield return Item("diag.ui.screen." + device, "diag.ui.screen", en, de, DiagCategories.Ui, device) with { Params = Ps(("device", device)), Size = new Size2(w, h) };
                yield return Item("diag.ui.button.primary", "diag.ui.button", "Primary button (filled)", "Primäre Schaltfläche (gefüllt)", DiagCategories.Ui, "primary", "primär", "filled", "gefüllt")
                    with { Params = Ps(("filled", true)) };
                yield return Item("diag.ui.field.search", "diag.ui.field", "Search field (GUI)", "Suchfeld (GUI)", DiagCategories.Ui, "search", "suche") with { Params = Ps(("kind", "search")) };
                yield return Item("diag.ui.field.dropdown", "diag.ui.field", "Drop-down (GUI)", "Auswahlliste (GUI)", DiagCategories.Ui, "dropdown", "combobox") with { Params = Ps(("kind", "dropdown")) };
                yield return Item("diag.ui.checkbox.radio", "diag.ui.checkbox", "Radio button (GUI)", "Optionsfeld (GUI)", DiagCategories.Ui, "radio", "optionsfeld") with { Params = Ps(("kind", "radio")) };
                yield return Item("diag.ui.touch.swipe", "diag.ui.touch", "Swipe gesture", "Wischgeste", DiagCategories.Ui, "swipe", "wischen") with { Params = Ps(("gesture", "swipe")) };
            }
        }
    }
}
