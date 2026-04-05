using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityPrefabXML
{
    [CustomEditor(typeof(PrefabXmlImporter))]
    public class PrefabXmlImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            var importer = (PrefabXmlImporter) target;
            var diagnostics = importer.diagnostics;

            if (diagnostics != null && diagnostics.Count != 0)
            {
                var errors = diagnostics.Where(d => d.severity == ImportDiagnostic.Severity.Error).ToList();
                var warnings = diagnostics.Where(d => d.severity == ImportDiagnostic.Severity.Warning).ToList();

                if (errors.Count > 0)
                {
                    EditorGUILayout.LabelField($"Errors ({errors.Count})", EditorStyles.boldLabel);
                    foreach (var diag in errors)
                        DrawDiagnostic(diag, MessageType.Error, importer);
                }

                if (warnings.Count > 0)
                {
                    if (errors.Count > 0)
                        EditorGUILayout.Space(4);

                    EditorGUILayout.LabelField($"Warnings ({warnings.Count})", EditorStyles.boldLabel);
                    foreach (var diag in warnings)
                        DrawDiagnostic(diag, MessageType.Warning, importer);
                }
            }

            ApplyRevertGUI();
        }

        private static void DrawDiagnostic(ImportDiagnostic diag, MessageType type, PrefabXmlImporter importer)
        {
            var msg = diag.line > 0
                ? $"Line {diag.line}: {diag.message}"
                : diag.message;

            var iconName = type == MessageType.Error ? "console.erroricon" : "console.warnicon";
            var icon = EditorGUIUtility.IconContent(iconName).image;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndHorizontal();
        }
    }
}