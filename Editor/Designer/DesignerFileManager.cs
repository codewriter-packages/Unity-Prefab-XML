using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML.Designer
{
    public static class DesignerFileManager
    {
        /// <summary>
        /// True while the manager itself creates or applies a designer file. Used to ignore
        /// asset saves and imports caused by the manager.
        /// </summary>
        public static bool IsBusy { get; private set; }

        public static string GetDesignerPath(string prefabXmlPath)
        {
            return ChangeExtension(prefabXmlPath, ".prefab");
        }

        // Asset paths always use forward slashes, Path.Combine does not
        private static string ChangeExtension(string assetPath, string extension)
        {
            var dir = (Path.GetDirectoryName(assetPath) ?? "").Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(assetPath);
            return dir.Length > 0 ? dir + "/" + name + extension : name + extension;
        }

        /// <summary>
        /// Returns the prefabxml asset that owns the given designer file,
        /// or null if the path is not a designer file.
        /// </summary>
        public static string GetPrefabXmlPath(string designerPath)
        {
            if (string.IsNullOrEmpty(designerPath) ||
                !designerPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var prefabXmlPath = ChangeExtension(designerPath, ".prefabxml");

            var guid = AssetDatabase.AssetPathToGUID(prefabXmlPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            return string.IsNullOrEmpty(guid) ? null : prefabXmlPath;
        }

        public static bool DesignerExists(string prefabXmlPath)
        {
            var designerPath = GetDesignerPath(prefabXmlPath);
            var designerGuid = AssetDatabase.AssetPathToGUID(designerPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            return !string.IsNullOrEmpty(designerGuid);
        }

        /// <summary>
        /// True when the designer file holds overrides that were not written back to the XML yet.
        /// Overrides of the root transform and of the root name are ignored: Unity records them on
        /// every variant, <see cref="ResetDesignerOverrides"/> does not clear them, and they carry
        /// no intent of the user.
        /// </summary>
        public static bool HasUnappliedModifications(string prefabXmlPath)
        {
            var designerPath = GetDesignerPath(prefabXmlPath);
            var designerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);

            if (designerPrefab == null || !PrefabUtility.IsPartOfPrefabInstance(designerPrefab))
            {
                return false;
            }

            return NotEmpty(PrefabUtility.GetAddedGameObjects(designerPrefab))
                   || NotEmpty(PrefabUtility.GetAddedComponents(designerPrefab))
                   || NotEmpty(PrefabUtility.GetRemovedGameObjects(designerPrefab))
                   || NotEmpty(PrefabUtility.GetRemovedComponents(designerPrefab))
                   || PrefabUtility.HasPrefabInstanceAnyOverrides(designerPrefab, includeDefaultOverrides: false);
        }

        private static bool NotEmpty<T>(List<T> list)
        {
            return list != null && list.Count > 0;
        }

        public static void CreateDesignerFile(string prefabXmlPath, bool focusDesignerFile = false)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabXmlPath);
            if (basePrefab == null)
            {
                Debug.LogError($"DesignerFile: Cannot load prefab from '{prefabXmlPath}'.");
                return;
            }

            var designerPath = GetDesignerPath(prefabXmlPath);

            IsBusy = true;
            try
            {
                var instance = (GameObject) PrefabUtility.InstantiatePrefab(basePrefab);
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, designerPath);
                    AssetDatabase.ImportAsset(designerPath, ImportAssetOptions.ForceSynchronousImport);
                    ResetDesignerOverrides(designerPath);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            finally
            {
                IsBusy = false;
            }

            var designerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);
            if (focusDesignerFile && designerAsset != null)
            {
                Selection.activeObject = designerAsset;
            }
        }

        public static void ApplyDesignerModifications(string prefabXmlPath)
        {
            IsBusy = true;
            try
            {
                ApplyDesignerModificationsInternal(prefabXmlPath);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static void ApplyDesignerModificationsInternal(string prefabXmlPath)
        {
            var designerPath = GetDesignerPath(prefabXmlPath);

            var designerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);
            if (designerPrefab == null)
            {
                Debug.LogError($"DesignerFile: Designer file not found at '{designerPath}'.");
                return;
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabXmlPath);
            if (basePrefab == null)
            {
                Debug.LogError($"DesignerFile: Cannot load base prefab from '{prefabXmlPath}'.");
                return;
            }

            var xmlText = File.ReadAllText(prefabXmlPath);
            var xmlDoc = XDocument.Parse(xmlText, LoadOptions.PreserveWhitespace);

            var rootXmlElement = xmlDoc.Root?.Elements("GameObject").FirstOrDefault();
            if (rootXmlElement == null)
            {
                Debug.LogError("DesignerFile: Cannot find root <GameObject> in XML.");
                return;
            }

            // Build context with parallel mapping and bindings
            var ctx = new DesignerContext
            {
                BasePrefab = basePrefab,
                DesignerPrefab = designerPrefab,
            };
            ctx.UsedBindingNames.UnionWith(PrefabXmlSerializer.CollectBindingNames(xmlDoc));

            BuildParallelMapping(basePrefab.transform, designerPrefab.transform, rootXmlElement, ctx);

            // Process property modifications
            var modifications = PrefabUtility.GetPropertyModifications(designerPrefab);
            if (modifications != null)
            {
                ApplyPropertyModifications(modifications, ctx);
            }

            // Handle added components
            var addedComponents = PrefabUtility.GetAddedComponents(designerPrefab);
            if (addedComponents != null)
            {
                ApplyAddedComponents(addedComponents, ctx);
            }

            // Handle added GameObjects
            var addedGameObjects = PrefabUtility.GetAddedGameObjects(designerPrefab);
            if (addedGameObjects != null)
            {
                ApplyAddedGameObjects(addedGameObjects, ctx);
            }

            // Handle removed components
            var removedComponents = PrefabUtility.GetRemovedComponents(designerPrefab);
            if (removedComponents != null)
            {
                ApplyRemovedComponents(removedComponents, ctx);
            }

            // Handle removed GameObjects
            var removedGameObjects = PrefabUtility.GetRemovedGameObjects(designerPrefab);
            if (removedGameObjects != null)
            {
                ApplyRemovedGameObjects(removedGameObjects, ctx);
            }

            // Handle reordered GameObjects. Runs last so that objects added and removed above
            // are already part of the XML.
            ApplyChildOrder(designerPrefab.transform, ctx);

            // Write XML back
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };

            var stringWriter = new StringWriter();
            using (var writer = XmlWriter.Create(stringWriter, settings))
            {
                xmlDoc.Save(writer);
            }

            // Nothing was captured from the designer file — leave both files untouched
            if (string.Equals(stringWriter.ToString(), xmlText, StringComparison.Ordinal))
            {
                return;
            }

            using (var writer = XmlWriter.Create(prefabXmlPath, settings))
            {
                xmlDoc.Save(writer);
            }

            // Reimport prefabxml
            AssetDatabase.ImportAsset(prefabXmlPath, ImportAssetOptions.ForceUpdate);

            // Apply new bindings if any
            if (ctx.NewBindings.Count > 0)
            {
                var importer = (PrefabXmlImporter) AssetImporter.GetAtPath(prefabXmlPath);
                var result = PrefabXmlImporter.GetResult(prefabXmlPath);

                if (importer != null && result != null)
                {
                    foreach (var kvp in ctx.NewBindings)
                    {
                        var bindingName = kvp.Key;
                        var asset = kvp.Value;

                        if (result.discoveredBindings.TryGetValue(bindingName, out var expectedType))
                        {
                            var identifier = new AssetImporter.SourceAssetIdentifier(expectedType, bindingName);
                            importer.AddRemap(identifier, asset);
                        }
                    }

                    AssetDatabase.WriteImportSettingsIfDirty(prefabXmlPath);
                    AssetDatabase.ImportAsset(prefabXmlPath, ImportAssetOptions.ForceUpdate);
                }
            }

            // The modifications now live in the XML, so the base prefab contains them too.
            // Overrides left on the designer file would duplicate every added object and would
            // be written to the XML again on the next apply.
            ResetDesignerOverrides(designerPath);
        }

        /// <summary>
        /// Reverts every override of the designer file so it becomes a clean variant
        /// of the freshly reimported base prefab.
        /// </summary>
        private static void ResetDesignerOverrides(string designerPath)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == designerPath)
            {
                // The stage holds its own copy of the contents. Overwriting the asset behind its
                // back would leave the stage stale and it would save the old overrides again.
                if (RevertRoot(stage.prefabContentsRoot))
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, designerPath);
                }

                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(designerPath);
            try
            {
                if (RevertRoot(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, designerPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool RevertRoot(GameObject root)
        {
            if (root == null || !PrefabUtility.IsPartOfPrefabInstance(root))
            {
                return false;
            }

            PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
            return true;
        }

        private static void BuildParallelMapping(Transform baseTf, Transform variantTf, XElement goElement,
            DesignerContext ctx)
        {
            MapBaseToXml(baseTf, goElement, ctx);
            MapBaseToVariant(baseTf, variantTf, ctx);
        }

        /// <summary>
        /// Maps the base prefab to the XML it was built from. Both are in the same order,
        /// so they can be matched by index.
        /// </summary>
        private static void MapBaseToXml(Transform baseTf, XElement goElement, DesignerContext ctx)
        {
            ctx.GoToXml[baseTf.gameObject.GetInstanceID()] = goElement;

            // Match components by type name
            var xmlCompElements = goElement.Elements()
                .Where(PrefabXmlUtils.IsComponentElement)
                .ToList();

            var usedXmlIndices = new HashSet<int>();
            foreach (var baseComp in GetSerializedComponents(baseTf))
            {
                var compType = baseComp.GetType();
                for (var i = 0; i < xmlCompElements.Count; i++)
                {
                    if (!usedXmlIndices.Contains(i) &&
                        PrefabXmlUtils.MatchesComponentType(xmlCompElements[i].Name.LocalName, compType))
                    {
                        ctx.CompToXml[baseComp.GetInstanceID()] = xmlCompElements[i];
                        usedXmlIndices.Add(i);
                        break;
                    }
                }
            }

            var xmlChildren = goElement.Elements("GameObject").ToList();
            var childCount = Math.Min(baseTf.childCount, xmlChildren.Count);

            for (var i = 0; i < childCount; i++)
            {
                MapBaseToXml(baseTf.GetChild(i), xmlChildren[i], ctx);
            }
        }

        /// <summary>
        /// Maps the base prefab to the designer file. Children are matched by prefab correspondence
        /// instead of by sibling index, so reordering them in the designer file keeps the mapping intact.
        /// </summary>
        private static void MapBaseToVariant(Transform baseTf, Transform variantTf, DesignerContext ctx)
        {
            var baseGoId = baseTf.gameObject.GetInstanceID();
            ctx.GoToVariant[baseGoId] = variantTf;

            if (ctx.GoToXml.TryGetValue(baseGoId, out var goElement))
            {
                ctx.VariantToXml[variantTf.GetInstanceID()] = goElement;
            }

            // Match base components to variant components by type in order
            var variantComps = GetSerializedComponents(variantTf);
            var usedVariantIndices = new HashSet<int>();

            foreach (var baseComp in GetSerializedComponents(baseTf))
            {
                var compType = baseComp.GetType();
                for (var i = 0; i < variantComps.Count; i++)
                {
                    if (!usedVariantIndices.Contains(i) && variantComps[i].GetType() == compType)
                    {
                        ctx.CompToVariant[baseComp.GetInstanceID()] = variantComps[i];
                        usedVariantIndices.Add(i);
                        break;
                    }
                }
            }

            var variantChildren = MapChildrenBySource(variantTf);

            for (var i = 0; i < baseTf.childCount; i++)
            {
                var baseChild = baseTf.GetChild(i);

                if (!variantChildren.TryGetValue(baseChild.gameObject.GetInstanceID(), out var variantChild))
                {
                    // The child either was removed in the designer file, or correspondence is not
                    // available at all — in the latter case fall back to matching by index
                    if (variantChildren.Count > 0 || i >= variantTf.childCount)
                    {
                        continue;
                    }

                    variantChild = variantTf.GetChild(i);
                }

                MapBaseToVariant(baseChild, variantChild, ctx);
            }
        }

        /// <summary>
        /// Returns the children of a designer object keyed by the instance id of the base object each
        /// one was instantiated from. Objects added in the designer file have no source and are skipped.
        /// </summary>
        private static Dictionary<int, Transform> MapChildrenBySource(Transform variantTf)
        {
            var map = new Dictionary<int, Transform>();

            for (var i = 0; i < variantTf.childCount; i++)
            {
                var child = variantTf.GetChild(i);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null)
                {
                    map[source.GetInstanceID()] = child;
                }
            }

            return map;
        }

        private static List<Component> GetSerializedComponents(Transform tf)
        {
            return tf.GetComponents<Component>()
                .Where(c => c != null && !PrefabXmlSerializer.SkipComponents.Contains(c.GetType().Name))
                .ToList();
        }

        /// <summary>
        /// Rewrites the order of the child elements in the XML to match the designer file.
        /// </summary>
        private static void ApplyChildOrder(Transform variantTf, DesignerContext ctx)
        {
            if (ctx.VariantToXml.TryGetValue(variantTf.GetInstanceID(), out var goElement))
            {
                var desired = new List<XElement>();

                for (var i = 0; i < variantTf.childCount; i++)
                {
                    if (ctx.VariantToXml.TryGetValue(variantTf.GetChild(i).GetInstanceID(), out var childElement))
                    {
                        desired.Add(childElement);
                    }
                }

                ReorderXmlChildren(goElement, desired);
            }

            for (var i = 0; i < variantTf.childCount; i++)
            {
                ApplyChildOrder(variantTf.GetChild(i), ctx);
            }
        }

        private static void ReorderXmlChildren(XElement parent, List<XElement> desired)
        {
            var current = parent.Elements("GameObject").ToList();

            // Only reorder when the designer file accounts for every child element. Anything else
            // means the mapping is incomplete and moving elements around would drop them.
            if (current.Count == 0 || current.Count != desired.Count)
            {
                return;
            }

            var known = new HashSet<XElement>();
            var changed = false;

            for (var i = 0; i < desired.Count; i++)
            {
                if (desired[i].Parent != parent || !known.Add(desired[i]))
                {
                    return;
                }

                changed |= current[i] != desired[i];
            }

            if (!changed)
            {
                return;
            }

            // Only the elements move, the whitespace between them is rebuilt from the existing
            // indentation, so the formatting of the file survives
            var indent = GetWhitespace(current[0].PreviousNode);

            // Whitespace in front of the closing tag. It is missing when a GameObject was added,
            // because the new element is appended behind it — the parent is indented the same way.
            var tail = GetWhitespace(parent.LastNode) ?? GetWhitespace(parent.PreviousNode);

            var anchor = new XElement("Reorder");
            if (indent != null)
            {
                current[0].PreviousNode.AddBeforeSelf(anchor);
            }
            else
            {
                current[0].AddBeforeSelf(anchor);
            }

            foreach (var element in current)
            {
                var whitespace = element.PreviousNode;
                if (GetWhitespace(whitespace) != null)
                {
                    whitespace.Remove();
                }

                element.Remove();
            }

            foreach (var element in desired)
            {
                if (indent != null)
                {
                    anchor.AddBeforeSelf(new XText(indent));
                }

                anchor.AddBeforeSelf(element);
            }

            anchor.Remove();

            if (tail != null && GetWhitespace(parent.LastNode) == null)
            {
                parent.Add(new XText(tail));
            }
        }

        private static string GetWhitespace(XNode node)
        {
            return node is XText text && string.IsNullOrWhiteSpace(text.Value) ? text.Value : null;
        }

        private static void ApplyPropertyModifications(PropertyModification[] modifications, DesignerContext ctx)
        {
            // Group modifications by target object
            var modsByTarget = modifications
                .Where(m => m.target != null)
                .GroupBy(m => m.target.GetInstanceID());

            foreach (var group in modsByTarget)
            {
                var target = group.First().target;
                var mods = group.ToList();

                // Handle GameObject-level modifications (name, active)
                if (target is GameObject targetGo)
                {
                    if (!ctx.GoToXml.TryGetValue(targetGo.GetInstanceID(), out var goXml))
                    {
                        continue;
                    }

                    foreach (var mod in mods)
                    {
                        switch (mod.propertyPath)
                        {
                            case "m_Name":
                                goXml.SetAttributeValue("name", mod.value);
                                break;

                            case "m_IsActive":
                                goXml.SetAttributeValue("active", mod.value == "1" ? "true" : "false");
                                break;
                        }
                    }

                    continue;
                }

                // Handle Component modifications
                if (target is not Component targetComp)
                {
                    continue;
                }

                if (!ctx.CompToXml.TryGetValue(targetComp.GetInstanceID(), out var xmlElement))
                {
                    continue;
                }

                if (!ctx.CompToVariant.TryGetValue(targetComp.GetInstanceID(), out var variantComp))
                {
                    continue;
                }

                // Filter out skip properties
                var validMods = mods.Where(m => !PrefabXmlSerializer.IsSkipProperty(m.propertyPath)).ToList();
                if (validMods.Count == 0)
                {
                    continue;
                }

                // Group by root XML attribute name
                var variantSo = new SerializedObject(variantComp);
                var attrGroups = GroupByXmlAttributeName(validMods, xmlElement, variantSo);

                foreach (var attrGroup in attrGroups)
                {
                    var attrName = attrGroup.Key;

                    // Read final value from variant's SerializedProperty
                    var prop = variantSo.FindProperty(attrName);
                    if (prop == null)
                    {
                        continue;
                    }

                    var convertCtx = ctx.CreateConvertContext();
                    var value = PrefabXmlSerializer.SerializeValue(prop, convertCtx);
                    ctx.CollectBindings(convertCtx);
                    if (value != null)
                    {
                        xmlElement.SetAttributeValue(attrName, value);
                    }
                    else
                    {
                        xmlElement.Attribute(attrName)?.Remove();
                    }
                }
            }
        }

        private static Dictionary<string, List<PropertyModification>> GroupByXmlAttributeName(
            List<PropertyModification> mods, XElement xmlElement, SerializedObject so)
        {
            var groups = new Dictionary<string, List<PropertyModification>>();

            foreach (var mod in mods)
            {
                var attrName = ResolveXmlAttributeName(mod.propertyPath, xmlElement, so);
                if (attrName == null)
                {
                    continue;
                }

                if (!groups.ContainsKey(attrName))
                {
                    groups[attrName] = new List<PropertyModification>();
                }

                groups[attrName].Add(mod);
            }

            return groups;
        }

        private static string ResolveXmlAttributeName(string propertyPath, XElement xmlElement, SerializedObject so)
        {
            // Try exact match
            if (xmlElement.Attribute(propertyPath) != null)
            {
                return propertyPath;
            }

            // For dot-paths, check if the immediate parent is a leaf (Vector2, Color, etc.)
            // If so, the parent is the XML attribute. If not (non-leaf struct like Navigation),
            // the full path is the attribute (dot-notation).
            var dotIndex = propertyPath.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                var parent = propertyPath.Substring(0, dotIndex);

                if (xmlElement.Attribute(parent) != null)
                {
                    return parent;
                }

                var parentProp = so.FindProperty(parent);
                if (parentProp != null)
                {
                    return PrefabXmlSerializer.IsLeafProperty(parentProp) ? parent : propertyPath;
                }
            }

            // No dots — check if it's a leaf property itself
            var rootProp = so.FindProperty(propertyPath);
            if (rootProp != null && PrefabXmlSerializer.IsLeafProperty(rootProp))
            {
                return propertyPath;
            }

            return null;
        }

        private static void ApplyAddedComponents(List<AddedComponent> addedComponents, DesignerContext ctx)
        {
            foreach (var added in addedComponents)
            {
                var comp = added.instanceComponent;
                if (comp == null)
                {
                    continue;
                }

                // Find the parent GO in the base prefab
                // The added component is on the variant, we need to find the corresponding base GO
                var variantGo = comp.gameObject;
                var baseGoId = ctx.FindBaseGoIdForVariantGo(variantGo);
                if (baseGoId == -1)
                {
                    continue;
                }

                if (!ctx.GoToXml.TryGetValue(baseGoId, out var goXml))
                {
                    continue;
                }

                // Serialize the component using PrefabToXmlConverter
                var convertCtx = ctx.CreateConvertContext();
                var compElement = PrefabXmlSerializer.SerializeComponent(comp, convertCtx);
                ctx.CollectBindings(convertCtx);

                // Insert before child GameObjects (after existing components)
                var lastComp = goXml.Elements().LastOrDefault(PrefabXmlUtils.IsComponentElement);
                if (lastComp != null)
                {
                    lastComp.AddAfterSelf(compElement);
                }
                else
                {
                    goXml.AddFirst(compElement);
                }
            }
        }

        private static void ApplyAddedGameObjects(List<AddedGameObject> addedGameObjects, DesignerContext ctx)
        {
            foreach (var added in addedGameObjects)
            {
                var go = added.instanceGameObject;
                if (go == null)
                {
                    continue;
                }

                // Find parent in base
                var parentVariantTf = go.transform.parent;
                if (parentVariantTf == null)
                {
                    continue;
                }

                var baseGoId = ctx.FindBaseGoIdForVariantTf(parentVariantTf);
                if (baseGoId == -1)
                {
                    continue;
                }

                if (!ctx.GoToXml.TryGetValue(baseGoId, out var parentXml))
                {
                    continue;
                }

                // Serialize the subtree
                var convertCtx = ctx.CreateConvertContext();
                var goElement = PrefabXmlSerializer.SerializeGameObject(go, convertCtx);
                ctx.CollectBindings(convertCtx);

                parentXml.Add(goElement);

                // The element is appended at the end, the reorder pass moves it
                // to the position it has in the designer file
                ctx.VariantToXml[go.transform.GetInstanceID()] = goElement;
            }
        }

        private static void ApplyRemovedComponents(List<RemovedComponent> removedComponents, DesignerContext ctx)
        {
            foreach (var removed in removedComponents)
            {
                var comp = removed.assetComponent;
                if (comp == null)
                {
                    continue;
                }

                if (ctx.CompToXml.TryGetValue(comp.GetInstanceID(), out var xmlElement))
                {
                    xmlElement.Remove();
                }
            }
        }

        private static void ApplyRemovedGameObjects(List<RemovedGameObject> removedGameObjects, DesignerContext ctx)
        {
            foreach (var removed in removedGameObjects)
            {
                var go = removed.assetGameObject;
                if (go == null)
                {
                    continue;
                }

                if (ctx.GoToXml.TryGetValue(go.GetInstanceID(), out var xmlElement))
                {
                    xmlElement.Remove();
                }
            }
        }
    }

    public class DesignerContext
    {
        public GameObject BasePrefab;
        public GameObject DesignerPrefab;

        // Mapping: base instanceID -> XML element / variant counterpart
        public readonly Dictionary<int, XElement> CompToXml = new Dictionary<int, XElement>();
        public readonly Dictionary<int, Component> CompToVariant = new Dictionary<int, Component>();
        public readonly Dictionary<int, XElement> GoToXml = new Dictionary<int, XElement>();
        public readonly Dictionary<int, Transform> GoToVariant = new Dictionary<int, Transform>();

        // Mapping: designer transform instanceID -> XML element, used to reorder the XML
        public readonly Dictionary<int, XElement> VariantToXml = new Dictionary<int, XElement>();

        // Bindings
        public readonly Dictionary<string, Object> NewBindings = new Dictionary<string, Object>();
        public readonly HashSet<string> UsedBindingNames = new HashSet<string>();

        public int FindBaseGoIdForVariantGo(GameObject variantGo)
        {
            foreach (var kvp in GoToVariant)
            {
                if (kvp.Value.gameObject == variantGo)
                    return kvp.Key;
            }

            return -1;
        }

        public int FindBaseGoIdForVariantTf(Transform variantTf)
        {
            foreach (var kvp in GoToVariant)
            {
                if (kvp.Value == variantTf)
                    return kvp.Key;
            }

            return -1;
        }

        public PrefabXmlSerializer.PrefabXmlSerializationContext CreateConvertContext()
        {
            var convertCtx = new PrefabXmlSerializer.PrefabXmlSerializationContext
            {
                Root = DesignerPrefab,
            };

            // Seed with already-known bindings so new names don't collide
            foreach (var kvp in NewBindings)
            {
                convertCtx.UsedBindings[kvp.Key] = kvp.Value;
            }

            PrefabXmlSerializer.AssignIds(DesignerPrefab.transform, convertCtx);
            return convertCtx;
        }

        public void CollectBindings(PrefabXmlSerializer.PrefabXmlSerializationContext serializationCtx)
        {
            foreach (var kvp in serializationCtx.UsedBindings)
            {
                if (!NewBindings.ContainsKey(kvp.Key))
                {
                    NewBindings[kvp.Key] = kvp.Value;
                    UsedBindingNames.Add(kvp.Key);
                }
            }
        }
    }
}