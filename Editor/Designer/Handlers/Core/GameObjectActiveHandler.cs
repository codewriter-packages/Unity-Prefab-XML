using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Whether an object starts out active, written as the active attribute.
    /// </summary>
    public sealed class GameObjectActiveHandler : IDesignerChangeClaimer, IDesignerChangeWriter
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = "m_IsActive";
            return request.IsGameObject && request.PropertyPath == "m_IsActive";
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.GameObjectActive, key);
            change.Label = "active";
            change.NewValue = mods[0].value == "1" ? "true" : "false";
            change.OldValue = request.Element?.Attribute("active")?.Value ?? "true";
            change.Problem = request.Element == null ? DesignerChangeProblems.NoXmlObject : null;
            change.Redundant = change.Problem == null && change.NewValue == change.OldValue;
            return change;
        }

        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            change.TargetElement.SetAttributeValue("active", change.NewValue);
        }
    }
}