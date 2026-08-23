using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The list of changes the designer file holds, drawn as a table the user picks from. The
    /// changes are collected at most once every <see cref="RefreshInterval"/>, and only while the
    /// designer file actually holds overrides.
    /// </summary>
    internal sealed class DesignerChangesView
    {
        private const double RefreshInterval = 1.0;

        private const float ToggleWidth = 16;
        private const float ObjectWidth = 130;
        private const float ComponentWidth = 120;
        private const float PropertyWidth = 150;
        private const float MaxTableHeight = 260;

        /// <summary>
        /// What the user unchecked, by change key. Kept instead of the changes themselves, so the
        /// choice survives a refresh of the list.
        /// </summary>
        private readonly HashSet<string> _deselected = new HashSet<string>();

        private DesignerChangeSet _set;
        private double _nextRefresh;
        private bool _expanded;
        private bool _showRejected;
        private Vector2 _scroll;

        public void Invalidate()
        {
            _set = null;
            _nextRefresh = 0;
        }

        /// <summary>
        /// Writes the changes the user left checked. Collected again first, so what is written is
        /// what the designer file holds right now and not what the table was drawn from.
        /// </summary>
        public void ApplySelected(string assetPath)
        {
            var set = DesignerChangeCollector.Collect(assetPath, logErrors: true);
            if (set == null)
            {
                return;
            }

            foreach (var change in set.Changes)
            {
                change.Selected = change.IsApplicable && !_deselected.Contains(change.Key);
            }

            DesignerFileManager.ApplyDesignerModifications(set);

            _deselected.Clear();
            Invalidate();
        }

        public void Draw(string assetPath)
        {
            Refresh(assetPath);

            if (_set == null || _set.Changes.Count == 0)
            {
                return;
            }

            var actionable = _set.Actionable.ToList();
            var rejected = _set.Rejected.ToList();

            _expanded = EditorGUILayout.Foldout(_expanded, $"Modifications ({actionable.Count})", true);

            if (_expanded)
            {
                DrawTable(actionable, rejected);
            }

            if (actionable.Count > 0 && actionable.All(c => _deselected.Contains(c.Key)))
            {
                EditorGUILayout.HelpBox(
                    "Every change is unchecked, applying would write nothing.", MessageType.Info);
            }
            else if (actionable.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "The designer file has modifications that are not applied to the XML yet.",
                    MessageType.Warning);
            }
        }

        private void DrawTable(List<DesignerChange> actionable, List<DesignerChange> rejected)
        {
            if (actionable.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing to apply.", EditorStyles.miniLabel);
            }
            else
            {
                DrawHeader();

                var scroll = actionable.Count * EditorGUIUtility.singleLineHeight > MaxTableHeight;
                if (scroll)
                {
                    _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(MaxTableHeight));
                }

                foreach (var change in actionable)
                {
                    DrawRow(change);
                }

                if (scroll)
                {
                    EditorGUILayout.EndScrollView();
                }

                DrawSelectionButtons(actionable);
            }

            if (rejected.Count == 0)
            {
                return;
            }

            _showRejected = EditorGUILayout.Foldout(_showRejected, $"Not written ({rejected.Count})", true);
            if (!_showRejected)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                foreach (var change in rejected)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(ToggleWidth + 4);
                    EditorGUILayout.LabelField(Truncate(change.ObjectLabel), GUILayout.Width(ObjectWidth));
                    EditorGUILayout.LabelField(Truncate(change.ComponentType), GUILayout.Width(ComponentWidth));
                    EditorGUILayout.LabelField(Truncate(change.Label), GUILayout.Width(PropertyWidth));
                    EditorGUILayout.LabelField(change.Problem, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private static void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(ToggleWidth + 4);
            EditorGUILayout.LabelField("Object", EditorStyles.miniBoldLabel, GUILayout.Width(ObjectWidth));
            EditorGUILayout.LabelField("Component", EditorStyles.miniBoldLabel, GUILayout.Width(ComponentWidth));
            EditorGUILayout.LabelField("Property", EditorStyles.miniBoldLabel, GUILayout.Width(PropertyWidth));
            EditorGUILayout.LabelField("New value", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(DesignerChange change)
        {
            EditorGUILayout.BeginHorizontal();

            var selected = !_deselected.Contains(change.Key);
            var toggled = EditorGUILayout.Toggle(selected, GUILayout.Width(ToggleWidth));
            if (toggled != selected)
            {
                if (toggled)
                {
                    _deselected.Remove(change.Key);
                }
                else
                {
                    _deselected.Add(change.Key);
                }
            }

            GUILayout.Space(4);

            EditorGUILayout.LabelField(new GUIContent(Truncate(change.ObjectLabel), change.ObjectLabel),
                GUILayout.Width(ObjectWidth));
            EditorGUILayout.LabelField(new GUIContent(Truncate(change.ComponentType), change.ComponentType ?? ""),
                GUILayout.Width(ComponentWidth));
            EditorGUILayout.LabelField(new GUIContent(Truncate(change.Label), change.PropertyPath),
                GUILayout.Width(PropertyWidth));

            var value = change.NewValue ?? "<not set>";
            var tooltip = change.OldValue == null
                ? value
                : $"{change.OldValue}  →  {value}";
            EditorGUILayout.LabelField(new GUIContent(Truncate(value, 60), tooltip));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionButtons(List<DesignerChange> actionable)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
            {
                foreach (var change in actionable)
                {
                    _deselected.Remove(change.Key);
                }
            }

            if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(46)))
            {
                foreach (var change in actionable)
                {
                    _deselected.Add(change.Key);
                }
            }

            if (GUILayout.Button("Refresh", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            {
                Invalidate();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Refresh(string assetPath)
        {
            if (EditorApplication.timeSinceStartup < _nextRefresh)
            {
                return;
            }

            _nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            _set = DesignerFileManager.HasAnyOverride(assetPath)
                ? DesignerChangeCollector.Collect(assetPath)
                : null;
        }

        private static string Truncate(string value, int max = 24)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }
    }
}