using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityPrefabXML
{
    public class PrefabXmlBuildContext
    {
        public AssetImportContext Ctx { get; }
        public Dictionary<string, GameObject> IdRegistry { get; } = new Dictionary<string, GameObject>();
        public List<System.Action> DeferredActions { get; } = new List<System.Action>();

        private int _objectIndex;

        public PrefabXmlBuildContext(AssetImportContext ctx)
        {
            Ctx = ctx;
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

            // Pass 2: Components (Step 2)

            // Pass 3: Resolve deferred references (Step 8)

            // Register
            Ctx.AddObjectToAsset("root", rootGo);
            Ctx.SetMainObject(rootGo);
        }

        public string NextObjectId(string hint)
        {
            return $"{hint}_{_objectIndex++}";
        }

        public void RegisterSubObject(GameObject go)
        {
            Ctx.AddObjectToAsset(NextObjectId(go.name), go);
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
                Ctx.LogImportError($"XML parse error at line {ex.LineNumber}: {ex.Message}");
                CreateErrorPlaceholder();
                return null;
            }
        }

        private bool ValidateRoot(XElement root)
        {
            if (root == null || root.Name.LocalName != "UnityPrefab")
            {
                Ctx.LogImportError("Root element must be <UnityPrefab>.");
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
                        Ctx.LogImportError("<UnityPrefab> must contain exactly one root <GameObject>.");
                        CreateErrorPlaceholder();
                        return null;
                    }
                    rootGo = child;
                }
                else
                {
                    Ctx.LogImportWarning(
                        $"Unexpected element <{child.Name.LocalName}> under <UnityPrefab>. Ignored.");
                }
            }

            if (rootGo == null)
            {
                Ctx.LogImportError("<UnityPrefab> must contain a <GameObject> child.");
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