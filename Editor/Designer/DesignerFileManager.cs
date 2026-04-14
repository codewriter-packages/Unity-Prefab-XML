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
        public static string GetDesignerPath(string prefabXmlPath)
        {
            var dir = Path.GetDirectoryName(prefabXmlPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(prefabXmlPath);
            return Path.Combine(dir, name + ".prefab");
        }

        public static bool DesignerExists(string prefabXmlPath)
        {
            var designerPath = GetDesignerPath(prefabXmlPath);
            var designerGuid = AssetDatabase.AssetPathToGUID(designerPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            return !string.IsNullOrEmpty(designerGuid);
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

            var instance = (GameObject) PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, designerPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            var designerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(designerPath);
            if (focusDesignerFile && designerAsset != null)
            {
                Selection.activeObject = designerAsset;
            }
        }

        public static void ApplyDesignerModifications(string prefabXmlPath)
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

            // Write XML back
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };

            using (var writer = XmlWriter.Create(prefabXmlPath, settings))
            {
                xmlDoc.Save(writer);
            }

            // Delete designer
            AssetDatabase.DeleteAsset(designerPath);

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
        }

        private static void BuildParallelMapping(Transform baseTf, Transform variantTf, XElement goElement,
            DesignerContext ctx)
        {
            ctx.GoToXml[baseTf.gameObject.GetInstanceID()] = goElement;
            ctx.GoToVariant[baseTf.gameObject.GetInstanceID()] = variantTf;

            // Match components by type name
            var xmlCompElements = goElement.Elements()
                .Where(PrefabXmlUtils.IsComponentElement)
                .ToList();

            var baseComps = baseTf.GetComponents<Component>()
                .Where(c => c != null && !PrefabXmlSerializer.SkipComponents.Contains(c.GetType().Name))
                .ToList();

            var variantComps = variantTf.GetComponents<Component>()
                .Where(c => c != null && !PrefabXmlSerializer.SkipComponents.Contains(c.GetType().Name))
                .ToList();

            // Match base components to XML elements by type name in order
            var usedXmlIndices = new HashSet<int>();
            foreach (var baseComp in baseComps)
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

            // Match base components to variant components by type in order
            var usedVariantIndices = new HashSet<int>();
            foreach (var baseComp in baseComps)
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

            // Recurse children by index
            var xmlChildren = goElement.Elements("GameObject").ToList();
            var childCount = Math.Min(baseTf.childCount, Math.Min(variantTf.childCount, xmlChildren.Count));

            for (var i = 0; i < childCount; i++)
            {
                BuildParallelMapping(baseTf.GetChild(i), variantTf.GetChild(i), xmlChildren[i], ctx);
            }
        }

        private static void ApplyPropertyModifications(PropertyModification[] modifications, DesignerContext ctx)
        {
            // Group modifications by target object
            var modsByTarget = modifications
                .Where(m => m.target != null)
                .GroupBy(m => m.target.GetInstanceID());

            foreach (var group in modsByTarget)
            {
                var target = modifications.First(m => m.target.GetInstanceID() == group.Key).target;
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