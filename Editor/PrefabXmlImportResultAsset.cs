using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML
{
    /// <summary>
    /// Import result stored as a hidden sub-asset of the import artifact.
    /// <see cref="PrefabXmlImporter.OnImportAsset"/> only runs when Unity rebuilds the artifact,
    /// so the in-memory cache is empty after an editor restart or a domain reload. Keeping a copy
    /// inside the artifact lets the inspector show bindings and diagnostics without a reimport.
    /// </summary>
    public class PrefabXmlImportResultAsset : ScriptableObject
    {
        [Serializable]
        public class BindingEntry
        {
            public string name;
            public string assemblyQualifiedName;
            public string fullName;
        }

        public List<BindingEntry> bindings = new List<BindingEntry>();
        public List<ImportDiagnostic> diagnostics = new List<ImportDiagnostic>();

        public static PrefabXmlImportResultAsset Create(ImportResult result)
        {
            var asset = CreateInstance<PrefabXmlImportResultAsset>();
            asset.name = "ImportResult";
            asset.hideFlags = HideFlags.HideInHierarchy;
            asset.diagnostics = result.diagnostics;

            foreach (var kvp in result.discoveredBindings)
            {
                asset.bindings.Add(new BindingEntry
                {
                    name = kvp.Key,
                    assemblyQualifiedName = kvp.Value.AssemblyQualifiedName,
                    fullName = kvp.Value.FullName,
                });
            }

            return asset;
        }

        public ImportResult ToImportResult()
        {
            var result = new ImportResult
            {
                diagnostics = diagnostics ?? new List<ImportDiagnostic>(),
            };

            foreach (var entry in bindings)
            {
                var type = ResolveType(entry);
                if (type != null)
                    result.discoveredBindings[entry.name] = type;
            }

            return result;
        }

        private static Type ResolveType(BindingEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.assemblyQualifiedName))
            {
                var type = Type.GetType(entry.assemblyQualifiedName);
                if (type != null) return type;
            }

            if (string.IsNullOrEmpty(entry.fullName)) return null;
            if (entry.fullName == typeof(Object).FullName) return typeof(Object);

            // The assembly may have been renamed or the type moved since the import was cached
            foreach (var type in TypeCache.GetTypesDerivedFrom<Object>())
            {
                if (type.FullName == entry.fullName) return type;
            }

            return null;
        }
    }
}
