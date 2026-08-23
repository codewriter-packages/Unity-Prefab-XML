using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// A component the mapping between the prefabs and the XML does not cover. Nothing about it can
    /// be written, and saying so beats writing to the wrong element.
    /// </summary>
    public sealed class UnmappedComponentHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = request.PropertyPath;
            return request.Element == null || request.VariantObject == null;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.Problem = request.Element == null
                ? DesignerChangeProblems.NoXmlComponent
                : DesignerChangeProblems.NoVariant;
            return change;
        }
    }
}