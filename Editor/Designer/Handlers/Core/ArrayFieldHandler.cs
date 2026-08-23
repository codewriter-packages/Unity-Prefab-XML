using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// An element of an array has no name of its own in the XML, so a change anywhere inside one is
    /// a change of the whole array: the Field element is rebuilt the way the converter writes it.
    /// </summary>
    public sealed class ArrayFieldHandler : IDesignerChangeClaimer, IDesignerChangeWriter
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = DesignerFileManager.GetArrayPath(request.PropertyPath);
            return key != null;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.ArrayField, key);

            var existing = request.FindField(key);
            change.OldValue = existing == null ? null : Summary(existing);

            var prop = request.VariantObject.FindProperty(key);
            if (prop == null || !prop.isArray || prop.propertyType == SerializedPropertyType.String)
            {
                change.Problem = DesignerChangeProblems.NoAttribute;
                return change;
            }

            var refs = new List<XElement>();
            change.PayloadElement = PrefabXmlSerializer.SerializeField(prop, request.Set.ConvertContext, refs);

            if (refs.Count > 0)
            {
                // Every managed reference lives in a Ref element of its own. Writing them would mean
                // renumbering the ids the file already uses and dropping the ones the old items left
                // behind, so the array is left as it is.
                change.Problem = DesignerChangeProblems.ManagedReferences;
                change.PayloadElement = null;
                return change;
            }

            change.NewValue = change.PayloadElement == null ? null : Summary(change.PayloadElement);

            change.Redundant = change.PayloadElement == null
                ? existing == null
                : existing != null && XNode.DeepEquals(WithoutWhitespace(existing), change.PayloadElement);

            return change;
        }

        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            var existing = change.TargetElement.Elements("Field")
                .FirstOrDefault(el => el.Attribute("name")?.Value == change.PropertyPath);

            // The array is empty now, and an empty Field has no representation
            if (change.PayloadElement == null)
            {
                existing?.Remove();
                return;
            }

            if (existing != null)
            {
                PrefabXmlUtils.Replace(existing, change.PayloadElement);
            }
            else
            {
                PrefabXmlUtils.AddChild(change.TargetElement, change.PayloadElement);
            }
        }

        private static string Summary(XElement field)
        {
            return $"{field.Elements("Item").Count()} items";
        }

        /// <summary>
        /// A copy without the whitespace the file is formatted with, so it can be compared to a
        /// freshly built element.
        /// </summary>
        private static XElement WithoutWhitespace(XElement element)
        {
            var copy = new XElement(element);
            foreach (var text in copy.DescendantNodes().OfType<XText>().ToList())
            {
                if (string.IsNullOrWhiteSpace(text.Value))
                {
                    text.Remove();
                }
            }

            return copy;
        }
    }
}