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
    /// Works out what applying the designer file would write, without writing any of it. Every
    /// decision is made here and by the handlers it runs, so what the table shows and what lands in
    /// the file cannot disagree.
    /// </summary>
    public static class DesignerChangeCollector
    {
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
                    Classify(set, GameObjectRequest(set, targetGo), mods);
                    continue;
                }

                if (target is Component targetComp)
                {
                    Classify(set, ComponentRequest(set, targetComp), DivertChildOrder(set, targetComp, mods, rootOrderMods));
                }
            }
        }

        private static DesignerChangeRequest GameObjectRequest(DesignerChangeSet set, GameObject targetGo)
        {
            set.Context.GoToXml.TryGetValue(targetGo.GetInstanceID(), out var goXml);

            return new DesignerChangeRequest
            {
                Set = set,
                TargetGameObject = targetGo,
                Element = goXml,
                ObjectPath = DesignerChange.GetPath(targetGo.transform, set.BasePrefab.transform),
            };
        }

        private static DesignerChangeRequest ComponentRequest(DesignerChangeSet set, Component targetComp)
        {
            var componentId = targetComp.GetInstanceID();
            set.Context.CompToXml.TryGetValue(componentId, out var xmlElement);
            set.Context.CompToVariant.TryGetValue(componentId, out var variantComp);

            return new DesignerChangeRequest
            {
                Set = set,
                Element = xmlElement,
                VariantObject = variantComp == null ? null : new SerializedObject(variantComp),
                ObjectPath = DesignerChange.GetPath(targetComp.transform, set.BasePrefab.transform),
                ComponentType = targetComp.GetType().Name,
                ComponentIndex = DesignerChange.IndexOfComponent(targetComp),
            };
        }

        /// <summary>
        /// Takes the sibling index out of the modifications of a component and files it under the
        /// parent. Child order is one change of the parent object, not a change of every child, and
        /// the row for it is built from the hierarchy — these modifications only tell the selective
        /// revert what to clear once the order was written.
        /// </summary>
        private static List<PropertyModification> DivertChildOrder(DesignerChangeSet set, Component targetComp,
            List<PropertyModification> mods, Dictionary<XElement, List<PropertyModification>> rootOrderMods)
        {
            var parent = targetComp.transform.parent;
            if (parent == null ||
                !set.Context.GoToXml.TryGetValue(parent.gameObject.GetInstanceID(), out var parentXml))
            {
                return mods;
            }

            var rest = new List<PropertyModification>();

            foreach (var mod in mods)
            {
                if (mod.propertyPath != "m_RootOrder")
                {
                    rest.Add(mod);
                    continue;
                }

                if (!rootOrderMods.TryGetValue(parentXml, out var list))
                {
                    rootOrderMods[parentXml] = list = new List<PropertyModification>();
                }

                list.Add(mod);
            }

            return rest;
        }

        /// <summary>
        /// Runs the modifications of one target through the registry and folds the ones that answer
        /// with the same handler and key into a single change.
        /// </summary>
        private static void Classify(DesignerChangeSet set, DesignerChangeRequest request,
            List<PropertyModification> mods)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, ClaimGroup>();

            foreach (var mod in mods)
            {
                request.Modification = mod;

                var claimer = DesignerChangeHandlers.Claim(request, out var key);
                var id = claimer.GetType().Name + "|" + key;

                if (!groups.TryGetValue(id, out var group))
                {
                    groups[id] = group = new ClaimGroup {Claimer = claimer, Key = key};
                    order.Add(id);
                }

                group.Mods.Add(mod);
            }

            foreach (var id in order)
            {
                var group = groups[id];
                request.Modification = group.Mods[0];

                var change = group.Claimer.Build(request, group.Key, group.Mods);
                if (change == null)
                {
                    continue;
                }

                // A claimer that can write owns the writing of its own changes. The ones that only
                // explain why a change is left out write nothing and say so.
                change.Writer = group.Claimer as IDesignerChangeWriter;

                if (change.Problem == null && change.Writer == null)
                {
                    change.Problem = DesignerChangeProblems.NoWriter;
                }

                change.Sources.AddRange(group.Mods);
                set.Changes.Add(change);
            }
        }

        private sealed class ClaimGroup
        {
            public IDesignerChangeClaimer Claimer;
            public string Key;
            public readonly List<PropertyModification> Mods = new List<PropertyModification>();
        }

        // ------------------------------------------------------------------------------- structure

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
                    Writer = DesignerChangeHandlers.AddedComponent,
                    ObjectPath = DesignerChange.GetPath(comp.transform, set.DesignerPrefab.transform),
                    ComponentType = comp.GetType().Name,
                    ComponentIndex = DesignerChange.IndexOfComponent(comp),
                    PropertyPath = comp.GetType().Name,
                    Label = "added component",
                    NewValue = comp.GetType().Name,
                };

                var baseGoId = set.Context.FindBaseGoIdForVariantGo(comp.gameObject);
                if (baseGoId == -1 || !set.Context.GoToXml.TryGetValue(baseGoId, out var goXml))
                {
                    change.Problem = DesignerChangeProblems.NoXmlObject;
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
                    Writer = DesignerChangeHandlers.AddedGameObject,
                    ObjectPath = DesignerChange.GetPath(go.transform, set.DesignerPrefab.transform),
                    PropertyPath = go.name,
                    Label = "added object",
                    NewValue = go.name,
                    VariantTransform = go.transform,
                };

                var baseGoId = set.Context.FindBaseGoIdForVariantTf(go.transform.parent);
                if (baseGoId == -1 || !set.Context.GoToXml.TryGetValue(baseGoId, out var parentXml))
                {
                    change.Problem = DesignerChangeProblems.NoXmlObject;
                    set.Changes.Add(change);
                    continue;
                }

                change.TargetElement = parentXml;
                change.PayloadElement = PrefabXmlSerializer.SerializeGameObject(go, set.ConvertContext);
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
                    Writer = DesignerChangeHandlers.RemovedElement,
                    ObjectPath = DesignerChange.GetPath(comp.transform, set.BasePrefab.transform),
                    ComponentType = comp.GetType().Name,
                    ComponentIndex = DesignerChange.IndexOfComponent(comp),
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
                    Writer = DesignerChangeHandlers.RemovedElement,
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
    }
}