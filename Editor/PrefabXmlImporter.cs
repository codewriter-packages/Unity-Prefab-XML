using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML
{
    [Serializable]
    public class ImportDiagnostic
    {
        public enum Severity { Error, Warning }

        public Severity severity;
        public string message;
        public int line;
    }

    public class ImportResult
    {
        public List<ImportDiagnostic> diagnostics = new List<ImportDiagnostic>();
        public Dictionary<string, Type> discoveredBindings = new Dictionary<string, Type>();
    }

    [ScriptedImporter(1, "prefabxml")]
    public class PrefabXmlImporter : ScriptedImporter
    {
        private static readonly Dictionary<string, ImportResult> ResultCache =
            new Dictionary<string, ImportResult>();

        public static ImportResult GetResult(string assetPath)
        {
            ResultCache.TryGetValue(assetPath, out var result);
            return result;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var result = new ImportResult();

            var remap = GetExternalObjectMap();
            var bindings = new Dictionary<string, Object>();
            foreach (var kvp in remap)
            {
                if (kvp.Value != null)
                    bindings[kvp.Key.name] = kvp.Value;
            }

            var buildContext = new PrefabXmlBuildContext(ctx, result.diagnostics, bindings);
            buildContext.Execute(ctx.assetPath);

            result.discoveredBindings = buildContext.DiscoveredBindings;
            ResultCache[ctx.assetPath] = result;
        }
    }
}