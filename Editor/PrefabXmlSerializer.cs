using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML
{
    public static class PrefabXmlSerializer
    {
        public static readonly HashSet<string> SkipComponents = new HashSet<string>
        {
            "CanvasRenderer",
        };

        public static readonly HashSet<string> SkipProperties = new HashSet<string>
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier",
            "m_Children",
            "m_Father",
            "m_RootOrder",
            "m_LocalEulerAnglesHint",
            "m_LocalPosition",
            "m_LocalRotation",
            "m_LocalScale",
            "m_ConstrainProportionsScale",
            "m_TextStyleHashCode",
        };

        public static HashSet<string> CollectBindingNames(XDocument xmlDoc)
        {
            var names = new HashSet<string>();
            foreach (var el in xmlDoc.Descendants())
            {
                foreach (var attr in el.Attributes())
                {
                    if (PrefabXmlUtils.IsBinding(attr.Value))
                    {
                        names.Add(PrefabXmlUtils.GetBindingName(attr.Value));
                    }
                }
            }

            return names;
        }
    
        public static void AssignIds(Transform t, PrefabXmlSerializationContext ctx)
        {
            for (int i = 0; i < t.childCount; i++)
                AssignIds(t.GetChild(i), ctx);

            foreach (var comp in t.gameObject.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (prop.objectReferenceValue == null)
                    {
                        continue;
                    }

                    GameObject refGo = null;
                    if (prop.objectReferenceValue is GameObject go)
                    {
                        refGo = go;
                    }
                    else if (prop.objectReferenceValue is Component c)
                    {
                        refGo = c.gameObject;
                    }

                    if (refGo != null && !ctx.GoToId.ContainsKey(refGo) &&
                        refGo.transform.IsChildOf(ctx.Root.transform))
                    {
                        ctx.GoToId[refGo] = SanitizeId(refGo.name, ctx);
                    }
                }
            }
        }

        public static string SanitizeId(string name, PrefabXmlSerializationContext ctx)
        {
            var id = PrefabXmlUtils.MakeUnique(name.Replace(" ", ""), ctx.UsedIds.Contains);
            ctx.UsedIds.Add(id);
            return id;
        }

        public static XElement SerializeGameObject(GameObject go, PrefabXmlSerializationContext ctx)
        {
            var el = new XElement("GameObject", new XAttribute("name", go.name));

            if (ctx.GoToId.TryGetValue(go, out var id))
            {
                el.Add(new XAttribute("id", id));
            }

            if (!go.activeSelf)
            {
                el.Add(new XAttribute("active", "false"));
            }

            // Components (Transform/RectTransform is always first from GetComponents)
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                if (SkipComponents.Contains(comp.GetType().Name))
                {
                    continue;
                }

                el.Add(SerializeComponent(comp, ctx));
            }

            // Children
            for (int i = 0; i < go.transform.childCount; i++)
                el.Add(SerializeGameObject(go.transform.GetChild(i).gameObject, ctx));

            return el;
        }

        public static bool IsSkipProperty(string propertyPath)
        {
            var rootName = propertyPath.Contains(".")
                ? propertyPath.Substring(0, propertyPath.IndexOf('.'))
                : propertyPath;
            return SkipProperties.Contains(rootName);
        }

        public static bool IsLeafProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.BoundsInt:
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.LayerMask:
                    return true;
                default:
                    return false;
            }
        }

        public static XElement SerializeComponent(Component comp, PrefabXmlSerializationContext ctx)
        {
            var typeName = comp.GetType().Name;
            var el = new XElement(typeName);
            var so = new SerializedObject(comp);

            // Create a default instance to compare against
            SerializedObject defaultSo = null;
            var defaultGo = new GameObject("__default_temp");
            try
            {
                var compType = comp.GetType();
                Component defaultComp;
                if (compType == typeof(Transform))
                {
                    defaultComp = defaultGo.transform;
                }
                else if (compType == typeof(RectTransform))
                {
                    defaultComp = defaultGo.AddComponent<RectTransform>();
                }
                else
                {
                    defaultComp = defaultGo.AddComponent(compType);
                }

                if (defaultComp != null)
                {
                    defaultSo = new SerializedObject(defaultComp);
                }
            }
            catch
            {
                // Some components may fail to add (e.g. require other components)
            }

            var allRefs = new List<XElement>();
            var fields = new List<XElement>();

            try
            {
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    var name = prop.propertyPath;

                    // Check root property name for skip (handles dot-notation like m_LocalPosition.x)
                    if (IsSkipProperty(name))
                    {
                        continue;
                    }

                    if (name.Contains(".Array."))
                    {
                        continue;
                    }

                    // Arrays → Field elements
                    if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                    {
                        if (IsDefaultValue(prop, defaultSo))
                        {
                            enterChildren = false;
                            continue;
                        }

                        var fieldResult = SerializeField(prop, ctx, allRefs);
                        if (fieldResult != null)
                        {
                            fields.Add(fieldResult);
                        }

                        enterChildren = false;
                        continue;
                    }

                    // Standalone ManagedReference → @refId attribute + Ref elements
                    if (prop.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        if (!IsDefaultValue(prop, defaultSo))
                        {
                            var refId = SerializeManagedReference(prop, ctx, allRefs);
                            if (refId != null)
                            {
                                el.Add(new XAttribute(name, $"@{refId}"));
                            }
                        }

                        enterChildren = false;
                        continue;
                    }

                    // Leaf property — serialize directly (handles Vector2, Color, etc.)
                    if (IsLeafProperty(prop))
                    {
                        if (!IsDefaultValue(prop, defaultSo))
                        {
                            var value = SerializeValue(prop, ctx);
                            if (value != null)
                            {
                                el.Add(new XAttribute(name, value));
                            }
                        }

                        enterChildren = false;
                        continue;
                    }

                    // Non-leaf struct — enter children for dot notation
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaultGo);
            }

            // Add child elements: Refs first, then Fields
            foreach (var r in allRefs)
                el.Add(r);
            foreach (var field in fields)
                el.Add(field);

            return el;
        }

        public static bool IsDefaultValue(SerializedProperty prop, SerializedObject defaultSo)
        {
            if (defaultSo == null)
            {
                return false;
            }

            var defaultProp = defaultSo.FindProperty(prop.propertyPath);
            if (defaultProp == null)
            {
                return false;
            }

            return SerializedProperty.DataEquals(prop, defaultProp);
        }

        public static XElement SerializeField(SerializedProperty prop, PrefabXmlSerializationContext ctx, List<XElement> allRefs)
        {
            if (prop.arraySize == 0)
            {
                return null;
            }

            var firstElement = prop.GetArrayElementAtIndex(0);

            // ObjectReference array
            if (firstElement.propertyType == SerializedPropertyType.ObjectReference)
            {
                var fieldEl = new XElement("Field", new XAttribute("name", prop.propertyPath));
                for (int i = 0; i < prop.arraySize; i++)
                {
                    var refStr = SerializeObjectReference(prop.GetArrayElementAtIndex(i), ctx);
                    var item = new XElement("Item");
                    if (refStr != null)
                    {
                        item.Add(new XAttribute("v", refStr));
                    }

                    fieldEl.Add(item);
                }

                return fieldEl;
            }

            // ManagedReference array
            if (firstElement.propertyType == SerializedPropertyType.ManagedReference)
            {
                var fieldEl = new XElement("Field", new XAttribute("name", prop.propertyPath));

                for (int i = 0; i < prop.arraySize; i++)
                {
                    var elProp = prop.GetArrayElementAtIndex(i);
                    if (elProp.managedReferenceValue == null)
                    {
                        fieldEl.Add(new XElement("Item"));
                        continue;
                    }

                    var refId = SerializeManagedReference(elProp, ctx, allRefs);
                    fieldEl.Add(new XElement("Item", new XAttribute("v", "@" + refId)));
                }

                return fieldEl;
            }

            // Regular struct array
            {
                var fieldEl = new XElement("Field", new XAttribute("name", prop.propertyPath));
                for (int i = 0; i < prop.arraySize; i++)
                {
                    var elProp = prop.GetArrayElementAtIndex(i);
                    var item = new XElement("Item");

                    var copy = elProp.Copy();
                    bool enter = true;
                    while (copy.NextVisible(enter))
                    {
                        enter = false;
                        if (!copy.propertyPath.StartsWith(elProp.propertyPath + "."))
                        {
                            break;
                        }

                        var relName = copy.propertyPath.Substring(elProp.propertyPath.Length + 1);
                        if (relName.Contains("."))
                        {
                            continue;
                        }

                        var val = SerializeValue(copy, ctx);
                        if (val != null)
                        {
                            item.Add(new XAttribute(relName, val));
                        }
                    }

                    fieldEl.Add(item);
                }

                return fieldEl;
            }
        }

        /// <summary>
        /// Serializes a ManagedReference property into a Ref element, handling nested ManagedReferences recursively.
        /// Returns the ref id, or null if the managed reference value is null.
        /// </summary>
        public static string SerializeManagedReference(SerializedProperty prop, PrefabXmlSerializationContext ctx,
            List<XElement> allRefs)
        {
            if (prop.managedReferenceValue == null)
            {
                return null;
            }

            var refId = $"ref{ctx.RefCounter++}";
            var refEl = new XElement("Ref",
                new XAttribute("id", refId),
                new XAttribute("type", prop.managedReferenceFullTypename));

            var copy = prop.Copy();
            bool enter = true;
            while (copy.NextVisible(enter))
            {
                enter = false;
                if (!copy.propertyPath.StartsWith(prop.propertyPath + "."))
                {
                    break;
                }

                var relName = copy.propertyPath.Substring(prop.propertyPath.Length + 1);
                if (relName.Contains("."))
                {
                    continue;
                }

                if (copy.propertyType == SerializedPropertyType.ManagedReference)
                {
                    var nestedRefId = SerializeManagedReference(copy, ctx, allRefs);
                    if (nestedRefId != null)
                    {
                        refEl.Add(new XAttribute(relName, $"@{nestedRefId}"));
                    }
                }
                else
                {
                    var val = SerializeValue(copy, ctx);
                    if (val != null)
                    {
                        refEl.Add(new XAttribute(relName, val));
                    }
                }
            }

            allRefs.Add(refEl);
            return refId;
        }

        public static string SerializeValue(SerializedProperty prop, PrefabXmlSerializationContext ctx)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (prop.type == "long")
                        return prop.longValue.ToString(CultureInfo.InvariantCulture);
                    return prop.intValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Float:
                    if (prop.type == "double")
                        return prop.doubleValue.ToString(CultureInfo.InvariantCulture);
                    return prop.floatValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Boolean:
                    return prop.boolValue ? "true" : "false";

                case SerializedPropertyType.String:
                    return prop.stringValue;

                case SerializedPropertyType.Color:
                    return FormatColor(prop.colorValue);

                case SerializedPropertyType.Enum:
                    var fieldInfo = ScriptAttributeUtilityProxy.GetFieldInfoAndStaticTypeFromProperty(prop, out _);
                    return Enum.ToObject(fieldInfo.FieldType, prop.intValue).ToString();

                case SerializedPropertyType.Vector2:
                    return FormatVector2(prop.vector2Value);

                case SerializedPropertyType.Vector3:
                    return FormatVector3(prop.vector3Value);

                case SerializedPropertyType.Vector4:
                    return FormatVector4(prop.vector4Value);

                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return $"{v2i.x}, {v2i.y}";

                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return $"{v3i.x}, {v3i.y}, {v3i.z}";

                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return FormatVector4(new Vector4(q.x, q.y, q.z, q.w));

                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return $"{F(r.x)}, {F(r.y)}, {F(r.width)}, {F(r.height)}";

                case SerializedPropertyType.RectInt:
                    var ri = prop.rectIntValue;
                    return $"{ri.x}, {ri.y}, {ri.width}, {ri.height}";

                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return $"{F(b.center.x)}, {F(b.center.y)}, {F(b.center.z)}, {F(b.size.x)}, {F(b.size.y)}, {F(b.size.z)}";

                case SerializedPropertyType.BoundsInt:
                    var bi = prop.boundsIntValue;
                    return $"{bi.position.x}, {bi.position.y}, {bi.position.z}, {bi.size.x}, {bi.size.y}, {bi.size.z}";

                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.ObjectReference:
                    return SerializeObjectReference(prop, ctx);

                default:
                    return null;
            }
        }

        public static string SerializeObjectReference(SerializedProperty prop, PrefabXmlSerializationContext ctx)
        {
            var obj = prop.objectReferenceValue;
            if (obj == null)
            {
                return null;
            }

            GameObject refGo = null;
            string componentType = null;

            if (obj is GameObject go)
            {
                refGo = go;
            }
            else if (obj is Component comp)
            {
                refGo = comp.gameObject;
                componentType = comp.GetType().Name;
            }

            if (refGo != null && ctx.GoToId.TryGetValue(refGo, out var id))
            {
                return componentType != null ? $"#{id}/{componentType}" : $"#{id}";
            }

            // External asset — convert to binding
            var bindingName = PrefabXmlUtils.MakeUnique(obj.name, ctx.UsedBindings.ContainsKey);
            ctx.UsedBindings[bindingName] = obj;
            return $"{{{bindingName}}}";
        }

        public static string FormatVector2(Vector2 v) => $"{F(v.x)}, {F(v.y)}";
        public static string FormatVector3(Vector3 v) => $"{F(v.x)}, {F(v.y)}, {F(v.z)}";
        public static string FormatVector4(Vector4 v) => $"{F(v.x)}, {F(v.y)}, {F(v.z)}, {F(v.w)}";

        public static string F(float v)
        {
            if (Mathf.Approximately(v, Mathf.Round(v)))
            {
                return ((int) Mathf.Round(v)).ToString(CultureInfo.InvariantCulture);
            }

            return v.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatColor(Color c)
        {
            if (Mathf.Approximately(c.a, 1f))
            {
                return $"#{ColorUtility.ToHtmlStringRGB(c)}";
            }

            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }

        public class PrefabXmlSerializationContext
        {
            public GameObject Root;
            public Dictionary<GameObject, string> GoToId = new Dictionary<GameObject, string>();
            public HashSet<string> UsedIds = new HashSet<string>();
            public Dictionary<string, Object> UsedBindings = new Dictionary<string, Object>();
            public int RefCounter;
        }
    }
}