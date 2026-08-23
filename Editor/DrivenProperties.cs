using System;
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
    /// A layout component overwrites part of the RectTransform of every object it controls, so a
    /// value written for one of those properties is noise — the file would keep it and the layout
    /// would ignore it. Unity marks them through <see cref="DrivenRectTransformTracker"/>, which is
    /// filled in by a layout pass on a live object, so a prefab is instantiated into a preview scene
    /// and laid out to find out what is driven.
    ///
    /// What this package writes itself is held to that. A value already in a file was put there by
    /// someone who meant it, and nothing takes it back out behind their back — the one thing that
    /// removes driven values from a finished file is the reformat command, which they press.
    /// </summary>
    public static class DrivenProperties
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

        private static bool _warned;

        /// <summary>
        /// True when every flag behind the attribute is driven, so writing the value would say
        /// nothing. Everything that asks about driven values comes through here, so no two callers
        /// can disagree about what counts as one.
        /// </summary>
        public static bool IsDriven(string attributeName, DrivenTransformProperties driven)
        {
            return DrivenAttributes.TryGetValue(attributeName, out var flags) && (driven & flags) == flags;
        }

        /// <summary>
        /// True when a layout is able to take the attribute over at all. Answered off the table
        /// alone, which is what makes it the cheap question to ask before measuring anything.
        /// </summary>
        public static bool CanBeDriven(string attributeName)
        {
            return DrivenAttributes.ContainsKey(attributeName);
        }

        /// <summary>
        /// What the layout of the prefab takes over, keyed by the instance id of the RectTransform
        /// of the prefab itself. Costs a preview scene and a layout pass, so the caller is expected
        /// to ask once and keep the answer.
        /// </summary>
        public static Dictionary<int, DrivenTransformProperties> MeasureDriven(GameObject prefab)
        {
            var map = new Dictionary<int, DrivenTransformProperties>();

            if (prefab == null || !Available())
            {
                return map;
            }

            InPreviewScene(prefab, instance =>
            {
                CollectDriven(instance.transform, prefab.transform, map);
                return true;
            });

            return map;
        }

        /// <summary>What a measurement says about one object, None for one it never reached.</summary>
        public static DrivenTransformProperties Lookup(Dictionary<int, DrivenTransformProperties> driven,
            Transform source)
        {
            return driven.TryGetValue(source.GetInstanceID(), out var flags)
                ? flags
                : DrivenTransformProperties.None;
        }

        /// <summary>
        /// Drops from a document the values the layout of <paramref name="prefab"/> computes. The
        /// document has to be the one that prefab was built from, or was written from it, so that
        /// the two describe the same objects. Returns true when an attribute was removed.
        /// </summary>
        public static bool StripDocument(XDocument xmlDoc, GameObject prefab)
        {
            var rootElement = xmlDoc.Root?.Elements("GameObject").FirstOrDefault();
            if (rootElement == null || prefab == null)
            {
                return false;
            }

            return StripSubtree(rootElement, prefab.transform, MeasureDriven(prefab));
        }

        /// <summary>
        /// Drops the values a layout computes out of a subtree that was just written. The element
        /// was serialized from <paramref name="source"/>, so the two are walked side by side.
        /// Returns true when an attribute was removed.
        /// </summary>
        public static bool StripSubtree(XElement goElement, Transform source,
            Dictionary<int, DrivenTransformProperties> driven)
        {
            var changed = false;
            var flags = Lookup(driven, source);

            if (flags != DrivenTransformProperties.None)
            {
                var rectElement = goElement.Elements().FirstOrDefault(
                    el => PrefabXmlUtils.MatchesComponentType(el.Name.LocalName, typeof(RectTransform)));

                if (rectElement != null)
                {
                    changed = StripRectTransform(rectElement, flags);
                }
            }

            var childElements = goElement.Elements("GameObject").ToList();

            // The element is written from this object, so the two lists line up. When they do not,
            // the element did not come from it after all, and matching by index below would strip
            // attributes off something unrelated.
            if (childElements.Count != source.childCount)
            {
                return changed;
            }

            for (var i = 0; i < childElements.Count; i++)
            {
                changed |= StripSubtree(childElements[i], source.GetChild(i), driven);
            }

            return changed;
        }

        private static bool StripRectTransform(XElement rectElement, DrivenTransformProperties driven)
        {
            var changed = false;

            foreach (var attribute in rectElement.Attributes().ToList())
            {
                if (!IsDriven(attribute.Name.LocalName, driven))
                {
                    continue;
                }

                attribute.Remove();
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// The instance is a plain copy of the prefab, so both are walked side by side and every
        /// answer is filed under the object of the prefab the caller has in hand.
        /// </summary>
        private static void CollectDriven(Transform instance, Transform source,
            Dictionary<int, DrivenTransformProperties> map)
        {
            if (instance is RectTransform rect)
            {
                var driven = (DrivenTransformProperties) DrivenPropertiesProperty.GetValue(rect);
                if (driven != DrivenTransformProperties.None)
                {
                    map[source.GetInstanceID()] = driven;
                }
            }

            var count = Mathf.Min(instance.childCount, source.childCount);
            for (var i = 0; i < count; i++)
            {
                CollectDriven(instance.GetChild(i), source.GetChild(i), map);
            }
        }

        /// <summary>
        /// Runs the layout of a prefab where it can be read off. The prefab asset carries no driven
        /// flags — they are set while the layout runs, and that needs an object living in a scene.
        /// </summary>
        private static T InPreviewScene<T>(GameObject prefab, Func<GameObject, T> body)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    return default;
                }

                ActivateAll(instance.transform);
                RebuildLayout(instance.transform);
                return body(instance);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// A layout lays out the children it can see and leaves the switched-off ones alone, and a
        /// fitter on an object that is off never runs at all — so asking an object as it stands
        /// answers that nothing about it is driven, and the file keeps the size and the anchors a
        /// layout takes over the moment the object comes on. The question worth asking is what
        /// would be driven if everything were on, so the copy is switched on whole.
        ///
        /// Nothing of this leaves the preview scene: the copy is thrown away with it and never
        /// written anywhere, so the active states of the prefab and of the file stay the ones the
        /// user set. Only the driven flags are read back, and those do not depend on which siblings
        /// took part in the pass.
        /// </summary>
        private static void ActivateAll(Transform transform)
        {
            // Top down, so a child is switched on into a parent that is already on
            if (!transform.gameObject.activeSelf)
            {
                transform.gameObject.SetActive(true);
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                ActivateAll(transform.GetChild(i));
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
        /// Said once: a measurement runs whenever the editor asks what a layout controls, and a
        /// warning per answer would bury everything else in the console.
        /// </summary>
        private static bool Available()
        {
            if (DrivenPropertiesProperty != null)
            {
                return true;
            }

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("PrefabXml: RectTransform.drivenProperties is not available, " +
                                 "the values a layout computes are written to the XML like any other.");
            }

            return false;
        }
    }
}