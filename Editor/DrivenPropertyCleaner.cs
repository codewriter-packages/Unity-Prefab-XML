using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnityPrefabXML
{
    /// <summary>
    /// A layout component overwrites part of the RectTransform of every object it controls, so the
    /// values written for those properties are noise — the file keeps them, the layout ignores them.
    /// Unity marks them through <see cref="DrivenRectTransformTracker"/>, which is filled in by a
    /// layout pass on a live object, so the prefab built from the XML is instantiated into a preview
    /// scene and laid out to find out what is driven.
    /// </summary>
    public static class DrivenPropertyCleaner
    {
        /// <summary>
        /// The flags behind every RectTransform attribute. An attribute is dropped only when all of
        /// its flags are driven: in "0, 140" under a group that controls the width only, the height
        /// is still the value the layout works from.
        /// </summary>
        private static readonly Dictionary<string, DrivenTransformProperties> DrivenAttributes =
            new Dictionary<string, DrivenTransformProperties>
            {
                {"m_AnchorMin", DrivenTransformProperties.AnchorMinX | DrivenTransformProperties.AnchorMinY},
                {"m_AnchorMin.x", DrivenTransformProperties.AnchorMinX},
                {"m_AnchorMin.y", DrivenTransformProperties.AnchorMinY},

                {"m_AnchorMax", DrivenTransformProperties.AnchorMaxX | DrivenTransformProperties.AnchorMaxY},
                {"m_AnchorMax.x", DrivenTransformProperties.AnchorMaxX},
                {"m_AnchorMax.y", DrivenTransformProperties.AnchorMaxY},

                {
                    "m_AnchoredPosition",
                    DrivenTransformProperties.AnchoredPositionX | DrivenTransformProperties.AnchoredPositionY
                },
                {"m_AnchoredPosition.x", DrivenTransformProperties.AnchoredPositionX},
                {"m_AnchoredPosition.y", DrivenTransformProperties.AnchoredPositionY},

                {"m_SizeDelta", DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.SizeDeltaY},
                {"m_SizeDelta.x", DrivenTransformProperties.SizeDeltaX},
                {"m_SizeDelta.y", DrivenTransformProperties.SizeDeltaY},

                {"m_Pivot", DrivenTransformProperties.PivotX | DrivenTransformProperties.PivotY},
                {"m_Pivot.x", DrivenTransformProperties.PivotX},
                {"m_Pivot.y", DrivenTransformProperties.PivotY},
            };

        // Unity keeps the driven flags of a RectTransform to itself
        private static readonly PropertyInfo DrivenPropertiesProperty = typeof(RectTransform)
            .GetProperty("drivenProperties", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Removes the driven properties from an already imported file and reimports it when
        /// something was removed.
        /// </summary>
        public static void CleanFile(string prefabXmlPath)
        {
            var xmlDoc = PrefabXmlUtils.LoadXml(prefabXmlPath, out var xmlText);

            if (Strip(xmlDoc, prefabXmlPath) &&
                PrefabXmlUtils.SaveXmlIfChanged(xmlDoc, prefabXmlPath, xmlText))
            {
                AssetDatabase.ImportAsset(prefabXmlPath, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>
        /// Removes the attributes driven by a layout from the document. The prefab imported from
        /// <paramref name="prefabXmlPath"/> is the object the layout runs on, so the document has to
        /// be the one that prefab was built from. Returns true when an attribute was removed.
        /// </summary>
        public static bool Strip(XDocument xmlDoc, string prefabXmlPath)
        {
            var rootElement = xmlDoc.Root?.Elements("GameObject").FirstOrDefault();
            if (rootElement == null)
            {
                return false;
            }

            if (DrivenPropertiesProperty == null)
            {
                Debug.LogWarning("PrefabXml: RectTransform.drivenProperties is not available, " +
                                 "driven properties are left in the XML.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabXmlPath);
            if (prefab == null)
            {
                return false;
            }

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // The prefab asset carries no driven flags — they are set while the layout runs,
                // and that needs an object living in a scene
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    return false;
                }

                RebuildLayout(instance.transform);
                return StripGameObject(instance.transform, rootElement);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// A rebuild walks down only as long as every object on the way controls its children, so
        /// each layout root has to be rebuilt on its own.
        /// </summary>
        private static void RebuildLayout(Transform transform)
        {
            if (transform is RectTransform rect && transform.GetComponent<ILayoutController>() != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                RebuildLayout(transform.GetChild(i));
            }
        }

        /// <summary>
        /// The prefab is built from the document, so both are walked in the same order.
        /// </summary>
        private static bool StripGameObject(Transform instance, XElement goElement)
        {
            var changed = false;

            if (instance is RectTransform rect)
            {
                var rectElement = goElement.Elements().FirstOrDefault(
                    el => PrefabXmlUtils.MatchesComponentType(el.Name.LocalName, typeof(RectTransform)));

                if (rectElement != null)
                {
                    changed |= StripRectTransform(rect, rectElement);
                }
            }

            var childElements = goElement.Elements("GameObject").ToList();

            // The document does not describe this instance anymore — matching by index below would
            // strip attributes off unrelated objects
            if (childElements.Count != instance.childCount)
            {
                return changed;
            }

            for (var i = 0; i < childElements.Count; i++)
            {
                changed |= StripGameObject(instance.GetChild(i), childElements[i]);
            }

            return changed;
        }

        private static bool StripRectTransform(RectTransform rect, XElement rectElement)
        {
            var driven = (DrivenTransformProperties) DrivenPropertiesProperty.GetValue(rect);
            if (driven == DrivenTransformProperties.None)
            {
                return false;
            }

            var changed = false;

            foreach (var attribute in rectElement.Attributes().ToList())
            {
                if (!DrivenAttributes.TryGetValue(attribute.Name.LocalName, out var flags))
                {
                    continue;
                }

                if ((driven & flags) != flags)
                {
                    continue;
                }

                attribute.Remove();
                changed = true;
            }

            return changed;
        }
    }
}
