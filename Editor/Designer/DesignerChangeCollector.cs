using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Everything one run of the applier works on: the document, the mapping between the prefabs and
    /// the XML, and the list of changes the designer file holds.
    /// </summary>
    public sealed class DesignerChangeSet
    {
        public string PrefabXmlPath;
        public string DesignerPath;
        public GameObject BasePrefab;
        public GameObject DesignerPrefab;

        public XDocument Document;
        public string DocumentText;

        public DesignerContext Context;
        public PrefabXmlSerializer.PrefabXmlSerializationContext ConvertContext;

        public readonly List<DesignerChange> Changes = new List<DesignerChange>();

        /// <summary>Changes the user is asked about.</summary>
        public IEnumerable<DesignerChange> Actionable => Changes.Where(c => c.IsVisible);

        /// <summary>Changes that carry a reason why they are not written.</summary>
        public IEnumerable<DesignerChange> Rejected => Changes.Where(c => c.Problem != null);

        public bool HasActionable => Changes.Any(c => c.IsVisible);
    }

    /// <summary>
    /// Works out what applying the designer file would write, without writing any of it. The applier
    /// runs off the result, so what the table shows and what lands in the file is decided once.
    /// </summary>
    public static class DesignerChangeCollector
    {
        private const string ProblemNoXmlObject = "the object is not described by the XML";
        private const string ProblemNoXmlComponent = "the component is not described by the XML";
        private const string ProblemNoVariant = "the component is missing from the designer file";
        private const string ProblemNotAnAttribute = "not a valid attribute name";
        private const string ProblemNoAttribute = "no attribute of the format matches this property";
        private const string ProblemUnsupportedType = "the format cannot write this value type";
        private const string ProblemManagedReferences = "arrays of managed references are not written";
        private const string ProblemGameObjectProperty = "only the name and the active state of an object are written";
        private const string ProblemSkipped = "the format never writes this property";
        private const string ProblemDerived = "the value is derived from another property of the file";

        public static DesignerChangeSet Collect(string prefabXmlPath, bool logErrors = false)
        {
            var designerPath = DesignerFileManager.GetDesignerPath(prefabXmlPath);

            var designerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);
            if (designerPrefab == null)
            {
                Log(logErrors, $"DesignerFile: Designer file not found at '{designerPath}'.");
                return null;
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabXmlPath);
            if (basePrefab == null)
            {
                Log(logErrors, $"DesignerFile: Cannot load base prefab from '{prefabXmlPath}'.");
                return null;
            }

            XDocument document;
            string documentText;
            try
            {
                document = PrefabXmlUtils.LoadXml(prefabXmlPath, out documentText);
            }
            catch (System.Xml.XmlException e)
            {
                Log(logErrors, $"DesignerFile: Cannot read '{prefabXmlPath}': {e.Message}");
                return null;
            }

            var rootXmlElement = document.Root?.Elements("GameObject").FirstOrDefault();
            if (rootXmlElement == null)
            {
                Log(logErrors, "DesignerFile: Cannot find root <GameObject> in XML.");
                return null;
            }

            var ctx = new DesignerContext
            {
                BasePrefab = basePrefab,
                DesignerPrefab = designerPrefab,
            };
            ctx.UsedBindingNames.UnionWith(PrefabXmlSerializer.CollectBindingNames(document));

            DesignerFileManager.BuildParallelMapping(basePrefab.transform, designerPrefab.transform,
                rootXmlElement, ctx);

            var set = new DesignerChangeSet
            {
                PrefabXmlPath = prefabXmlPath,
                DesignerPath = designerPath,
                BasePrefab = basePrefab,
                DesignerPrefab = designerPrefab,
                Document = document,
                DocumentText = documentText,
                Context = ctx,

                // One context for the whole pass: the ids it hands out stay consistent between the
                // rows, and the bindings it finds are only promoted when the change is applied
                ConvertContext = ctx.CreateConvertContext(),
            };

            var rootOrderMods = new Dictionary<XElement, List<PropertyModification>>();

            CollectPropertyChanges(set, rootOrderMods);
            CollectAddedComponents(set);
            CollectAddedGameObjects(set);
            CollectRemovedComponents(set);
            CollectRemovedGameObjects(set);
            CollectChildOrder(set, rootOrderMods);

            return set;
        }

        private static void Log(bool logErrors, string message)
        {
            if (logErrors)
            {
                Debug.LogError(message);
            }
        }

        // ------------------------------------------------------------------ property modifications

        private static void CollectPropertyChanges(DesignerChangeSet set,
            Dictionary<XElement, List<PropertyModification>> rootOrderMods)
        {
            var modifications = PrefabUtility.GetPropertyModifications(set.DesignerPrefab);
            if (modifications == null)
            {
                return;
            }

            var byTarget = modifications
                .Where(m => m.target != null)
                .GroupBy(m => m.target.GetInstanceID());

            foreach (var group in byTarget)
            {
                var target = group.First().target;
                var mods = group.ToList();

                if (target is GameObject targetGo)
                {
                    CollectGameObjectChanges(set, targetGo, mods);
                    continue;
                }

                if (target is Component targetComp)
                {
                    CollectComponentChanges(set, targetComp, mods, rootOrderMods);
                }
            }
        }

        private static void CollectGameObjectChanges(DesignerChangeSet set, GameObject targetGo,
            List<PropertyModification> mods)
        {
            var objectPath = DesignerChange.GetPath(targetGo.transform, set.BasePrefab.transform);
            set.Context.GoToXml.TryGetValue(targetGo.GetInstanceID(), out var goXml);

            foreach (var mod in mods)
            {
                var change = new DesignerChange
                {
                    ObjectPath = objectPath,
                    PropertyPath = mod.propertyPath,
                    TargetElement = goXml,
                };
                change.Sources.Add(mod);

                switch (mod.propertyPath)
                {
                    case "m_Name":
                        change.Kind = DesignerChangeKind.GameObjectName;
                        change.Label = "name";
                        change.NewValue = mod.value;
                        change.OldValue = goXml?.Attribute("name")?.Value;
                        break;

                    case "m_IsActive":
                        change.Kind = DesignerChangeKind.GameObjectActive;
                        change.Label = "active";
                        change.NewValue = mod.value == "1" ? "true" : "false";
                        change.OldValue = goXml?.Attribute("active")?.Value ?? "true";
                        break;

                    default:
                        change.Kind = DesignerChangeKind.Property;
                        change.Label = mod.propertyPath;
                        change.Problem = ProblemGameObjectProperty;
                        break;
                }

                if (change.Problem == null && goXml == null)
                {
                    change.Problem = ProblemNoXmlObject;
                }

                change.Redundant = change.Problem == null && change.NewValue == change.OldValue;
                set.Changes.Add(change);
            }
        }

        private static void CollectComponentChanges(DesignerChangeSet set, Component targetComp,
            List<PropertyModification> mods, Dictionary<XElement, List<PropertyModification>> rootOrderMods)
        {
            var componentId = targetComp.GetInstanceID();
            var objectPath = DesignerChange.GetPath(targetComp.transform, set.BasePrefab.transform);
            var componentType = targetComp.GetType().Name;
            var componentIndex = IndexOfComponent(targetComp);

            set.Context.CompToXml.TryGetValue(componentId, out var xmlElement);
            set.Context.CompToVariant.TryGetValue(componentId, out var variantComp);

            DesignerChange NewChange(PropertyModification mod, string propertyPath) => new DesignerChange
            {
                Kind = DesignerChangeKind.Property,
                ObjectPath = objectPath,
                ComponentType = componentType,
                ComponentIndex = componentIndex,
                PropertyPath = propertyPath,
                Label = propertyPath,
                TargetElement = xmlElement,
            };

            // Nothing can be written without both sides of the mapping. The properties the format
            // never writes are still called out as such — a component missing from the XML is not
            // the reason they are left alone.
            if (xmlElement == null || variantComp == null)
            {
                foreach (var mod in mods)
                {
                    var change = NewChange(mod, mod.propertyPath);
                    change.Problem = PrefabXmlSerializer.IsSkipProperty(mod.propertyPath)
                        ? ProblemSkipped
                        : xmlElement == null
                            ? ProblemNoXmlComponent
                            : ProblemNoVariant;
                    change.Sources.Add(mod);
                    set.Changes.Add(change);
                }

                return;
            }

            var variantSo = new SerializedObject(variantComp);

            var plain = new List<PropertyModification>();
            var arrays = new Dictionary<string, List<PropertyModification>>();

            foreach (var mod in mods)
            {
                if (mod.propertyPath == "m_RootOrder")
                {
                    // Child order is one change of the parent, not of every child on its own
                    var parent = targetComp.transform.parent;
                    if (parent != null &&
                        set.Context.GoToXml.TryGetValue(parent.gameObject.GetInstanceID(), out var parentXml))
                    {
                        if (!rootOrderMods.TryGetValue(parentXml, out var list))
                        {
                            rootOrderMods[parentXml] = list = new List<PropertyModification>();
                        }

                        list.Add(mod);
                        continue;
                    }
                }

                if (PrefabXmlSerializer.IsSkipProperty(mod.propertyPath))
                {
                    var skipped = NewChange(mod, mod.propertyPath);
                    skipped.Problem = ProblemSkipped;
                    skipped.Sources.Add(mod);
                    set.Changes.Add(skipped);
                    continue;
                }

                var arrayPath = DesignerFileManager.GetArrayPath(mod.propertyPath);
                if (arrayPath != null)
                {
                    if (!arrays.TryGetValue(arrayPath, out var list))
                    {
                        arrays[arrayPath] = list = new List<PropertyModification>();
                    }

                    list.Add(mod);
                    continue;
                }

                plain.Add(mod);
            }

            foreach (var array in arrays)
            {
                set.Changes.Add(ClassifyArray(set, NewChange(array.Value[0], array.Key), array.Value,
                    xmlElement, variantSo));
            }

            CollectPlainChanges(set, plain, xmlElement, variantSo, NewChange);
        }

        /// <summary>
        /// Folds the modifications into one change per XML attribute: the x and the y of a Vector2 are
        /// one attribute of the file, and the file is written from the value the property holds now.
        /// </summary>
        private static void CollectPlainChanges(DesignerChangeSet set, List<PropertyModification> mods,
            XElement xmlElement, SerializedObject variantSo,
            System.Func<PropertyModification, string, DesignerChange> newChange)
        {
            var groups = new Dictionary<string, List<PropertyModification>>();

            foreach (var mod in mods)
            {
                if (!DesignerFileManager.IsXmlName(mod.propertyPath))
                {
                    var invalid = newChange(mod, mod.propertyPath);
                    invalid.Problem = ProblemNotAnAttribute;
                    invalid.Sources.Add(mod);
                    set.Changes.Add(invalid);
                    continue;
                }

                var attrName = DesignerFileManager.ResolveXmlAttributeName(mod.propertyPath, xmlElement, variantSo);
                if (attrName == null)
                {
                    var unknown = newChange(mod, mod.propertyPath);
                    unknown.Problem = ProblemNoAttribute;
                    unknown.Sources.Add(mod);
                    set.Changes.Add(unknown);
                    continue;
                }

                if (!groups.TryGetValue(attrName, out var list))
                {
                    groups[attrName] = list = new List<PropertyModification>();
                }

                list.Add(mod);
            }

            foreach (var group in groups)
            {
                var attrName = group.Key;
                var change = newChange(group.Value[0], attrName);
                change.Sources.AddRange(group.Value);
                change.OldValue = xmlElement.Attribute(attrName)?.Value;

                var prop = variantSo.FindProperty(attrName);
                if (prop == null)
                {
                    change.Problem = ProblemNoAttribute;
                    set.Changes.Add(change);
                    continue;
                }

                change.NewValue = PrefabXmlSerializer.SerializeValue(prop, set.ConvertContext);

                // A leaf with no text is an empty reference, and clearing the attribute is how the
                // format writes it. Anything else means the type has no representation at all.
                if (change.NewValue == null && !PrefabXmlSerializer.IsLeafProperty(prop))
                {
                    change.Problem = ProblemUnsupportedType;
                }

                change.Redundant = change.Problem == null && change.NewValue == change.OldValue;
                set.Changes.Add(change);
            }
        }

        private static DesignerChange ClassifyArray(DesignerChangeSet set, DesignerChange change,
            List<PropertyModification> mods, XElement xmlElement, SerializedObject variantSo)
        {
            change.Kind = DesignerChangeKind.ArrayField;
            change.Sources.AddRange(mods);

            var existing = xmlElement.Elements("Field")
                .FirstOrDefault(el => el.Attribute("name")?.Value == change.PropertyPath);

            change.OldValue = existing == null
                ? null
                : $"{existing.Elements("Item").Count()} items";

            var prop = variantSo.FindProperty(change.PropertyPath);
            if (prop == null || !prop.isArray || prop.propertyType == SerializedPropertyType.String)
            {
                change.Problem = ProblemNoAttribute;
                return change;
            }

            // Left out of the file while it holds nothing but what another property derives. Once the
            // file spells the value out, it is the file that decides and the value is written again.
            if (existing == null && TmpFontFeatures.IsDerivedFromKerningField(prop))
            {
                change.Problem = ProblemDerived;
                return change;
            }

            var refs = new List<XElement>();
            change.PayloadElement = PrefabXmlSerializer.SerializeField(prop, set.ConvertContext, refs);

            if (refs.Count > 0)
            {
                // Every managed reference lives in a Ref element of its own. Writing them would mean
                // renumbering the ids the file already uses and dropping the ones the old items left
                // behind, so the array is left as it is.
                change.Problem = ProblemManagedReferences;
                change.PayloadElement = null;
                return change;
            }

            change.NewValue = change.PayloadElement == null
                ? null
                : $"{change.PayloadElement.Elements("Item").Count()} items";

            change.Redundant = change.PayloadElement == null
                ? existing == null
                : existing != null && XNode.DeepEquals(Normalized(existing), change.PayloadElement);

            return change;
        }

        /// <summary>
        /// A copy without the whitespace the file is formatted with, so it can be compared to a
        /// freshly built element.
        /// </summary>
        private static XElement Normalized(XElement element)
        {
            var copy = new XElement(element);
            foreach (var text in copy.DescendantNodes().OfType<XText>().ToList())
            {
                if (string.IsNullOrWhiteSpace(text.Value))
                {
                    text.Remove();
                }
            }

            return copy;
        }

        // ------------------------------------------------------------------------- structure

        private static void CollectAddedComponents(DesignerChangeSet set)
        {
            var added = PrefabUtility.GetAddedComponents(set.DesignerPrefab);
            if (added == null)
            {
                return;
            }

            foreach (var entry in added)
            {
                var comp = entry.instanceComponent;
                if (comp == null)
                {
                    continue;
                }

                var change = new DesignerChange
                {
                    Kind = DesignerChangeKind.AddedComponent,
                    ObjectPath = DesignerChange.GetPath(comp.transform, set.DesignerPrefab.transform),
                    ComponentType = comp.GetType().Name,
                    ComponentIndex = IndexOfComponent(comp),
                    PropertyPath = comp.GetType().Name,
                    Label = "added component",
                    NewValue = comp.GetType().Name,
                };

                var baseGoId = set.Context.FindBaseGoIdForVariantGo(comp.gameObject);
                if (baseGoId == -1 || !set.Context.GoToXml.TryGetValue(baseGoId, out var goXml))
                {
                    change.Problem = ProblemNoXmlObject;
                    set.Changes.Add(change);
                    continue;
                }

                change.TargetElement = goXml;
                change.PayloadElement = PrefabXmlSerializer.SerializeComponent(comp, set.ConvertContext);
                set.Changes.Add(change);
            }
        }

        private static void CollectAddedGameObjects(DesignerChangeSet set)
        {
            var added = PrefabUtility.GetAddedGameObjects(set.DesignerPrefab);
            if (added == null)
            {
                return;
            }

            foreach (var entry in added)
            {
                var go = entry.instanceGameObject;
                if (go == null || go.transform.parent == null)
                {
                    continue;
                }

                var change = new DesignerChange
                {
                    Kind = DesignerChangeKind.AddedGameObject,
                    ObjectPath = DesignerChange.GetPath(go.transform, set.DesignerPrefab.transform),
                    PropertyPath = go.name,
                    Label = "added object",
                    NewValue = go.name,
                };

                var baseGoId = set.Context.FindBaseGoIdForVariantTf(go.transform.parent);
                if (baseGoId == -1 || !set.Context.GoToXml.TryGetValue(baseGoId, out var parentXml))
                {
                    change.Problem = ProblemNoXmlObject;
                    set.Changes.Add(change);
                    continue;
                }

                change.TargetElement = parentXml;
                change.PayloadElement = PrefabXmlSerializer.SerializeGameObject(go, set.ConvertContext);
                change.VariantTransform = go.transform;
                set.Changes.Add(change);
            }
        }

        private static void CollectRemovedComponents(DesignerChangeSet set)
        {
            var removed = PrefabUtility.GetRemovedComponents(set.DesignerPrefab);
            if (removed == null)
            {
                return;
            }

            foreach (var entry in removed)
            {
                var comp = entry.assetComponent;
                if (comp == null)
                {
                    continue;
                }

                set.Context.CompToXml.TryGetValue(comp.GetInstanceID(), out var xmlElement);

                set.Changes.Add(new DesignerChange
                {
                    Kind = DesignerChangeKind.RemovedComponent,
                    ObjectPath = DesignerChange.GetPath(comp.transform, set.BasePrefab.transform),
                    ComponentType = comp.GetType().Name,
                    ComponentIndex = IndexOfComponent(comp),
                    PropertyPath = comp.GetType().Name,
                    Label = "removed component",
                    NewValue = comp.GetType().Name,
                    TargetElement = xmlElement,

                    // Not in the file to begin with, so there is nothing to write
                    Redundant = xmlElement == null,
                });
            }
        }

        private static void CollectRemovedGameObjects(DesignerChangeSet set)
        {
            var removed = PrefabUtility.GetRemovedGameObjects(set.DesignerPrefab);
            if (removed == null)
            {
                return;
            }

            foreach (var entry in removed)
            {
                var go = entry.assetGameObject;
                if (go == null)
                {
                    continue;
                }

                set.Context.GoToXml.TryGetValue(go.GetInstanceID(), out var xmlElement);

                set.Changes.Add(new DesignerChange
                {
                    Kind = DesignerChangeKind.RemovedGameObject,
                    ObjectPath = DesignerChange.GetPath(go.transform, set.BasePrefab.transform),
                    PropertyPath = go.name,
                    Label = "removed object",
                    NewValue = go.name,
                    TargetElement = xmlElement,
                    Redundant = xmlElement == null,
                });
            }
        }

        /// <summary>
        /// One change per parent whose children sit in a different order than the file describes.
        /// </summary>
        private static void CollectChildOrder(DesignerChangeSet set,
            Dictionary<XElement, List<PropertyModification>> rootOrderMods)
        {
            CollectChildOrder(set, set.DesignerPrefab.transform, rootOrderMods);
        }

        private static void CollectChildOrder(DesignerChangeSet set, Transform variantTf,
            Dictionary<XElement, List<PropertyModification>> rootOrderMods)
        {
            if (set.Context.VariantToXml.TryGetValue(variantTf.GetInstanceID(), out var goElement))
            {
                var desired = new List<XElement>();

                for (var i = 0; i < variantTf.childCount; i++)
                {
                    if (set.Context.VariantToXml.TryGetValue(variantTf.GetChild(i).GetInstanceID(),
                            out var childElement))
                    {
                        desired.Add(childElement);
                    }
                }

                if (DesignerFileManager.WouldReorder(goElement, desired))
                {
                    var change = new DesignerChange
                    {
                        Kind = DesignerChangeKind.ChildOrder,
                        ObjectPath = DesignerChange.GetPath(variantTf, set.DesignerPrefab.transform),
                        PropertyPath = "children",
                        Label = "child order",
                        NewValue = string.Join(", ", desired.Select(el => el.Attribute("name")?.Value)),
                        TargetElement = goElement,
                        DesiredChildren = desired,
                    };

                    if (rootOrderMods.TryGetValue(goElement, out var sources))
                    {
                        change.Sources.AddRange(sources);
                    }

                    set.Changes.Add(change);
                }
            }

            for (var i = 0; i < variantTf.childCount; i++)
            {
                CollectChildOrder(set, variantTf.GetChild(i), rootOrderMods);
            }
        }

        private static int IndexOfComponent(Component comp)
        {
            return DesignerChange.IndexOfComponent(comp);
        }
    }
}