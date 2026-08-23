using System;
using System.IO;
using System.Linq;
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
        /// Adds an element as the last child of the parent, on a line of its own.
        ///
        /// A document read with PreserveWhitespace carries its newlines as text nodes, which makes
        /// the writer treat it as mixed content and stop indenting on its own — that is what keeps
        /// hand-written layout intact. The flip side is that every element added to such a document
        /// has to bring its own whitespace, or it is written inline.
        /// </summary>
        public static void AddChild(XElement parent, XElement child)
        {
            var indent = GetChildIndent(parent);
            IndentSubtree(child, indent);

            if (AsIndent(parent.LastNode) != null)
            {
                // The closing tag already sits on a line of its own, the element goes in front of it
                parent.LastNode.AddBeforeSelf(new XText(indent), child);
            }
            else
            {
                parent.Add(new XText(indent), child, new XText(GetIndent(parent)));
            }
        }

        /// <summary>
        /// Adds an element as the first child of the parent, on a line of its own.
        /// </summary>
        public static void AddFirstChild(XElement parent, XElement child)
        {
            var indent = GetChildIndent(parent);
            IndentSubtree(child, indent);

            var wasEmpty = parent.FirstNode == null;
            parent.AddFirst(new XText(indent), child);

            if (wasEmpty)
            {
                parent.Add(new XText(GetIndent(parent)));
            }
        }

        /// <summary>
        /// Inserts an element behind another one, on a line of its own at the same indentation.
        /// </summary>
        public static void AddAfter(XElement anchor, XElement element)
        {
            var indent = GetIndent(anchor);
            IndentSubtree(element, indent);
            anchor.AddAfterSelf(new XText(indent), element);
        }

        /// <summary>
        /// Replaces an element with another one, keeping the line it was written on.
        /// </summary>
        public static void Replace(XElement existing, XElement replacement)
        {
            IndentSubtree(replacement, GetIndent(existing));
            existing.ReplaceWith(replacement);
        }

        /// <summary>
        /// Writes newlines and indentation into a subtree that was just built, so every tag of it
        /// ends up on a line of its own.
        /// </summary>
        public static void IndentSubtree(XElement element, string indent)
        {
            var children = element.Elements().ToList();
            if (children.Count == 0)
            {
                return;
            }

            var childIndent = indent + IndentChars;

            foreach (var child in children)
            {
                child.AddBeforeSelf(new XText(childIndent));
                IndentSubtree(child, childIndent);
            }

            element.Add(new XText(indent));
        }

        /// <summary>
        /// The newline and indentation an element sits behind, rebuilt from its depth when the
        /// element shares a line with something else.
        /// </summary>
        public static string GetIndent(XElement element)
        {
            var indent = AsIndent(element.PreviousNode);
            if (indent != null)
            {
                return indent;
            }

            var result = "\n";
            for (var ancestor = element.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                result += IndentChars;
            }

            return result;
        }

        /// <summary>
        /// The newline and indentation the children of an element are written with, taken from the
        /// children it already has, so a file indented by hand keeps its own style.
        /// </summary>
        public static string GetChildIndent(XElement parent)
        {
            foreach (var child in parent.Elements())
            {
                var indent = AsIndent(child.PreviousNode);
                if (indent != null)
                {
                    return indent;
                }
            }

            return GetIndent(parent) + IndentChars;
        }

        /// <summary>
        /// The indentation a whitespace node ends with, or null for anything else. Only the last
        /// line of it is taken, so blank lines between elements are not copied along with it.
        /// </summary>
        private static string AsIndent(XNode node)
        {
            if (!(node is XText text) || !string.IsNullOrWhiteSpace(text.Value))
            {
                return null;
            }

            var lineStart = text.Value.LastIndexOf('\n');
            return lineStart < 0 ? null : "\n" + text.Value.Substring(lineStart + 1);
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