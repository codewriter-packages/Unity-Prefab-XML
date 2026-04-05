using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace UnityPrefabXML
{
    public static class GameObjectBuilder
    {
        public static GameObject Build(XElement element, Transform parent, PrefabXmlBuildContext context)
        {
            string name = element.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                var lineInfo = (IXmlLineInfo)element;
                context.Ctx.LogImportWarning(
                    $"<GameObject> at line {lineInfo.LineNumber} has no 'name' attribute. Using 'Unnamed'.");
                name = "Unnamed";
            }

            var go = new GameObject(name);

            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
                context.RegisterSubObject(go);
            }

            // active attribute
            var activeAttr = element.Attribute("active");
            if (activeAttr != null && bool.TryParse(activeAttr.Value, out bool isActive))
            {
                go.SetActive(isActive);
            }

            // id attribute
            var idAttr = element.Attribute("id");
            if (idAttr != null)
            {
                var id = idAttr.Value;
                if (context.IdRegistry.ContainsKey(id))
                {
                    context.Ctx.LogImportWarning(
                        $"Duplicate id '{id}' on <GameObject name=\"{name}\">. Overwriting.");
                }
                context.IdRegistry[id] = go;
            }

            // Recurse children
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName == "GameObject")
                {
                    Build(child, go.transform, context);
                }
                // Non-GameObject tags are components, handled in Step 2
            }

            return go;
        }
    }
}