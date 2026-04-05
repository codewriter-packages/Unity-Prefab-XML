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
                            if (expectedType != null
                                && expectedType != "GameObject"
                                && expectedType[0] != '$')
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