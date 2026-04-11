using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityPrefabXML.Designer;
using Object = UnityEngine.Object;

namespace UnityPrefabXML
{
    [CustomEditor(typeof(PrefabXmlImporter))]
    public class PrefabXmlImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            var importer = (PrefabXmlImporter)target;
            var result = PrefabXmlImporter.GetResult(importer.assetPath);

            if (result != null)
            {
                DrawBindings(importer, result.discoveredBindings);
                DrawDiagnostics(result.diagnostics);
            }

            DrawDesignerSection(importer.assetPath);

            ApplyRevertGUI();
        }

        private static void DrawBindings(PrefabXmlImporter importer,
            Dictionary<string, Type> discoveredBindings)
        {
            if (discoveredBindings.Count == 0)
                return;

            EditorGUILayout.LabelField("Asset Bindings", EditorStyles.boldLabel);

            var remap = importer.GetExternalObjectMap();
            var sorted = discoveredBindings.OrderBy(kvp => kvp.Key);

            foreach (var kvp in sorted)
            {
                var bindingName = kvp.Key;
                var expectedType = kvp.Value;
                var identifier = new AssetImporter.SourceAssetIdentifier(expectedType, bindingName);

                remap.TryGetValue(identifier, out var currentAsset);

                EditorGUI.BeginChangeCheck();
                var label = $"{bindingName}  ({expectedType.Name})";
                var newAsset = EditorGUILayout.ObjectField(label, currentAsset, expectedType, false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newAsset != null)
                        importer.AddRemap(identifier, newAsset);
                    else
                        importer.RemoveRemap(identifier);

                    AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                    AssetDatabase.ImportAsset(importer.assetPath);
                }
            }

            EditorGUILayout.Space(8);
        }

        private static void DrawDesignerSection(string assetPath)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Designer");

            var designerExists = DesignerFileManager.DesignerExists(assetPath);

            using (new EditorGUI.DisabledScope(designerExists))
            {
                if (GUILayout.Button("Create", EditorStyles.miniButtonLeft))
                {
                    DesignerFileManager.CreateDesignerFile(assetPath, focusDesignerFile: true);
                }
            }

            using (new EditorGUI.DisabledScope(!designerExists))
            {
                if (GUILayout.Button("Apply modifications", EditorStyles.miniButtonRight))
                {
                    DesignerFileManager.ApplyDesignerModifications(assetPath);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        private static void DrawDiagnostics(List<ImportDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return;

            var errors = diagnostics.Where(d => d.severity == ImportDiagnostic.Severity.Error).ToList();
            var warnings = diagnostics.Where(d => d.severity == ImportDiagnostic.Severity.Warning).ToList();

            if (errors.Count > 0)
            {
                EditorGUILayout.LabelField($"Errors ({errors.Count})", EditorStyles.boldLabel);
                foreach (var diag in errors)
                    DrawDiagnostic(diag, MessageType.Error);
            }

            if (warnings.Count > 0)
            {
                if (errors.Count > 0)
                    EditorGUILayout.Space(4);

                EditorGUILayout.LabelField($"Warnings ({warnings.Count})", EditorStyles.boldLabel);
                foreach (var diag in warnings)
                    DrawDiagnostic(diag, MessageType.Warning);
            }
        }

        private static void DrawDiagnostic(ImportDiagnostic diag, MessageType type)
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