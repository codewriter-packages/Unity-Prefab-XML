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
        private const float ObjectWidth = 200;
        private const float ComponentWidth = 120;
        private const float PropertyWidth = 120;
        private const float ValueWidth = 100;
        private const float RowButtonWidth = 52;
        private const float MaxTableHeight = 260;

        /// <summary>
        /// What the user unchecked, by change key. Kept instead of the changes themselves, so the
        /// choice survives a refresh of the list.
        /// </summary>
        private readonly HashSet<string> _deselected = new HashSet<string>();

        private DesignerChangeSet _set;
        private double _nextRefresh;
        private bool _showSkipped;

        // Open from the start: these are the ones that mean something is missing from the applier
        private bool _showUnsupported = true;

        private Vector2 _scroll;
        private GUIStyle _unsupportedFoldout;

        /// <summary>What a row button asked for, run at the start of the next pass by RunPending.</summary>
        private DesignerChange _pending;

        private bool _pendingIsRevert;

        public void Invalidate()
        {
            _set = null;
            _nextRefresh = 0;
        }

        /// <summary>
        /// Whether applying would write anything: the file holds a change the applier can write and
        /// the user left it checked. Collects first, because the button is drawn above the table and
        /// would otherwise answer from the list of the frame before.
        /// </summary>
        public bool HasSelection(string assetPath)
        {
            Refresh(assetPath);
            return _set != null && _set.Actionable.Any(c => !_deselected.Contains(c.Key));
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
            RunPending(assetPath);
            Refresh(assetPath);

            if (_set == null || _set.Changes.Count == 0)
            {
                return;
            }

            var actionable = _set.Actionable.ToList();

            GUILayout.Label($"Modifications ({actionable.Count})");

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

            DrawTable(actionable);

            // Two reasons a change is left out, and only one of them is news. What the format never
            // writes is folded away; what nothing knew how to write is opened and marked.
            DrawRejected(_set.Unsupported.ToList(), ref _showUnsupported, "Unsupported modifications",
                UnsupportedFoldout);
            DrawRejected(_set.Skipped.ToList(), ref _showSkipped, "Skipped modifications", EditorStyles.foldout);
        }

        private void DrawTable(List<DesignerChange> actionable)
        {
            if (actionable.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing to apply.", EditorStyles.miniLabel);
                GUILayout.Space(8);
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            
            DrawHeader();

            var scroll = actionable.Count * EditorGUIUtility.singleLineHeight > MaxTableHeight;
            if (scroll)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(MaxTableHeight));
            }

            for (var i = 0; i < actionable.Count; i++)
            {
                DrawRow(actionable[i], i);
            }

            if (scroll)
            {
                EditorGUILayout.EndScrollView();
            }
            
            GUILayout.EndVertical();

            DrawSelectionButtons(actionable);

            GUILayout.Space(8);
        }

        /// <summary>
        /// One group of changes that are shown and never written, behind a foldout of its own. The
        /// value column gives way to the reason, because for these that is the whole story.
        /// </summary>
        private static void DrawRejected(List<DesignerChange> changes, ref bool shown, string title,
            GUIStyle titleStyle)
        {
            if (changes.Count == 0)
            {
                return;
            }

            shown = EditorGUILayout.Foldout(shown, $"{title} ({changes.Count})", true, titleStyle);
            if (!shown)
            {
                GUILayout.Space(8);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (var i = 0; i < changes.Count; i++)
                {
                    var change = changes[i];

                    DrawRowBackground(EditorGUILayout.BeginHorizontal(), i);
                    GUILayout.Space(ToggleWidth + 4);
                    DrawIdentity(change);
                    EditorGUILayout.LabelField(new GUIContent(change.Problem.Text, change.Problem.Text),
                        EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);
        }

        /// <summary>
        /// Shades every other row, so the eye carries one change across the width of the table
        /// without losing the line. A translucent wash rather than a color of its own: it sits over
        /// whatever the skin paints behind the table and reads the same in both of them.
        ///
        /// The rect comes from the layout group of the row, which is why this is drawn before the
        /// row is filled — during layout it is empty and nothing is painted, and by the time a
        /// repaint runs it is the rect the row had.
        /// </summary>
        private static void DrawRowBackground(Rect row, int index)
        {
            if (index % 2 != 0)
            {
                return;
            }

            EditorGUI.DrawRect(row, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.04f)
                : new Color(0f, 0f, 0f, 0.04f));
        }

        /// <summary>
        /// The heading of the unsupported group, in the red the editor uses for what went wrong. It
        /// is built from the current foldout style, so it keeps the arrow and the skin of the rest
        /// of the inspector and changes nothing but the color of the text.
        /// </summary>
        private GUIStyle UnsupportedFoldout
        {
            get
            {
                if (_unsupportedFoldout == null)
                {
                    var color = EditorGUIUtility.isProSkin
                        ? new Color(1f, 0.44f, 0.4f)
                        : new Color(0.66f, 0.1f, 0.06f);

                    _unsupportedFoldout = new GUIStyle(EditorStyles.foldout);
                    _unsupportedFoldout.normal.textColor = color;
                    _unsupportedFoldout.onNormal.textColor = color;
                    _unsupportedFoldout.hover.textColor = color;
                    _unsupportedFoldout.onHover.textColor = color;
                    _unsupportedFoldout.active.textColor = color;
                    _unsupportedFoldout.onActive.textColor = color;
                    _unsupportedFoldout.focused.textColor = color;
                    _unsupportedFoldout.onFocused.textColor = color;
                }

                return _unsupportedFoldout;
            }
        }

        private static void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(ToggleWidth + 4);
            EditorGUILayout.LabelField("Object", EditorStyles.miniBoldLabel, GUILayout.Width(ObjectWidth));
            EditorGUILayout.LabelField("Component", EditorStyles.miniBoldLabel, GUILayout.Width(ComponentWidth));
            EditorGUILayout.LabelField("Property", EditorStyles.miniBoldLabel, GUILayout.Width(PropertyWidth));
            EditorGUILayout.LabelField("Old value", EditorStyles.miniBoldLabel, GUILayout.Width(ValueWidth));
            EditorGUILayout.LabelField("New value", EditorStyles.miniBoldLabel, GUILayout.Width(ValueWidth));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(DesignerChange change, int index)
        {
            DrawRowBackground(EditorGUILayout.BeginHorizontal(), index);

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

            DrawIdentity(change);

            // The old value is what the file says now, so nothing there means the file says nothing
            // yet. The new one is what the applier writes, and there nothing is a value of its own:
            // the attribute goes away.
            DrawValue(change.OldValue);
            DrawValue(change.NewValue);

            GUILayout.FlexibleSpace();
            DrawRowButtons(change);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The two ways one row can be dealt with on its own: write it to the XML, or drop it off the
        /// designer file and leave the XML alone. Neither runs where it is pressed — the table is
        /// still being drawn around it — so the press is only remembered here.
        /// </summary>
        private void DrawRowButtons(DesignerChange change)
        {
            if (GUILayout.Button(new GUIContent("Apply", "Write this change to the XML"),
                    EditorStyles.miniButtonLeft, GUILayout.Width(RowButtonWidth)))
            {
                _pending = change;
                _pendingIsRevert = false;
            }

            using (new EditorGUI.DisabledScope(!change.CanRevert))
            {
                var label = change.CanRevert
                    ? "Drop this change off the designer file, leaving the XML as it is"
                    : "Putting back what was removed is not something the revert pass can do";

                if (GUILayout.Button(new GUIContent("Revert", label),
                        EditorStyles.miniButtonRight, GUILayout.Width(RowButtonWidth)))
                {
                    _pending = change;
                    _pendingIsRevert = true;
                }
            }
        }

        /// <summary>
        /// Does what a row button asked for, before anything of this pass is laid out. Not where the
        /// button is drawn: the work saves the designer file and reimports the XML, and the list the
        /// table is being drawn from is thrown away in the middle of drawing it.
        /// </summary>
        private void RunPending(string assetPath)
        {
            var change = _pending;
            if (change == null)
            {
                return;
            }

            _pending = null;

            // The list the button was drawn from may be a second old, so the change is looked up
            // again in a freshly collected one and only the key carries over
            var set = DesignerChangeCollector.Collect(assetPath, logErrors: true);
            var fresh = set?.Changes.FirstOrDefault(c => c.Key == change.Key);
            if (fresh == null)
            {
                Invalidate();
                return;
            }

            if (_pendingIsRevert)
            {
                DesignerFileManager.RevertChange(set, fresh);
            }
            else
            {
                foreach (var other in set.Changes)
                {
                    other.Selected = other == fresh && other.IsApplicable;
                }

                DesignerFileManager.ApplyDesignerModifications(set);
            }

            Invalidate();
        }

        /// <summary>
        /// The columns that name the change, drawn the same for a change that is written and one
        /// that is not, so the two tables line up.
        /// </summary>
        private static void DrawIdentity(DesignerChange change)
        {
            EditorGUILayout.LabelField(new GUIContent(change.ObjectLabel, change.ObjectLabel),
                GUILayout.Width(ObjectWidth));
            EditorGUILayout.LabelField(
                new GUIContent(change.ComponentType, change.ComponentType ?? ""),
                GUILayout.Width(ComponentWidth));
            EditorGUILayout.LabelField(
                new GUIContent(change.Label, change.PropertyPath),
                GUILayout.Width(PropertyWidth));
        }

        private static void DrawValue(string value)
        {
            var shown = value ?? "-";
            EditorGUILayout.LabelField(new GUIContent(shown, shown), GUILayout.Width(ValueWidth));
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
    }
}