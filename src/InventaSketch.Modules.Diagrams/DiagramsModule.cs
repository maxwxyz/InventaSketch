using System;
using System.Collections.Generic;
using System.Linq;
using InventaSketch.Core;

namespace InventaSketch.Modules.Diagrams
{
    /// <summary>
    /// Diagrams module: UML (class, use case, activity, state, sequence, deployment, relationships), ER (entities,
    /// Chen notation, crow's-foot connectors), system/architecture blocks and BPMN-lite. Every box symbol exposes
    /// connector ports; connectors are point objects whose ends attach to those ports. Simple flowchart starters
    /// remain in the annotation module (<c>ann.flow.*</c>).
    /// </summary>
    public sealed class DiagramsModule : IModule
    {
        private static readonly ObjectType[] AllTypes = CreateTypes();

        public string Id => "diagrams";
        public I18nText Name => new("Diagrams (UML, ER, flow)", "Diagramme (UML, ER, Ablauf)");
        public string Version => "0.2.0";
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
            new LifelineType(), new ActivationType(), new MessageType(),
            // Relationships
            new UmlRelationType(),
            // ER
            new EntityType(), new ErAttributeType(), new ErRelationshipType(), new CrowsFootType(),
            // System / architecture
            new BlockType(), new ContainerType(), new CloudType(), new DatabaseType(), new QueueType(), new ServerType(),
            new ClientType(), new UserType(), new LollipopType(),
            // BPMN-lite
            new BpmnEventType(EventKind.Start), new BpmnEventType(EventKind.Intermediate), new BpmnEventType(EventKind.End),
            new BpmnTaskType(), new BpmnGatewayType(),
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
            }
        }
    }
}
