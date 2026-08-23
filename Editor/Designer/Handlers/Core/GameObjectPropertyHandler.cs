using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Anything else changed on the GameObject itself — the layer, the tag, the static flags. The
    /// format describes an object by its name and its active state alone.
    /// </summary>
    public sealed class GameObjectPropertyHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = request.PropertyPath;
            return request.IsGameObject;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.Problem = DesignerChangeProblems.GameObjectProperty;
            return change;
        }
    }
}