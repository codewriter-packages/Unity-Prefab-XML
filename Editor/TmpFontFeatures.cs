using UnityEditor;

namespace UnityPrefabXML
{
    /// <summary>
    /// TextMeshPro keeps kerning in two serialized fields at once: m_ActiveFontFeatures drives the
    /// mesh, the older m_enableKerning drives the preferred size. A feature list of exactly { 0 }
    /// means "not migrated yet" — TMP_Text.LoadDefaultSettings turns it into { kern } or { }
    /// following m_enableKerning when the object loads.
    ///
    /// The XML keeps m_enableKerning as the single switch: the list stays out of the file while it
    /// holds nothing but what the migration derives, and the importer runs that same migration so
    /// the built prefab already holds the derived value. Without the second half the designer file
    /// would see the migrated list as an override of the sentinel and would try to write it back on
    /// every apply. A list holding anything else — liga, mark, mkmk — is written and read as it is.
    /// </summary>
    internal static class TmpFontFeatures
    {
        private const string FeaturesField = "m_ActiveFontFeatures";
        private const string KerningField = "m_enableKerning";

        // OTL_FeatureTag.kern, the OpenType tag 'kern' packed into four bytes
        private const int KernTag = 'k' << 24 | 'e' << 16 | 'r' << 8 | 'n';

        /// <summary>
        /// True when the feature list holds exactly what TMP derives from m_enableKerning, so
        /// writing it to the XML would only pin down the result of the migration.
        /// </summary>
        public static bool IsDerivedFromKerningField(SerializedProperty prop)
        {
            if (prop.propertyPath != FeaturesField)
            {
                return false;
            }

            var kerning = prop.serializedObject.FindProperty(KerningField);
            if (kerning == null)
            {
                return false;
            }

            return kerning.boolValue
                ? prop.arraySize == 1 && prop.GetArrayElementAtIndex(0).intValue == KernTag
                : prop.arraySize == 0;
        }

        /// <summary>
        /// Replaces the legacy sentinel with the value TMP would migrate it to, the way
        /// TMP_Text.LoadDefaultSettings does when the object loads. A list the XML filled in
        /// itself is not a sentinel and is left alone.
        /// </summary>
        public static void MigrateSentinel(SerializedObject so)
        {
            var features = so.FindProperty(FeaturesField);
            var kerning = so.FindProperty(KerningField);

            if (features == null || kerning == null || !features.isArray)
            {
                return;
            }

            if (features.arraySize != 1 || features.GetArrayElementAtIndex(0).intValue != 0)
            {
                return;
            }

            if (kerning.boolValue)
            {
                features.GetArrayElementAtIndex(0).intValue = KernTag;
            }
            else
            {
                features.arraySize = 0;
            }
        }
    }
}