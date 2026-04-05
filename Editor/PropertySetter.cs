using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML
{
    public static class PropertySetter
    {
        public static void ApplyAttributes(Component component, XElement element, PrefabXmlBuildContext context)
        {
            var so = new SerializedObject(component);

            foreach (var attr in element.Attributes())
            {
                var name = attr.Name.LocalName;
                var value = attr.Value;

                var prop = so.FindProperty(name);
                if (prop == null)
                {
                    var lineInfo = (IXmlLineInfo)element;
                    context.Ctx.LogImportWarning(
                        $"Unknown property '{name}' on {component.GetType().Name} at line {lineInfo.LineNumber}. Skipped.");
                    continue;
                }

                SetPropertyValue(prop, value, element, context);
            }

            // Process <Field> child elements for arrays/lists
            foreach (var fieldElement in element.Elements())
            {
                if (fieldElement.Name.LocalName != "Field") continue;

                var fieldName = fieldElement.Attribute("name")?.Value;
                if (fieldName == null) continue;

                var fieldProp = so.FindProperty(fieldName);
                if (fieldProp == null || !fieldProp.isArray) continue;

                var items = fieldElement.Elements("Item").ToList();
                fieldProp.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    var itemElement = items[i];
                    var arrayElement = fieldProp.GetArrayElementAtIndex(i);

                    // Set attributes on array element's sub-properties
                    foreach (var attr in itemElement.Attributes())
                    {
                        var subProp = arrayElement.FindPropertyRelative(attr.Name.LocalName);
                        if (subProp != null)
                        {
                            SetPropertyValue(subProp, attr.Value, itemElement, context);
                        }
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPropertyValue(SerializedProperty prop, string value,
            XElement element, PrefabXmlBuildContext context)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                    break;

                case SerializedPropertyType.Float:
                    prop.floatValue = float.Parse(value, CultureInfo.InvariantCulture);
                    break;

                case SerializedPropertyType.Boolean:
                    prop.boolValue = bool.Parse(value);
                    break;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;

                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = FindEnumIndex(prop, value);
                    break;

                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2(value);
                    break;

                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value);
                    break;

                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value);
                    break;

                case SerializedPropertyType.ObjectReference:
                    if (value.StartsWith("#"))
                    {
                        // Object reference by id
                        var refId = value.Substring(1);
                        var propPath = prop.propertyPath;
                        var targetObject = prop.serializedObject.targetObject;
                        var propType = prop.type;
                        context.DeferredActions.Add(() =>
                        {
                            if (!context.IdRegistry.TryGetValue(refId, out var referencedGo))
                            {
                                context.Ctx.LogImportWarning($"Unresolved reference '#{refId}'.");
                                return;
                            }

                            var so = new SerializedObject(targetObject);
                            var p = so.FindProperty(propPath);

                            var expectedType = ExtractPPtrTypeName(propType);
                            if (expectedType != null && expectedType.StartsWith("$"))
                                expectedType = expectedType.Substring(1);
                            if (expectedType != null
                                && expectedType != "GameObject")
                            {
                                p.objectReferenceValue = referencedGo.GetComponent(expectedType);
                            }
                            else
                            {
                                p.objectReferenceValue = referencedGo;
                            }

                            so.ApplyModifiedPropertiesWithoutUndo();
                        });
                    }
                    else
                    {
                        // Asset reference by path
                        var expectedType = ExtractPPtrTypeName(prop.type);
                        var assetType = ResolveAssetType(expectedType);
                        var asset = LoadAsset(value, assetType, prop, element, context);
                        if (asset != null)
                        {
                            prop.objectReferenceValue = asset;
                            context.Ctx.DependsOnSourceAsset(value);
                        }
                    }
                    break;

                default:
                {
                    var lineInfo = (IXmlLineInfo)element;
                    context.Ctx.LogImportWarning(
                        $"Unsupported property type '{prop.propertyType}' for '{prop.name}' at line {lineInfo.LineNumber}. Skipped.");
                    break;
                }
            }
        }

        private static string ExtractPPtrTypeName(string propType)
        {
            if (!propType.StartsWith("PPtr<")) return null;
            return propType.Substring(5, propType.Length - 6);
        }

        private static readonly Dictionary<string, System.Type> BuiltinAssetTypes = new Dictionary<string, System.Type>
        {
            { "$Sprite", typeof(Sprite) },
            { "$Texture2D", typeof(Texture2D) },
            { "$Material", typeof(Material) },
            { "$Font", typeof(Font) },
            { "$Shader", typeof(Shader) },
            { "$Mesh", typeof(Mesh) },
            { "$AudioClip", typeof(AudioClip) },
            { "$GameObject", typeof(GameObject) },
            { "Sprite", typeof(Sprite) },
            { "Texture2D", typeof(Texture2D) },
            { "Material", typeof(Material) },
            { "Font", typeof(Font) },
            { "Shader", typeof(Shader) },
            { "Mesh", typeof(Mesh) },
            { "AudioClip", typeof(AudioClip) },
            { "GameObject", typeof(GameObject) },
        };

        private static System.Type ResolveAssetType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(UnityEngine.Object);

            if (BuiltinAssetTypes.TryGetValue(typeName, out var builtinType))
                return builtinType;

            // Strip $ prefix for custom types
            if (typeName[0] == '$')
                typeName = typeName.Substring(1);

            // Search all assemblies for custom types
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
            }

            return typeof(UnityEngine.Object);
        }

        private static UnityEngine.Object LoadAsset(string path, System.Type assetType,
            SerializedProperty prop, XElement element, PrefabXmlBuildContext context)
        {
            var asset = AssetDatabase.LoadAssetAtPath(path, assetType);
            if (asset != null)
                return asset;

            // Sprite-specific: check if texture exists but not imported as Sprite
            if (assetType == typeof(Sprite))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    var lineInfo = (IXmlLineInfo)element;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var textureType = importer != null ? importer.textureType.ToString() : "unknown";
                    context.Ctx.LogImportError(
                        $"Texture at '{path}' is not imported as Sprite (current type: {textureType}). " +
                        $"Select the texture in Project window, set 'Texture Type' to 'Sprite (2D and UI)' and click Apply. " +
                        $"(line {lineInfo.LineNumber})");
                    return null;
                }
            }

            var li = (IXmlLineInfo)element;
            context.Ctx.LogImportWarning(
                $"Asset not found at '{path}' for '{prop.name}' (type={assetType.Name}) at line {li.LineNumber}. Skipped.");
            return null;
        }

        private static Color ParseColor(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color))
                return color;
            return Color.white;
        }

        private static int FindEnumIndex(SerializedProperty prop, string value)
        {
            var names = prop.enumDisplayNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            // Try matching internal enum names
            var internalNames = prop.enumNames;
            for (int i = 0; i < internalNames.Length; i++)
            {
                if (string.Equals(internalNames[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private static Vector2 ParseVector2(string value)
        {
            var parts = value.Split(',');
            return new Vector2(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
        }

        private static Vector3 ParseVector3(string value)
        {
            var parts = value.Split(',');
            return new Vector3(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture));
        }

        private static Vector4 ParseVector4(string value)
        {
            var parts = value.Split(',');
            return new Vector4(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture));
        }
    }
}