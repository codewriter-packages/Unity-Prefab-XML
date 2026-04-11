using System;
using System.Xml.Linq;

namespace UnityPrefabXML
{
    public static class PrefabXmlUtils
    {
        public static string MakeUnique(string name, Func<string, bool> exists)
        {
            while (exists(name))
            {
                name += "_";
            }

            return name;
        }

        /// <summary>
        /// Returns true if the XML element represents a component tag
        /// (i.e. not a structural child like GameObject, Field, or Ref).
        /// </summary>
        public static bool IsComponentElement(XElement element)
        {
            var name = element.Name.LocalName;
            return name != "GameObject" && name != "Field" && name != "Ref";
        }

        /// <summary>
        /// Checks if an XML tag name matches a component type.
        /// Handles both short names ("Image") and full names ("UnityEngine.UI.Image").
        /// </summary>
        public static bool MatchesComponentType(string xmlTagName, Type componentType)
        {
            return xmlTagName == componentType.Name || xmlTagName == componentType.FullName;
        }

        public static bool IsBinding(string value)
        {
            return value.Length > 2 && value[0] == '{' && value[value.Length - 1] == '}';
        }

        public static string GetBindingName(string value)
        {
            return value.Substring(1, value.Length - 2);
        }
    }
}