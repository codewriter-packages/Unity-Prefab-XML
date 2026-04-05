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
                context.LogWarning(
                    $"<GameObject> has no 'name' attribute. Using 'Unnamed'.", element);
                name = "Unnamed";
            }

            var go = new GameObject(name);
            context.ElementToGameObject[element] = go;

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
                    context.LogWarning(
                        $"Duplicate id '{id}' on <GameObject name=\"{name}\">. Overwriting.", element);
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