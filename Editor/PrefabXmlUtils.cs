using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace UnityPrefabXML
{
    public static class PrefabXmlUtils
    {
        public const string IndentChars = "    ";

        /// <summary>
        /// The layout every writer of a prefabxml file uses, so files produced by the converter,
        /// by the designer and by the reformat command all look the same.
        /// </summary>
        public static XmlWriterSettings CreateWriterSettings()
        {
            return new XmlWriterSettings
            {
                Indent = true,
                IndentChars = IndentChars,
                OmitXmlDeclaration = true,
                NewLineOnAttributes = false,
            };
        }

        /// <summary>
        /// Reads the document keeping the whitespace it was written with, so editing a single
        /// attribute leaves the layout of the rest of the file untouched.
        /// </summary>
        public static XDocument LoadXml(string path, out string originalText)
        {
            originalText = File.ReadAllText(path);
            return XDocument.Parse(originalText, LoadOptions.PreserveWhitespace);
        }

        /// <summary>
        /// Writes the document back only when it differs from the text it was read from.
        /// Returns false when the file was left untouched.
        /// </summary>
        public static bool SaveXmlIfChanged(XDocument doc, string path, string originalText)
        {
            if (string.Equals(ToXmlText(doc), originalText, StringComparison.Ordinal))
            {
                return false;
            }

            SaveXml(doc, path);
            return true;
        }

        public static string ToXmlText(XDocument doc)
        {
            var stringWriter = new StringWriter();
            using (var writer = XmlWriter.Create(stringWriter, CreateWriterSettings()))
            {
                doc.Save(writer);
            }

            return stringWriter.ToString();
        }

        public static void SaveXml(XDocument doc, string path)
        {
            using (var writer = XmlWriter.Create(path, CreateWriterSettings()))
            {
                doc.Save(writer);
            }
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