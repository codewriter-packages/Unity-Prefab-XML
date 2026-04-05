using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityPrefabXML
{
    public class PrefabXmlBuildContext
    {
        public AssetImportContext Ctx { get; }
        public Dictionary<string, GameObject> IdRegistry { get; } = new Dictionary<string, GameObject>();
        public Dictionary<XElement, GameObject> ElementToGameObject { get; } = new Dictionary<XElement, GameObject>();
        public List<System.Action> DeferredActions { get; } = new List<System.Action>();

        private readonly List<ImportDiagnostic> _diagnostics;

        public PrefabXmlBuildContext(AssetImportContext ctx, List<ImportDiagnostic> diagnostics)
        {
            Ctx = ctx;
            _diagnostics = diagnostics;
        }

        public void LogError(string message, int line = -1)
        {
            _diagnostics.Add(new ImportDiagnostic
            {
                severity = ImportDiagnostic.Severity.Error,
                message = message,
                line = line
            });
            Ctx.LogImportError(message);
        }

        public void LogWarning(string message, int line = -1)
        {
            _diagnostics.Add(new ImportDiagnostic
            {
                severity = ImportDiagnostic.Severity.Warning,
                message = message,
                line = line
            });
            Ctx.LogImportWarning(message);
        }

        public void LogError(string message, XElement element)
        {
            var lineInfo = (IXmlLineInfo)element;
            LogError(message, lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1);
        }

        public void LogWarning(string message, XElement element)
        {
            var lineInfo = (IXmlLineInfo)element;
            LogWarning(message, lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1);
        }

        public void Execute(string assetPath)
        {
            // Parse
            XDocument doc = ParseXml(assetPath);
            if (doc == null) return;

            XElement unityPrefab = doc.Root;
            if (!ValidateRoot(unityPrefab)) return;

            XElement rootElement = GetSingleRootGameObject(unityPrefab);
            if (rootElement == null) return;

            // Pass 1: Build hierarchy
            GameObject rootGo = GameObjectBuilder.Build(rootElement, parent: null, this);

            // Pass 2: Components
            ComponentBuilder.ProcessAll(rootElement, rootGo, this);

            // Pass 3: Resolve deferred references
            foreach (var action in DeferredActions) action();

            // Register
            Ctx.AddObjectToAsset("root", rootGo);
            Ctx.SetMainObject(rootGo);
        }

        public void RegisterSubObject(GameObject go)
        {
            Ctx.AddObjectToAsset(GetStableId(go), go);
        }

        private string GetStableId(GameObject go)
        {
            var parts = new List<string>();
            var current = go.transform;
            while (current != null)
            {
                int siblingIndex = current.GetSiblingIndex();
                parts.Add($"{current.name}[{siblingIndex}]");
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private XDocument ParseXml(string path)
        {
            try
            {
                string xml = System.IO.File.ReadAllText(path);
                return XDocument.Parse(xml, LoadOptions.SetLineInfo);
            }
            catch (System.Xml.XmlException ex)
            {
                LogError($"XML parse error at line {ex.LineNumber}: {ex.Message}", ex.LineNumber);
                CreateErrorPlaceholder();
                return null;
            }
        }

        private bool ValidateRoot(XElement root)
        {
            if (root == null || root.Name.LocalName != "UnityPrefab")
            {
                LogError("Root element must be <UnityPrefab>.", line: 1);
                CreateErrorPlaceholder();
                return false;
            }
            return true;
        }

        private XElement GetSingleRootGameObject(XElement unityPrefab)
        {
            XElement rootGo = null;
            foreach (var child in unityPrefab.Elements())
            {
                if (child.Name.LocalName == "GameObject")
                {
                    if (rootGo != null)
                    {
                        LogError("<UnityPrefab> must contain exactly one root <GameObject>.", child);
                        CreateErrorPlaceholder();
                        return null;
                    }
                    rootGo = child;
                }
                else
                {
                    LogWarning(
                        $"Unexpected element <{child.Name.LocalName}> under <UnityPrefab>. Ignored.", child);
                }
            }

            if (rootGo == null)
            {
                LogError("<UnityPrefab> must contain a <GameObject> child.");
                CreateErrorPlaceholder();
                return null;
            }

            return rootGo;
        }

        private void CreateErrorPlaceholder()
        {
            var go = new GameObject("IMPORT_ERROR");
            Ctx.AddObjectToAsset("root", go);
            Ctx.SetMainObject(go);
        }
    }
}