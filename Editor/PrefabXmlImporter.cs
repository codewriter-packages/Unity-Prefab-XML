using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityPrefabXML.Builder;
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

    [ScriptedImporter(3, "prefabxml")]
    public class PrefabXmlImporter : ScriptedImporter
    {
        private static readonly Dictionary<string, ImportResult> ResultCache =
            new Dictionary<string, ImportResult>();

        /// <summary>
        /// Matches the target of an <c>externalObjects</c> entry in the .meta. The trailing
        /// <c>script:</c> pointer uses its own key, so it is not picked up.
        /// </summary>
        private static readonly Regex RemapTargetGuid = new Regex(
            @"^\s*second:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})",
            RegexOptions.Compiled);

        public static ImportResult GetResult(string assetPath)
        {
            if (ResultCache.TryGetValue(assetPath, out var result))
                return result;

            // Unity reuses the cached artifact instead of calling OnImportAsset after an editor
            // restart or a domain reload, so recover the result from the artifact itself.
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is PrefabXmlImportResultAsset stored)
                {
                    result = stored.ToImportResult();
                    ResultCache[assetPath] = result;
                    return result;
                }
            }

            return null;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Declared before the remap is resolved, and from the .meta rather than from the
            // resolved objects: GetExternalObjectMap() hands back a null value for an entry whose
            // artifact this import cannot load yet, and a null value carries no path, so reading
            // the paths off the resolved objects would drop the dependency on exactly the import
            // that needs it — leaving the field null until someone reimports by hand.
            foreach (var path in ReadRemapTargetPaths(ctx.assetPath))
            {
                ctx.DependsOnArtifact(path);

                // Declaring the dependency is not enough on its own: Unity throws away one the
                // import never reads ("dependency isn't used and therefore not registered"), and
                // the read is also what brings that artifact up to date here. Without it the
                // remap below resolves against a stale artifact and hands back nulls.
                AssetDatabase.LoadMainAssetAtPath(path);
            }

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

            ctx.AddObjectToAsset("importResult", PrefabXmlImportResultAsset.Create(result));
        }

        private static IEnumerable<string> ReadRemapTargetPaths(string assetPath)
        {
            var metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
                yield break;

            foreach (var line in File.ReadLines(metaPath))
            {
                var match = RemapTargetGuid.Match(line);
                if (!match.Success) continue;

                var path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);

                // A built-in resource — the default UI sprite and friends — resolves to a path
                // outside the project such as 'resources/unity_builtin_extra'. Nothing imports
                // those, so depending on one is a dependency Unity can never satisfy: it reports
                // the dependency as invalid and reimports the file forever.
                if (!IsProjectAsset(path))
                    continue;

                yield return path;
            }
        }

        private static bool IsProjectAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && (path.StartsWith("Assets/") || path.StartsWith("Packages/"));
        }
    }
}