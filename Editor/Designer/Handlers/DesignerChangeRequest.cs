using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Everything a handler needs to decide about one modification: the object it sits on, the
    /// element of the file that describes it, and the property it reads the value from.
    /// </summary>
    public sealed class DesignerChangeRequest
    {
        public DesignerChangeSet Set;

        /// <summary>The modification being classified.</summary>
        public PropertyModification Modification;

        /// <summary>Set when the modification targets the GameObject itself.</summary>
        public GameObject TargetGameObject;

        /// <summary>Reads the values the file is written from. Null for a GameObject target.</summary>
        public SerializedObject VariantObject;

        /// <summary>The element of the XML the change is written to, null when there is none.</summary>
        public XElement Element;

        public string ObjectPath;
        public string ComponentType;
        public int ComponentIndex;

        public string PropertyPath => Modification.propertyPath;

        public bool IsGameObject => TargetGameObject != null;

        public DesignerChange NewChange(DesignerChangeKind kind, string propertyPath)
        {
            return new DesignerChange
            {
                Kind = kind,
                ObjectPath = ObjectPath,
                ComponentType = ComponentType,
                ComponentIndex = ComponentIndex,
                PropertyPath = propertyPath,
                Label = propertyPath,
                TargetElement = Element,
            };
        }

        /// <summary>The Field element of an array, or null while the file has none.</summary>
        public XElement FindField(string arrayPath)
        {
            return Element?.Elements("Field")
                .FirstOrDefault(el => el.Attribute("name")?.Value == arrayPath);
        }
    }
}