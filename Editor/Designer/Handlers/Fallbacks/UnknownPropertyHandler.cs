using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The last handler of the registry: it claims whatever nothing else recognized, so a property
    /// the format has no place for is reported instead of quietly dropped.
    /// </summary>
    public sealed class UnknownPropertyHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = request.PropertyPath;
            return true;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.Problem = DesignerFileManager.IsXmlName(key)
                ? DesignerChangeProblems.NoAttribute
                : DesignerChangeProblems.NotAnAttribute;
            return change;
        }
    }
}