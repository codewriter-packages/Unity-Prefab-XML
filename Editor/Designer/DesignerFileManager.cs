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
        /// True when the designer file holds a change the applier can write and has not written yet.
        ///
        /// Unity records overrides the format never writes — properties it skips, values it derives
        /// from another field, and the root transform of every variant. Counting those would leave
        /// the warning on forever, because applying them changes nothing, so the answer comes from
        /// the collected changes instead of from the raw override list.
        /// </summary>
        public static bool HasUnappliedModifications(string prefabXmlPath)
        {
            // Cheap enough to run on every repaint, and false here means there is nothing to collect
            if (!HasAnyOverride(prefabXmlPath))
            {
                return false;
            }

            var set = DesignerChangeCollector.Collect(prefabXmlPath);
            return set != null && set.HasActionable;
        }

        /// <summary>
        /// True when the designer file was touched at all, whether or not the format writes any of
        /// it. Answering this costs a fraction of collecting the changes.
        /// </summary>
        internal static bool HasAnyOverride(string prefabXmlPath)
        {
            var designerPath = GetDesignerPath(prefabXmlPath);
            var designerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);

            return designerPrefab != null
                   && PrefabUtility.IsPartOfPrefabInstance(designerPrefab)
                   && HasAnyOverride(designerPrefab);
        }

        private static bool HasAnyOverride(GameObject designerPrefab)
        {
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

        /// <summary>
        /// Writes everything the applier is able to write back into the XML.
        /// </summary>
        public static void ApplyDesignerModifications(string prefabXmlPath)
        {
            var set = DesignerChangeCollector.Collect(prefabXmlPath, logErrors: true);
            if (set == null)
            {
                return;
            }

            foreach (var change in set.Changes)
            {
                change.Selected = change.IsApplicable;
            }

            ApplyDesignerModifications(set);
        }

        /// <summary>
        /// Writes the changes marked as selected. What is left out stays an override of the designer
        /// file, so it survives the run and shows up again the next time the changes are collected.
        /// </summary>
        public static void ApplyDesignerModifications(DesignerChangeSet set)
        {
            IsBusy = true;
            try
            {
                ApplyDesignerModificationsInternal(set);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static void ApplyDesignerModificationsInternal(DesignerChangeSet set)
        {
            var applied = set.Changes.Where(c => c.Selected && c.IsApplicable).ToList();

            ApplyValueChanges(applied);
            ApplyAddedComponents(applied);
            ApplyAddedGameObjects(applied, set.Context);
            ApplyRemovedElements(applied);

            // Runs last so that objects added and removed above are already part of the XML
            ApplyChildOrder(set, applied);

            set.Context.CollectBindings(set.ConvertContext);

            if (PrefabXmlUtils.SaveXmlIfChanged(set.Document, set.PrefabXmlPath, set.DocumentText))
            {
                AssetDatabase.ImportAsset(set.PrefabXmlPath, ImportAssetOptions.ForceUpdate);

                // The prefab now matches the XML, so a layout pass on it tells which of the values
                // just written are driven and do not belong in the file
                DrivenPropertyCleaner.CleanFile(set.PrefabXmlPath);

                ApplyNewBindings(set);
            }

            // The applied changes now live in the XML, so the base prefab contains them too.
            // Overrides left on the designer file would duplicate every added object and would
            // be written to the XML again on the next apply.
            ResetDesignerOverrides(set, applied);
        }

        private static void ApplyNewBindings(DesignerChangeSet set)
        {
            if (set.Context.NewBindings.Count == 0)
            {
                return;
            }

            var importer = (PrefabXmlImporter) AssetImporter.GetAtPath(set.PrefabXmlPath);
            var result = PrefabXmlImporter.GetResult(set.PrefabXmlPath);

            if (importer == null || result == null)
            {
                return;
            }

            foreach (var kvp in set.Context.NewBindings)
            {
                var bindingName = kvp.Key;
                var asset = kvp.Value;

                if (result.discoveredBindings.TryGetValue(bindingName, out var expectedType))
                {
                    var identifier = new AssetImporter.SourceAssetIdentifier(expectedType, bindingName);
                    importer.AddRemap(identifier, asset);
                }
            }

            AssetDatabase.WriteImportSettingsIfDirty(set.PrefabXmlPath);
            AssetDatabase.ImportAsset(set.PrefabXmlPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Clears the overrides that were written to the XML, so the designer file becomes a variant
        /// of the freshly reimported base prefab again. What was left out of the run is kept.
        /// </summary>
        private static void ResetDesignerOverrides(DesignerChangeSet set, List<DesignerChange> applied)
        {
            // The designer file holds nothing worth clearing, and rewriting it would only touch its
            // timestamp
            if (set.Changes.Count == 0)
            {
                return;
            }

            // Nothing was held back, so the whole file can go at once — the same single call the
            // applier always made
            var revertAll = set.Changes.All(c => c.Selected);

            EditDesignerFile(set.DesignerPath, root => RevertRoot(root, applied, revertAll));
        }

        /// <summary>
        /// Drops every override of the designer file. A file that was just created holds the ones
        /// Unity records while instantiating, and they describe nothing the user did.
        /// </summary>
        private static void ResetDesignerOverrides(string designerPath)
        {
            EditDesignerFile(designerPath,
                root => RevertRoot(root, new List<DesignerChange>(), revertAll: true));
        }

        /// <summary>
        /// Runs an edit on the designer file and saves it when the edit changed anything.
        /// </summary>
        private static void EditDesignerFile(string designerPath, Func<GameObject, bool> edit)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == designerPath)
            {
                // The stage holds its own copy of the contents. Overwriting the asset behind its
                // back would leave the stage stale and it would save the old overrides again.
                if (edit(stage.prefabContentsRoot))
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, designerPath);
                }

                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(designerPath);
            try
            {
                if (edit(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, designerPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool RevertRoot(GameObject root, List<DesignerChange> applied, bool revertAll)
        {
            if (root == null || !PrefabUtility.IsPartOfPrefabInstance(root))
            {
                return false;
            }

            if (revertAll)
            {
                PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
                return true;
            }

            // The objects of this copy are not the ones the changes were collected from, so added
            // objects are found by their path. It runs before the property overrides are dropped,
            // because dropping a rename would move the path out from under the lookup.
            var changed = RevertAddedObjects(root, applied);

            return RevertAppliedModifications(root, applied) || changed;
        }

        /// <summary>
        /// Drops the objects and components the run wrote into the XML.
        ///
        /// The copy being edited is not the one the changes were collected from, so instead of
        /// looking objects up in it, Unity is asked what it considers added there and the answer is
        /// matched against the changes by the address they both name.
        /// </summary>
        private static bool RevertAddedObjects(GameObject root, List<DesignerChange> applied)
        {
            var wanted = applied
                .Where(c => c.Kind == DesignerChangeKind.AddedGameObject ||
                            c.Kind == DesignerChangeKind.AddedComponent)
                .ToList();

            if (wanted.Count == 0)
            {
                return false;
            }

            var changed = false;
            var reverted = new HashSet<DesignerChange>();

            var addedGameObjects = PrefabUtility.GetAddedGameObjects(root);
            if (addedGameObjects != null)
            {
                foreach (var entry in addedGameObjects)
                {
                    var go = entry.instanceGameObject;
                    if (go == null)
                    {
                        continue;
                    }

                    var path = DesignerChange.GetPath(go.transform, root.transform);
                    var change = wanted.FirstOrDefault(c =>
                        c.Kind == DesignerChangeKind.AddedGameObject && c.ObjectPath == path);

                    if (change == null)
                    {
                        continue;
                    }

                    PrefabUtility.RevertAddedGameObject(go, InteractionMode.AutomatedAction);
                    reverted.Add(change);
                    changed = true;
                }
            }

            var addedComponents = PrefabUtility.GetAddedComponents(root);
            if (addedComponents != null)
            {
                foreach (var entry in addedComponents)
                {
                    var comp = entry.instanceComponent;
                    if (comp == null)
                    {
                        continue;
                    }

                    var path = DesignerChange.GetPath(comp.transform, root.transform);
                    var typeName = comp.GetType().Name;
                    var index = DesignerChange.IndexOfComponent(comp);

                    var change = wanted.FirstOrDefault(c =>
                        c.Kind == DesignerChangeKind.AddedComponent &&
                        c.ObjectPath == path && c.ComponentType == typeName && c.ComponentIndex == index);

                    if (change == null)
                    {
                        continue;
                    }

                    PrefabUtility.RevertAddedComponent(comp, InteractionMode.AutomatedAction);
                    reverted.Add(change);
                    changed = true;
                }
            }

            foreach (var change in wanted)
            {
                // Written to the XML but still an override of the designer file. Leaving it silently
                // would show up later as the object appearing twice.
                if (!reverted.Contains(change))
                {
                    Debug.LogWarning($"DesignerFile: '{change.NewValue}' on '{change.ObjectLabel}' was " +
                                     "written to the XML, but it could not be cleared from the designer " +
                                     "file. Revert it by hand to avoid a duplicate.");
                }
            }

            return changed;
        }

        /// <summary>
        /// Drops the modifications that were written and keeps the rest. Removals are not in the
        /// list: once the base prefab loses the object a removal points at, Unity drops the override
        /// on its own.
        /// </summary>
        private static bool RevertAppliedModifications(GameObject root, List<DesignerChange> applied)
        {
            var current = PrefabUtility.GetPropertyModifications(root);
            if (current == null || current.Length == 0)
            {
                return false;
            }

            var written = new HashSet<string>();
            foreach (var change in applied)
            {
                foreach (var mod in change.Sources)
                {
                    if (mod.target != null)
                    {
                        written.Add(ModificationKey(mod));
                    }
                }
            }

            var kept = current
                .Where(m => m.target == null || !written.Contains(ModificationKey(m)))
                .ToArray();

            if (kept.Length == current.Length)
            {
                return false;
            }

            PrefabUtility.SetPropertyModifications(root, kept);
            return true;
        }

        private static string ModificationKey(PropertyModification mod)
        {
            return mod.target.GetInstanceID() + "|" + mod.propertyPath;
        }

        internal static void BuildParallelMapping(Transform baseTf, Transform variantTf, XElement goElement,
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
        /// Rewrites the order of the child elements in the XML to match the designer file. Objects
        /// added by this run are already in the document, so they are placed here as well — only a
        /// parent whose reorder the user left out is passed over.
        /// </summary>
        private static void ApplyChildOrder(DesignerChangeSet set, List<DesignerChange> applied)
        {
            var skipped = new HashSet<XElement>(set.Changes
                .Where(c => c.Kind == DesignerChangeKind.ChildOrder && !applied.Contains(c))
                .Select(c => c.TargetElement)
                .Where(el => el != null));

            ApplyChildOrder(set.DesignerPrefab.transform, set.Context, skipped);
        }

        private static void ApplyChildOrder(Transform variantTf, DesignerContext ctx, HashSet<XElement> skipped)
        {
            if (ctx.VariantToXml.TryGetValue(variantTf.GetInstanceID(), out var goElement) &&
                !skipped.Contains(goElement))
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
                ApplyChildOrder(variantTf.GetChild(i), ctx, skipped);
            }
        }

        /// <summary>
        /// True when the children of the element sit in a different order than the designer file
        /// describes and moving them is safe. Answers the same question <see cref="ReorderXmlChildren"/>
        /// answers before it starts moving anything.
        /// </summary>
        internal static bool WouldReorder(XElement parent, List<XElement> desired)
        {
            var current = parent.Elements("GameObject").ToList();

            // Only reorder when the designer file accounts for every child element. Anything else
            // means the mapping is incomplete and moving elements around would drop them.
            if (current.Count == 0 || current.Count != desired.Count)
            {
                return false;
            }

            var known = new HashSet<XElement>();
            var changed = false;

            for (var i = 0; i < desired.Count; i++)
            {
                if (desired[i].Parent != parent || !known.Add(desired[i]))
                {
                    return false;
                }

                changed |= current[i] != desired[i];
            }

            return changed;
        }

        private static void ReorderXmlChildren(XElement parent, List<XElement> desired)
        {
            if (!WouldReorder(parent, desired))
            {
                return;
            }

            var current = parent.Elements("GameObject").ToList();

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

        /// <summary>
        /// Writes the value changes: an attribute of a component, a rebuilt Field of an array, and
        /// the name and the active state of an object. What to write was worked out while the
        /// changes were collected, so nothing is decided here.
        /// </summary>
        private static void ApplyValueChanges(List<DesignerChange> applied)
        {
            foreach (var change in applied)
            {
                var element = change.TargetElement;
                if (element == null)
                {
                    continue;
                }

                switch (change.Kind)
                {
                    case DesignerChangeKind.GameObjectName:
                        element.SetAttributeValue("name", change.NewValue);
                        break;

                    case DesignerChangeKind.GameObjectActive:
                        element.SetAttributeValue("active", change.NewValue);
                        break;

                    case DesignerChangeKind.Property:
                        // No text for a leaf is an empty reference, and the format writes that by
                        // leaving the attribute out
                        if (change.NewValue != null)
                        {
                            element.SetAttributeValue(change.PropertyPath, change.NewValue);
                        }
                        else
                        {
                            element.Attribute(change.PropertyPath)?.Remove();
                        }

                        break;

                    case DesignerChangeKind.ArrayField:
                        ApplyArrayField(change, element);
                        break;
                }
            }
        }

        /// <summary>
        /// An element of an array has no name of its own in the XML, so the array is written as a
        /// whole, the way the converter writes it.
        /// </summary>
        private static void ApplyArrayField(DesignerChange change, XElement element)
        {
            var existing = element.Elements("Field")
                .FirstOrDefault(el => el.Attribute("name")?.Value == change.PropertyPath);

            // The array is empty now, and an empty Field has no representation
            if (change.PayloadElement == null)
            {
                existing?.Remove();
                return;
            }

            if (existing != null)
            {
                PrefabXmlUtils.Replace(existing, change.PayloadElement);
            }
            else
            {
                PrefabXmlUtils.AddChild(element, change.PayloadElement);
            }
        }

        /// <summary>
        /// The array a modification points into, or null when it points at a plain property.
        /// "m_Options.m_Options.Array.data[0].m_Text" belongs to "m_Options.m_Options". Of nested
        /// arrays the outermost one is taken — writing it covers everything below it.
        /// </summary>
        internal static string GetArrayPath(string propertyPath)
        {
            var index = propertyPath.IndexOf(".Array.", StringComparison.Ordinal);
            return index < 0 ? null : propertyPath.Substring(0, index);
        }

        /// <summary>
        /// The attribute of the XML a modification belongs to, or null when the format has no
        /// attribute for it. The x of a Vector2 belongs to the attribute of the whole vector, a
        /// field of a struct the format does not write as one value belongs to a dot-path of its own.
        /// </summary>
        internal static string ResolveXmlAttributeName(string propertyPath, XElement xmlElement, SerializedObject so)
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

        /// <summary>
        /// A path like "managedReferences[1234].value" names no attribute, and XName throws on it.
        /// </summary>
        internal static bool IsXmlName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            try
            {
                XmlConvert.VerifyName(name);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        /// <summary>
        /// Inserts an added component behind the components the object already has, in front of its
        /// child objects.
        /// </summary>
        private static void ApplyAddedComponents(List<DesignerChange> applied)
        {
            foreach (var change in applied)
            {
                if (change.Kind != DesignerChangeKind.AddedComponent ||
                    change.TargetElement == null || change.PayloadElement == null)
                {
                    continue;
                }

                var lastComp = change.TargetElement.Elements().LastOrDefault(PrefabXmlUtils.IsComponentElement);
                if (lastComp != null)
                {
                    PrefabXmlUtils.AddAfter(lastComp, change.PayloadElement);
                }
                else
                {
                    PrefabXmlUtils.AddFirstChild(change.TargetElement, change.PayloadElement);
                }
            }
        }

        private static void ApplyAddedGameObjects(List<DesignerChange> applied, DesignerContext ctx)
        {
            foreach (var change in applied)
            {
                if (change.Kind != DesignerChangeKind.AddedGameObject ||
                    change.TargetElement == null || change.PayloadElement == null)
                {
                    continue;
                }

                PrefabXmlUtils.AddChild(change.TargetElement, change.PayloadElement);

                // The element is appended at the end, the reorder pass moves it
                // to the position it has in the designer file
                if (change.VariantTransform != null)
                {
                    ctx.VariantToXml[change.VariantTransform.GetInstanceID()] = change.PayloadElement;
                }
            }
        }

        private static void ApplyRemovedElements(List<DesignerChange> applied)
        {
            foreach (var change in applied)
            {
                if (change.Kind != DesignerChangeKind.RemovedComponent &&
                    change.Kind != DesignerChangeKind.RemovedGameObject)
                {
                    continue;
                }

                change.TargetElement?.Remove();
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