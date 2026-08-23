using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The general handler of values: everything the format writes as one attribute. That covers the
    /// types it has a short form for — vectors, colors, enums, a RectOffset — where the x and the y
    /// of a vector fold into the single attribute of the whole value, and the dot-paths of a struct
    /// it has no short form for, where each field is an attribute of its own.
    /// </summary>
    public sealed class AttributeValueHandler : IDesignerChangeClaimer, IDesignerChangeWriter
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = null;

            if (!DesignerFileManager.IsXmlName(request.PropertyPath))
            {
                return false;
            }

            key = DesignerFileManager.ResolveXmlAttributeName(
                request.PropertyPath, request.Element, request.VariantObject);

            return key != null;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.OldValue = request.Element.Attribute(key)?.Value;

            var prop = request.VariantObject.FindProperty(key);
            if (prop == null)
            {
                change.Problem = DesignerChangeProblems.NoAttribute;
                return change;
            }

            change.NewValue = PrefabXmlSerializer.SerializeValue(prop, request.Set.ConvertContext);

            // A leaf with no text is an empty reference, and clearing the attribute is how the
            // format writes it. Anything else means the type has no representation at all.
            if (change.NewValue == null && !PrefabXmlSerializer.IsLeafProperty(prop))
            {
                change.Problem = DesignerChangeProblems.UnsupportedType;
                return change;
            }

            change.Redundant = change.NewValue == change.OldValue;
            return change;
        }

        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            if (change.NewValue != null)
            {
                change.TargetElement.SetAttributeValue(change.PropertyPath, change.NewValue);
            }
            else
            {
                change.TargetElement.Attribute(change.PropertyPath)?.Remove();
            }
        }
    }
}