using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// A layout component overwrites part of the RectTransform of every object it controls, so
    /// putting one of those values into the file for the first time writes a number nothing will
    /// ever read back — the layout computes it again the moment the object comes to life. The
    /// handler claims the change instead, so the user reads why the drag they made in the designer
    /// went nowhere.
    ///
    /// An attribute the file already names is a different matter: someone wrote it on purpose, the
    /// file keeps saying it, and a change to it is applied like any other. Nothing here removes it.
    /// </summary>
    public sealed class DrivenPropertyHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = null;

            if (!(request.VariantObject.targetObject is RectTransform rect) ||
                !DesignerFileManager.IsXmlName(request.PropertyPath))
            {
                return false;
            }

            var name = DesignerFileManager.ResolveXmlAttributeName(
                request.PropertyPath, request.Element, request.VariantObject);

            // Asked before the layout is run: most changes name an attribute no layout ever takes
            // over, and those must not pay for measuring one
            if (name == null || !DrivenProperties.CanBeDriven(name))
            {
                return false;
            }

            if (request.Element.Attribute(name) != null)
            {
                return false;
            }

            if (!DrivenProperties.IsDriven(name, request.Set.GetDrivenProperties(rect)))
            {
                return false;
            }

            key = name;
            return true;
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.Property, key);
            change.Problem = DesignerChangeProblems.Driven;
            return change;
        }
    }
}