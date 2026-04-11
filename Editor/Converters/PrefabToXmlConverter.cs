using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML.Converters
{
    public static class PrefabToXmlConverter
    {
        [MenuItem("Assets/PrefabXML/Convert UGUI Prefab to PrefabXML", true)]
        private static bool ValidateConvert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<RectTransform>() != null)
                    return true;
            }

            return false;
        }

        [MenuItem("Assets/PrefabXML/Convert UGUI Prefab to PrefabXML")]
        private static void Convert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                ConvertOne(path);
            }
        }

        private static void ConvertOne(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"PrefabToXml: Cannot load prefab at '{path}'.");
                return;
            }

            var doc = ConvertPrefab(go, out var bindings);
            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };

            var outputPath = Path.ChangeExtension(path, ".prefabxml");
            using (var writer = System.Xml.XmlWriter.Create(outputPath, settings))
            {
                doc.Save(writer);
            }

            // First import — discovers bindings and their expected types
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            if (bindings.Count > 0)
            {
                var importer = (PrefabXmlImporter) AssetImporter.GetAtPath(outputPath);
                var result = PrefabXmlImporter.GetResult(outputPath);

                if (importer != null && result != null)
                {
                    foreach (var kvp in bindings)
                    {
                        var bindingName = kvp.Key;
                        var asset = kvp.Value;

                        if (result.discoveredBindings.TryGetValue(bindingName, out var expectedType))
                        {
                            var identifier = new AssetImporter.SourceAssetIdentifier(expectedType, bindingName);
                            importer.AddRemap(identifier, asset);
                        }
                    }

                    AssetDatabase.WriteImportSettingsIfDirty(outputPath);
                    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
                }
            }

            Debug.Log($"PrefabToXml: Converted '{path}' → '{outputPath}'");
        }

        public static XDocument ConvertPrefab(GameObject root)
        {
            return ConvertPrefab(root, out _);
        }

        public static XDocument ConvertPrefab(GameObject root, out Dictionary<string, Object> bindings)
        {
            var ctx = new PrefabXmlSerializer.PrefabXmlSerializationContext {Root = root};
            PrefabXmlSerializer.AssignIds(root.transform, ctx);

            var rootElement = PrefabXmlSerializer.SerializeGameObject(root, ctx);
            bindings = ctx.UsedBindings;
            var unityPrefab = new XElement("UnityPrefab",
                new XAttribute("format", "Packages/com.codewriter.unity-prefab-xml/FORMAT.md"),
                new XAttribute("guide", "Packages/com.codewriter.unity-prefab-xml/GUIDE.md"),
                rootElement);
            return new XDocument(unityPrefab);
        }
    }
}