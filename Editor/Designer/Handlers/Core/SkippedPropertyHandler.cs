using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The properties the format never writes — the ones Unity keeps on every object and the ones
    /// the file describes through its own structure instead.
    /// </summary>
    public sealed class SkippedPropertyHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = request.PropertyPath;
            return PrefabXmlSerializer.IsSkipProperty(request.PropertyPath);
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.Problem = DesignerChangeProblems.Skipped;
            return change;
        }
    }
}