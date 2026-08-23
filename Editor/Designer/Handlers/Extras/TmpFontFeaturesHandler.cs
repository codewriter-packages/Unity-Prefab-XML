using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// TextMeshPro keeps its font features in a list it derives from the older m_enableKerning
    /// field, see <see cref="TmpFontFeatures"/>. While the list holds nothing but what that field
    /// derives, the file keeps the single switch and the list stays out of it — the handler claims
    /// the change so the general array handler never sees it.
    ///
    /// Once the file spells the list out, it is the file that decides and this handler steps aside.
    /// </summary>
    public sealed class TmpFontFeaturesHandler : IDesignerChangeClaimer
    {
        public bool TryClaim(DesignerChangeRequest request, out string key)
        {
            key = DesignerFileManager.GetArrayPath(request.PropertyPath);
            if (key == null || request.VariantObject == null || request.FindField(key) != null)
            {
                return false;
            }

            var prop = request.VariantObject.FindProperty(key);
            return prop != null && TmpFontFeatures.IsDerivedFromKerningField(prop);
        }

        public DesignerChange Build(DesignerChangeRequest request, string key,
            List<PropertyModification> mods)
        {
            var change = request.NewChange(DesignerChangeKind.ArrayField, key);
            change.Problem = DesignerChangeProblems.Derived;
            return change;
        }
    }
}