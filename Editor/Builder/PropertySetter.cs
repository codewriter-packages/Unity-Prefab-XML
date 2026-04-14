using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Builder
{
    public static class PropertySetter
    {
        public static void ApplyAttributes(Component component, XElement element, PrefabXmlBuildContext context)
        {
            var so = new SerializedObject(component);

            // Create managed reference instances once (scoped to this component)
            var managedRefInstances = new Dictionary<string, object>();
            var managedRefElements = new Dictionary<string, XElement>();
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName != "Ref") continue;

                var refId = child.Attribute("id")?.Value;
                if (refId == null)
                {
                    context.LogWarning("Ref element missing 'id' attribute. Skipped.", child);
                    continue;
                }

                var typeString = child.Attribute("type")?.Value;
                var type = ResolveManagedReferenceType(typeString);
                if (type == null)
                {
                    context.LogError($"Ref '{refId}': cannot resolve type '{typeString}'.", child);
                    continue;
                }

                managedRefInstances[refId] = Activator.CreateInstance(type);
                managedRefElements[refId] = child;
            }

            foreach (var attr in element.Attributes())
            {
                var name = attr.Name.LocalName;
                var value = attr.Value;

                var prop = so.FindProperty(name);
                if (prop == null)
                {
                    context.LogWarning(
                        $"Unknown property '{name}' on {component.GetType().Name}. Skipped.", element);
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.ManagedReference
                    && value.StartsWith("@"))
                {
                    ApplyManagedRefLink(prop, value.Substring(1),
                        managedRefInstances, managedRefElements, element, context);
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

                    var itemValue = itemElement.Attribute("v")?.Value;

                    // ManagedReference array element — use v="@id" or leave null
                    if (arrayElement.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        if (itemValue == null || !itemValue.StartsWith("@"))
                            continue; // null managed reference

                        var ridValue = itemValue.Substring(1);
                        if (!managedRefInstances.TryGetValue(ridValue, out var instance))
                        {
                            context.LogWarning($"Unresolved managed reference v='{itemValue}'.", itemElement);
                            continue;
                        }

                        arrayElement.managedReferenceValue = instance;
                        ApplyRefAttributes(arrayElement, ridValue,
                            managedRefInstances, managedRefElements, itemElement, context);
                        continue;
                    }

                    // Primitive or reference value via v="..."
                    if (itemValue != null)
                    {
                        SetPropertyValue(arrayElement, itemValue, itemElement, context);
                        continue;
                    }

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

        private static void ApplyManagedRefLink(SerializedProperty prop, string rid,
            Dictionary<string, object> managedRefInstances,
            Dictionary<string, XElement> managedRefElements,
            XElement sourceElement, PrefabXmlBuildContext context)
        {
            if (!managedRefInstances.TryGetValue(rid, out var instance))
            {
                context.LogWarning($"Unresolved managed reference '@{rid}'.", sourceElement);
                return;
            }

            prop.managedReferenceValue = instance;
            ApplyRefAttributes(prop, rid, managedRefInstances, managedRefElements, sourceElement, context);
        }

        private static void ApplyRefAttributes(SerializedProperty prop, string rid,
            Dictionary<string, object> managedRefInstances,
            Dictionary<string, XElement> managedRefElements,
            XElement sourceElement, PrefabXmlBuildContext context)
        {
            if (!managedRefElements.Remove(rid, out var refElement)) return;

            foreach (var attr in refElement.Attributes())
            {
                var attrName = attr.Name.LocalName;
                if (attrName == "id" || attrName == "type") continue;

                var subProp = prop.FindPropertyRelative(attrName);
                if (subProp == null)
                {
                    context.LogWarning(
                        $"Unknown property '{attrName}' on managed reference. Skipped.", refElement);
                    continue;
                }

                if (subProp.propertyType == SerializedPropertyType.ManagedReference
                    && attr.Value.StartsWith("@"))
                {
                    ApplyManagedRefLink(subProp, attr.Value.Substring(1),
                        managedRefInstances, managedRefElements, refElement, context);
                }
                else
                {
                    SetPropertyValue(subProp, attr.Value, refElement, context);
                }
            }
        }

        private static Type ResolveManagedReferenceType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString)) return null;

            // Format: "AssemblyName Namespace.ClassName"
            var spaceIndex = typeString.IndexOf(' ');
            if (spaceIndex < 0) return null;

            var assemblyName = typeString.Substring(0, spaceIndex);
            var typeName = typeString.Substring(spaceIndex + 1);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == assemblyName)
                {
                    return asm.GetType(typeName);
                }
            }

            return null;
        }

        private static void SetPropertyValue(SerializedProperty prop, string value,
            XElement element, PrefabXmlBuildContext context)
        {
            try
            {
                SetPropertyValueCore(prop, value, element, context);
            }
            catch (FormatException ex)
            {
                context.LogError($"Failed to parse value '{value}' for '{prop.name}': {ex.Message}", element);
            }
        }

        private static void SetPropertyValueCore(SerializedProperty prop, string value,
            XElement element, PrefabXmlBuildContext context)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (prop.type == "long")
                        prop.longValue = long.Parse(value, CultureInfo.InvariantCulture);
                    else
                        prop.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                    break;

                case SerializedPropertyType.Float:
                    if (prop.type == "double")
                        prop.doubleValue = double.Parse(value, CultureInfo.InvariantCulture);
                    else
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
                    if (int.TryParse(value, out var enumInt))
                    {
                        prop.intValue = enumInt;
                    }
                    else
                    {
                        var fieldInfo = ScriptAttributeUtilityProxy.GetFieldInfoAndStaticTypeFromProperty(prop, out _);
                        prop.intValue = (int) Enum.Parse(fieldInfo.FieldType, value, true);
                    }

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

                case SerializedPropertyType.Vector2Int:
                    prop.vector2IntValue = ParseVector2Int(value);
                    break;

                case SerializedPropertyType.Vector3Int:
                    prop.vector3IntValue = ParseVector3Int(value);
                    break;

                case SerializedPropertyType.Quaternion:
                {
                    var v = ParseVector4(value);
                    prop.quaternionValue = new Quaternion(v.x, v.y, v.z, v.w);
                    break;
                }

                case SerializedPropertyType.Rect:
                    prop.rectValue = ParseRect(value);
                    break;

                case SerializedPropertyType.RectInt:
                    prop.rectIntValue = ParseRectInt(value);
                    break;

                case SerializedPropertyType.Bounds:
                    prop.boundsValue = ParseBounds(value);
                    break;

                case SerializedPropertyType.BoundsInt:
                    prop.boundsIntValue = ParseBoundsInt(value);
                    break;

                case SerializedPropertyType.LayerMask:
                    prop.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                    break;

                case SerializedPropertyType.ObjectReference:
                    if (PrefabXmlUtils.IsBinding(value))
                    {
                        var bindingName = PrefabXmlUtils.GetBindingName(value);
                        var expectedType = ExtractPPtrTypeName(prop.type);
                        var targetType = ResolveAssetType(expectedType);

                        if (context.DiscoveredBindings.TryGetValue(bindingName, out var existingType)
                            && existingType != targetType)
                        {
                            context.LogError(
                                $"Binding '{{{bindingName}}}' used with conflicting types: {existingType.Name} and {targetType.Name}.",
                                element);
                        }

                        context.DiscoveredBindings[bindingName] = targetType;

                        if (context.AssetBindings.TryGetValue(bindingName, out var boundAsset))
                        {
                            prop.objectReferenceValue = boundAsset;

                            var assetPath = AssetDatabase.GetAssetPath(boundAsset);
                            if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
                                context.Ctx.DependsOnSourceAsset(assetPath);
                        }
                    }
                    else if (value.StartsWith("#"))
                    {
                        // Object reference by id: #objectId or #objectId/ComponentType
                        var refValue = value.Substring(1);
                        var propPath = prop.propertyPath;
                        var targetObject = prop.serializedObject.targetObject;
                        var propType = prop.type;

                        string refId;
                        string explicitComponent;
                        var slashIndex = refValue.IndexOf('/');
                        if (slashIndex >= 0)
                        {
                            refId = refValue.Substring(0, slashIndex);
                            explicitComponent = refValue.Substring(slashIndex + 1);
                        }
                        else
                        {
                            refId = refValue;
                            explicitComponent = null;
                        }

                        context.DeferredActions.Add(() =>
                        {
                            if (!context.IdRegistry.TryGetValue(refId, out var referencedGo))
                            {
                                context.LogWarning($"Unresolved reference '#{refId}'.");
                                return;
                            }

                            var so = new SerializedObject(targetObject);
                            var p = so.FindProperty(propPath);

                            if (explicitComponent != null)
                            {
                                p.objectReferenceValue = referencedGo.GetComponent(explicitComponent);
                            }
                            else
                            {
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
                    context.LogWarning(
                        $"Unsupported property type '{prop.propertyType}' for '{prop.name}'. Skipped.", element);
                    break;
                }
            }
        }

        internal static string ExtractPPtrTypeName(string propType)
        {
            if (!propType.StartsWith("PPtr<")) return null;
            return propType.Substring(5, propType.Length - 6);
        }

        private static readonly Dictionary<string, System.Type> BuiltinAssetTypes = new Dictionary<string, System.Type>
        {
            {"$Sprite", typeof(Sprite)},
            {"$Texture2D", typeof(Texture2D)},
            {"$Material", typeof(Material)},
            {"$Font", typeof(Font)},
            {"$Shader", typeof(Shader)},
            {"$Mesh", typeof(Mesh)},
            {"$AudioClip", typeof(AudioClip)},
            {"$GameObject", typeof(GameObject)},
            {"Sprite", typeof(Sprite)},
            {"Texture2D", typeof(Texture2D)},
            {"Material", typeof(Material)},
            {"Font", typeof(Font)},
            {"Shader", typeof(Shader)},
            {"Mesh", typeof(Mesh)},
            {"AudioClip", typeof(AudioClip)},
            {"GameObject", typeof(GameObject)},
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
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var textureType = importer != null ? importer.textureType.ToString() : "unknown";
                    context.LogError(
                        $"Texture at '{path}' is not imported as Sprite (current type: {textureType}). " +
                        $"Select the texture in Project window, set 'Texture Type' to 'Sprite (2D and UI)' and click Apply.",
                        element);
                    return null;
                }
            }

            context.LogWarning(
                $"Asset not found at '{path}' for '{prop.name}' (type={assetType.Name}). Skipped.", element);
            return null;
        }

        private static Color ParseColor(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color))
            {
                return color;
            }

            return Color.white;
        }

        private static float[] ParseFloats(string value, int expectedCount, out int actualCount)
        {
            var parts = value.Split(',');
            actualCount = parts.Length;
            if (parts.Length != expectedCount)
            {
                return null;
            }

            var result = new float[expectedCount];
            for (var i = 0; i < expectedCount; i++)
            {
                result[i] = float.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static int[] ParseInts(string value, int expectedCount, out int actualCount)
        {
            var parts = value.Split(',');
            actualCount = parts.Length;
            if (parts.Length != expectedCount)
            {
                return null;
            }

            var result = new int[expectedCount];
            for (var i = 0; i < expectedCount; i++)
            {
                result[i] = int.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static Vector2 ParseVector2(string value)
        {
            return ParseFloats(value, 2, out var actualCount) is { } p
                ? new Vector2(p[0], p[1])
                : throw new FormatException($"Expected 2 components for Vector2, but got {actualCount} in '{value}'");
        }

        private static Vector3 ParseVector3(string value)
        {
            return ParseFloats(value, 3, out var actualCount) is { } p
                ? new Vector3(p[0], p[1], p[2])
                : throw new FormatException($"Expected 3 components for Vector3, but got {actualCount} in '{value}'");
        }

        private static Vector4 ParseVector4(string value)
        {
            return ParseFloats(value, 4, out var actualCount) is { } p
                ? new Vector4(p[0], p[1], p[2], p[3])
                : throw new FormatException($"Expected 4 components for Vector4, but got {actualCount} in '{value}'");
        }

        private static Vector2Int ParseVector2Int(string value)
        {
            return ParseInts(value, 2, out var actualCount) is { } p
                ? new Vector2Int(p[0], p[1])
                : throw new FormatException($"Expected 2 components for Vector2Int, but got {actualCount} in '{value}'");
        }

        private static Vector3Int ParseVector3Int(string value)
        {
            return ParseInts(value, 3, out var actualCount) is { } p
                ? new Vector3Int(p[0], p[1], p[2])
                : throw new FormatException($"Expected 3 components for Vector3Int, but got {actualCount} in '{value}'");
        }

        private static Rect ParseRect(string value)
        {
            return ParseFloats(value, 4, out var actualCount) is { } p
                ? new Rect(p[0], p[1], p[2], p[3])
                : throw new FormatException($"Expected 4 components for Rect, but got {actualCount} in '{value}'");
        }

        private static RectInt ParseRectInt(string value)
        {
            return ParseInts(value, 4, out var actualCount) is { } p
                ? new RectInt(p[0], p[1], p[2], p[3])
                : throw new FormatException($"Expected 4 components for RectInt, but got {actualCount} in '{value}'");
        }

        private static Bounds ParseBounds(string value)
        {
            return ParseFloats(value, 6, out var actualCount) is { } p
                ? new Bounds(new Vector3(p[0], p[1], p[2]), new Vector3(p[3], p[4], p[5]))
                : throw new FormatException($"Expected 6 components for Bounds, but got {actualCount} in '{value}'");
        }

        private static BoundsInt ParseBoundsInt(string value)
        {
            return ParseInts(value, 6, out var actualCount) is { } p
                ? new BoundsInt(new Vector3Int(p[0], p[1], p[2]), new Vector3Int(p[3], p[4], p[5]))
                : throw new FormatException($"Expected 6 components for BoundsInt, but got {actualCount} in '{value}'");
        }
    }
}