using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Everything the designer file says about [SerializeReference] values, as a single change.
    ///
    /// Unity keeps managed references in a flat block of the serialized object and records every
    /// override against the id of an entry — "managedReferences[1000].duration". Those ids belong to
    /// that one object and mean nothing in the XML, which hands out its own with "ref0". Nothing
    /// bridges the two yet, so none of it is written.
    ///
    /// The point of the handler is that the whole set arrives as one row. Changing a single field of
    /// a reference comes with the id of the entry, the size of the array holding it and the id
    /// written into the array element, and those are one edit: writing part of it would leave the
    /// file describing a reference nothing points at. There is nothing to decide per row here, so the
    /// table says it once per component instead of once per modification.
    /// </summary>
    public sealed class ManagedReferenceHandler : IDesignerChangeClaimer
    {
        /// <summary>The flat block every managed reference override is recorded against.</summary>
        private const string ReferenceBlock = "managedReferences[";

        /// <summary>
        /// One key for the whole component, which is what folds every managed reference change on it
        /// into a single row. Not a property path — no property of the object is named this.
        /// </summary>
        private const string GroupKey = "managedReferences";

        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = GroupKey;

            return IsReferenceBlock(request.PropertyPath) ||
                   IsReferenceArray(request) ||
                   IsReferenceField(request);
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.ManagedReference, key);
            change.Label = $"managed references ({mods.Count})";
            change.Problem = DesignerChangeProblems.ManagedReferences;
            return change;
        }

        /// <summary>
        /// A value inside a reference, the type of one the designer file added, or the id of one
        /// written into an array element — all of it is recorded against the flat block.
        /// </summary>
        private static bool IsReferenceBlock(string propertyPath)
        {
            return propertyPath.StartsWith(ReferenceBlock, StringComparison.Ordinal);
        }

        /// <summary>
        /// An array of references. Its elements carry ids of the block rather than values, so the
        /// general array handler would rebuild it into Ref elements this run cannot number.
        /// </summary>
        private static bool IsReferenceArray(DesignerChangeRequest request)
        {
            var arrayPath = DesignerFileManager.GetArrayPath(request.PropertyPath);
            if (arrayPath == null)
            {
                return false;
            }

            var prop = request.VariantObject.FindProperty(arrayPath);
            if (prop == null || !prop.isArray || prop.propertyType == SerializedPropertyType.String)
            {
                return false;
            }

            if (prop.arraySize > 0)
            {
                return prop.GetArrayElementAtIndex(0).propertyType ==
                       SerializedPropertyType.ManagedReference;
            }

            // Emptied by the designer file, so only what the file still says tells this array from
            // any other. Dropping the Field alone would leave its Ref elements behind.
            var field = request.FindField(arrayPath);
            return field != null && field.Elements("Item").Any(IsReferenceItem);
        }

        private static bool IsReferenceItem(XElement item)
        {
            var value = item.Attribute("v")?.Value;
            return value != null && value.StartsWith("@", StringComparison.Ordinal);
        }

        /// <summary>
        /// A reference assigned to a field of its own. The modification names the field, and the
        /// value it carries is the id of the entry the object was put into.
        /// </summary>
        private static bool IsReferenceField(DesignerChangeRequest request)
        {
            var prop = request.VariantObject.FindProperty(request.PropertyPath);
            return prop != null && prop.propertyType == SerializedPropertyType.ManagedReference;
        }
    }
}