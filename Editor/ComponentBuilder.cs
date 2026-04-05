using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML
{
    public static class ComponentBuilder
    {
        private static readonly string[] NamespacePriority =
        {
            "TMPro",
            "UnityEngine.UI",
            "UnityEngine",
        };

        private static Dictionary<string, Type> _shortNameCache;
        private static Dictionary<string, Type> _fullNameCache;

        public static void ProcessAll(XElement element, GameObject go, PrefabXmlBuildContext context)
        {
            EnsureCache();

            foreach (var compElement in element.Elements())
            {
                var tagName = compElement.Name.LocalName;

                if (tagName == "GameObject" || tagName == "Field")
                    continue;

                var type = ResolveType(tagName);
                if (type == null)
                {
                    var lineInfo = (IXmlLineInfo)compElement;
                    context.Ctx.LogImportWarning(
                        $"Unknown component '{tagName}' at line {lineInfo.LineNumber}. Skipped.");
                    continue;
                }

                // RectTransform/Transform already exist on GameObject
                Component component;
                if (typeof(Transform).IsAssignableFrom(type))
                {
                    component = go.GetComponent(type);
                    if (component == null)
                        component = go.AddComponent(type);
                }
                else
                {
                    component = go.AddComponent(type);
                }

                PropertySetter.ApplyAttributes(component, compElement, context);
            }

            // Recurse into child GameObjects
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName == "GameObject")
                {
                    var childGo = context.ElementToGameObject[child];
                    ProcessAll(child, childGo, context);
                }
            }
        }

        private static Type ResolveType(string tagName)
        {
            // Full name (contains dot) — exact lookup
            if (tagName.Contains('.'))
            {
                _fullNameCache.TryGetValue(tagName, out var type);
                return type;
            }

            // Short name
            _shortNameCache.TryGetValue(tagName, out var result);
            return result;
        }

        private static void EnsureCache()
        {
            if (_shortNameCache != null) return;

            _shortNameCache = new Dictionary<string, Type>();
            _fullNameCache = new Dictionary<string, Type>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (type.IsAbstract) continue;

                _shortNameCache[type.Name] = type;
                _fullNameCache[type.FullName] = type;
            }

            // Priority namespaces overwrite short names (last wins = highest priority)
            foreach (var ns in NamespacePriority)
            {
                foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
                {
                    if (type.IsAbstract) continue;
                    if (type.Namespace == ns)
                    {
                        _shortNameCache[type.Name] = type;
                    }
                }
            }
        }
    }
}