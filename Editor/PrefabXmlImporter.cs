using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;

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

    [ScriptedImporter(1, "prefabxml")]
    public class PrefabXmlImporter : ScriptedImporter
    {
        [SerializeField, HideInInspector]
        public List<ImportDiagnostic> diagnostics = new List<ImportDiagnostic>();

        public override void OnImportAsset(AssetImportContext ctx)
        {
            diagnostics.Clear();
            var buildContext = new PrefabXmlBuildContext(ctx, diagnostics);
            buildContext.Execute(ctx.assetPath);
        }
    }
}