using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityPrefabXML
{
    public static class PrefabToXmlConverter
    {
        private static readonly HashSet<string> SkipComponents = new HashSet<string>
        {
            "CanvasRenderer",
        };

        private static readonly HashSet<string> SkipProperties = new HashSet<string>
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

        [MenuItem("Assets/PrefabXML/Convert UGUI Prefab to PrefabXML", true)]
        private static bool ValidateConvert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<RectTransform>() != null)
                    return true;
            }
            return false;
        }

        [MenuItem("Assets/PrefabXML/Convert UGUI Prefab to PrefabXML")]
        private static void Convert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                ConvertOne(path);
            }
        }

        private static void ConvertOne(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"PrefabToXml: Cannot load prefab at '{path}'.");
                return;
            }

            var doc = ConvertPrefab(go, out var bindings);
            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };

            var outputPath = Path.ChangeExtension(path, ".prefabxml");
            using (var writer = System.Xml.XmlWriter.Create(outputPath, settings))
            {
                doc.Save(writer);
            }

            // First import — discovers bindings and their expected types
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            if (bindings.Count > 0)
            {
                var importer = (PrefabXmlImporter)AssetImporter.GetAtPath(outputPath);
                var result = PrefabXmlImporter.GetResult(outputPath);

                if (importer != null && result != null)
                {
                    foreach (var kvp in bindings)
                    {
                        var bindingName = kvp.Key;
                        var asset = kvp.Value;

                        if (result.discoveredBindings.TryGetValue(bindingName, out var expectedType))
                        {
                            var identifier = new AssetImporter.SourceAssetIdentifier(expectedType, bindingName);
                            importer.AddRemap(identifier, asset);
                        }
                    }

                    AssetDatabase.WriteImportSettingsIfDirty(outputPath);
                    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
                }
            }

            Debug.Log($"PrefabToXml: Converted '{path}' → '{outputPath}'");
        }

        public static XDocument ConvertPrefab(GameObject root)
        {
            return ConvertPrefab(root, out _);
        }

        public static XDocument ConvertPrefab(GameObject root, out Dictionary<string, Object> bindings)
        {
            var ctx = new ConvertContext { Root = root };
            AssignIds(root.transform, ctx);

            var rootElement = BuildGameObject(root, ctx);
            bindings = ctx.UsedBindings;
            var unityPrefab = new XElement("UnityPrefab",
                new XAttribute("format", "Packages/com.codewriter.unity-prefab-xml/FORMAT.md"),
                new XAttribute("guide", "Packages/com.codewriter.unity-prefab-xml/GUIDE.md"),
                rootElement);
            return new XDocument(unityPrefab);
        }

        private static void AssignIds(Transform t, ConvertContext ctx)
        {
            for (int i = 0; i < t.childCount; i++)
                AssignIds(t.GetChild(i), ctx);

            foreach (var comp in t.gameObject.GetComponents<Component>())
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.objectReferenceValue == null) continue;

                    GameObject refGo = null;
                    if (prop.objectReferenceValue is GameObject go)
                        refGo = go;
                    else if (prop.objectReferenceValue is Component c)
                        refGo = c.gameObject;

                    if (refGo != null && !ctx.GoToId.ContainsKey(refGo) &&
                        refGo.transform.IsChildOf(ctx.Root.transform))
                        ctx.GoToId[refGo] = SanitizeId(refGo.name, ctx);
                }
            }
        }

        private static string SanitizeId(string name, ConvertContext ctx)
        {
            var id = name.Replace(" ", "");
            while (ctx.UsedIds.Contains(id))
                id += "_";
            ctx.UsedIds.Add(id);
            return id;
        }

        private static XElement BuildGameObject(GameObject go, ConvertContext ctx)
        {
            var el = new XElement("GameObject", new XAttribute("name", go.name));

            if (ctx.GoToId.TryGetValue(go, out var id))
                el.Add(new XAttribute("id", id));
            if (!go.activeSelf)
                el.Add(new XAttribute("active", "false"));

            // Components (Transform/RectTransform is always first from GetComponents)
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (SkipComponents.Contains(comp.GetType().Name)) continue;
                el.Add(BuildComponent(comp, ctx));
            }

            // Children
            for (int i = 0; i < go.transform.childCount; i++)
                el.Add(BuildGameObject(go.transform.GetChild(i).gameObject, ctx));

            return el;
        }

        private static bool IsLeafProperty(SerializedProperty prop)
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
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.LayerMask:
                    return true;
                default:
                    return false;
            }
        }

        private static XElement BuildComponent(Component comp, ConvertContext ctx)
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
                    defaultComp = defaultGo.transform;
                else if (compType == typeof(RectTransform))
                    defaultComp = defaultGo.AddComponent<RectTransform>();
                else
                    defaultComp = defaultGo.AddComponent(compType);

                if (defaultComp != null)
                    defaultSo = new SerializedObject(defaultComp);
            }
            catch
            {
                // Some components may fail to add (e.g. require other components)
            }

            var refs = new List<XElement>();
            var fields = new List<object>();

            try
            {
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    var name = prop.propertyPath;

                    // Check root property name for skip (handles dot-notation like m_LocalPosition.x)
                    var rootName = name.Contains(".") ? name.Substring(0, name.IndexOf('.')) : name;
                    if (SkipProperties.Contains(rootName)) continue;
                    if (name.Contains(".Array.")) continue;

                    // Arrays → Field elements
                    if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                    {
                        if (IsDefault(prop, defaultSo))
                        {
                            enterChildren = false;
                            continue;
                        }

                        var fieldResult = BuildField(prop, ctx);
                        if (fieldResult != null)
                            fields.Add(fieldResult);
                        enterChildren = false;
                        continue;
                    }

                    if (prop.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        enterChildren = false;
                        continue;
                    }

                    // Leaf property — serialize directly (handles Vector2, Color, etc.)
                    if (IsLeafProperty(prop))
                    {
                        if (!IsDefault(prop, defaultSo))
                        {
                            var value = SerializeValue(prop, ctx);
                            if (value != null)
                                el.Add(new XAttribute(name, value));
                        }
                        enterChildren = false;
                        continue;
                    }

                    // Non-leaf struct — enter children for dot notation
                    // (depth limit: only go 1 level deep)
                    if (prop.depth >= 1)
                    {
                        enterChildren = false;
                        continue;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaultGo);
            }

            // Add child elements: Refs first, then Fields
            foreach (var field in fields)
            {
                if (field is FieldWithRefs fwr)
                {
                    foreach (var r in fwr.refs)
                        el.Add(r);
                }
            }
            foreach (var field in fields)
            {
                if (field is FieldWithRefs fwr)
                    el.Add(fwr.field);
                else if (field is XElement xe)
                    el.Add(xe);
            }

            return el;
        }

        private static bool IsDefault(SerializedProperty prop, SerializedObject defaultSo)
        {
            if (defaultSo == null) return false;

            var defaultProp = defaultSo.FindProperty(prop.propertyPath);
            if (defaultProp == null) return false;

            return SerializedProperty.DataEquals(prop, defaultProp);
        }

        private static object BuildField(SerializedProperty prop, ConvertContext ctx)
        {
            if (prop.arraySize == 0) return null;

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
                        item.Add(new XAttribute("ref", refStr));
                    fieldEl.Add(item);
                }
                return fieldEl;
            }

            // ManagedReference array
            if (firstElement.propertyType == SerializedPropertyType.ManagedReference)
            {
                var container = new List<XElement>();
                var fieldEl = new XElement("Field", new XAttribute("name", prop.propertyPath));

                for (int i = 0; i < prop.arraySize; i++)
                {
                    var elProp = prop.GetArrayElementAtIndex(i);
                    if (elProp.managedReferenceValue == null)
                    {
                        fieldEl.Add(new XElement("Item"));
                        continue;
                    }

                    var refId = $"ref{ctx.RefCounter++}";
                    var refEl = new XElement("Ref",
                        new XAttribute("id", refId),
                        new XAttribute("type", elProp.managedReferenceFullTypename));

                    // Serialize managed reference sub-properties
                    var copy = elProp.Copy();
                    bool enter = true;
                    while (copy.NextVisible(enter))
                    {
                        enter = false;
                        if (!copy.propertyPath.StartsWith(elProp.propertyPath + ".")) break;
                        var relName = copy.propertyPath.Substring(elProp.propertyPath.Length + 1);
                        if (relName.Contains(".")) continue;

                        var val = SerializeValue(copy, ctx);
                        if (val != null)
                            refEl.Add(new XAttribute(relName, val));
                    }

                    container.Add(refEl);
                    fieldEl.Add(new XElement("Item", new XAttribute("rid", refId)));
                }

                return new FieldWithRefs { refs = container, field = fieldEl };
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
                        if (!copy.propertyPath.StartsWith(elProp.propertyPath + ".")) break;
                        var relName = copy.propertyPath.Substring(elProp.propertyPath.Length + 1);
                        if (relName.Contains(".")) continue;

                        var val = SerializeValue(copy, ctx);
                        if (val != null)
                            item.Add(new XAttribute(relName, val));
                    }

                    fieldEl.Add(item);
                }
                return fieldEl;
            }
        }

        private static string SerializeValue(SerializedProperty prop, ConvertContext ctx)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Boolean:
                    return prop.boolValue ? "true" : "false";

                case SerializedPropertyType.String:
                    return prop.stringValue;

                case SerializedPropertyType.Color:
                    return FormatColor(prop.colorValue);

                case SerializedPropertyType.Enum:
                    var idx = prop.enumValueIndex;
                    if (idx < 0 || idx >= prop.enumNames.Length) return null;
                    return prop.enumNames[idx];

                case SerializedPropertyType.Vector2:
                    return FormatVector2(prop.vector2Value);

                case SerializedPropertyType.Vector3:
                    return FormatVector3(prop.vector3Value);

                case SerializedPropertyType.Vector4:
                    return FormatVector4(prop.vector4Value);

                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return FormatVector4(new Vector4(q.x, q.y, q.z, q.w));

                case SerializedPropertyType.ObjectReference:
                    return SerializeObjectReference(prop, ctx);

                default:
                    return null;
            }
        }

        private static string SerializeObjectReference(SerializedProperty prop, ConvertContext ctx)
        {
            var obj = prop.objectReferenceValue;
            if (obj == null) return null;

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
                return componentType != null ? $"#{id}/{componentType}" : $"#{id}";

            // External asset — convert to binding
            var bindingName = obj.name;
            while (ctx.UsedBindings.ContainsKey(bindingName))
                bindingName += "_";
            ctx.UsedBindings[bindingName] = obj;
            return $"{{{bindingName}}}";
        }

        private static string FormatVector2(Vector2 v) => $"{F(v.x)}, {F(v.y)}";
        private static string FormatVector3(Vector3 v) => $"{F(v.x)}, {F(v.y)}, {F(v.z)}";
        private static string FormatVector4(Vector4 v) => $"{F(v.x)}, {F(v.y)}, {F(v.z)}, {F(v.w)}";

        private static string F(float v)
        {
            if (Mathf.Approximately(v, Mathf.Round(v)))
                return ((int)Mathf.Round(v)).ToString(CultureInfo.InvariantCulture);
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatColor(Color c)
        {
            if (Mathf.Approximately(c.a, 1f))
                return $"#{ColorUtility.ToHtmlStringRGB(c)}";
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }

        private class ConvertContext
        {
            public GameObject Root;
            public Dictionary<GameObject, string> GoToId = new Dictionary<GameObject, string>();
            public HashSet<string> UsedIds = new HashSet<string>();
            public Dictionary<string, Object> UsedBindings = new Dictionary<string, Object>();
            public int RefCounter;
        }

        private class FieldWithRefs
        {
            public List<XElement> refs;
            public XElement field;
        }
    }
}
