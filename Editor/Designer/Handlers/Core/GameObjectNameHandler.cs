using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The name of an object, written as the name attribute of its GameObject element.
    /// </summary>
    public sealed class GameObjectNameHandler : IDesignerChangeClaimer, IDesignerChangeWriter
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = "m_Name";
            return request.IsGameObject && request.PropertyPath == "m_Name";
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.GameObjectName, key);
            change.Label = "name";
            change.NewValue = mods[0].value;
            change.OldValue = request.Element?.Attribute("name")?.Value;
            change.Problem = request.Element == null ? DesignerChangeProblems.NoXmlObject : null;
            change.Redundant = change.Problem == null && change.NewValue == change.OldValue;
            return change;
        }

        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            change.TargetElement.SetAttributeValue("name", change.NewValue);
        }
    }
}