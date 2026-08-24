using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Designer
{
    public enum DesignerChangeKind
    {
        Property,
        ArrayField,
        ManagedReference,
        GameObjectName,
        GameObjectActive,
        AddedComponent,
        AddedGameObject,
        RemovedComponent,
        RemovedGameObject,
        ChildOrder,
    }

    /// <summary>
    /// One change of the designer file, described the way it will land in the XML. Everything is
    /// worked out before a single element is written, so the user sees what a run of the applier
    /// would do and can leave parts of it out.
    /// </summary>
    public sealed class DesignerChange
    {
        public DesignerChangeKind Kind;

        /// <summary>The object the change sits on, as a path below the root: "AccountCard/Nickname".</summary>
        public string ObjectPath = "";

        /// <summary>Component type name, null for a change of the GameObject itself.</summary>
        public string ComponentType;

        /// <summary>Index among the components of the same type on that object.</summary>
        public int ComponentIndex;

        /// <summary>Serialized property path, or the name of a structural change.</summary>
        public string PropertyPath = "";

        /// <summary>What to show in the property column.</summary>
        public string Label = "";

        /// <summary>The value that will be written. Null means the attribute is removed.</summary>
        public string NewValue;

        /// <summary>What the file says right now, shown to explain what the change replaces.</summary>
        public string OldValue;

        /// <summary>Null while the change can be applied, otherwise the reason it cannot.</summary>
        public DesignerChangeProblem Problem;

        /// <summary>
        /// The file already says exactly this. Unity keeps recording such overrides, and applying
        /// them writes nothing — they are only worth reverting off the designer file.
        /// </summary>
        public bool Redundant;

        public bool Selected;

        /// <summary>Raw modifications folded into this row. Reverting the row reverts all of them.</summary>
        public readonly List<PropertyModification> Sources = new List<PropertyModification>();

        /// <summary>The element the change writes to, or the parent for structural changes.</summary>
        public XElement TargetElement;

        /// <summary>The element to insert: a rebuilt Field, an added component or an added object.</summary>
        public XElement PayloadElement;

        /// <summary>The order the children of <see cref="TargetElement"/> are moved into.</summary>
        public List<XElement> DesiredChildren;

        /// <summary>
        /// The designer object an added element was built from. The element it produces has to be
        /// registered under it, or the reorder pass cannot place the object.
        /// </summary>
        public Transform VariantTransform;

        /// <summary>
        /// Puts the change into the document. Null for a change nothing writes: one the format
        /// leaves out, and the order of the children of an object, which is rewritten by a pass over
        /// the whole tree instead — see DesignerFileManager.ApplyChildOrder.
        /// </summary>
        public IDesignerChangeWriter Writer;

        public bool IsApplicable => Problem == null;

        /// <summary>
        /// Whether the change can be dropped off the designer file on its own. The revert pass takes
        /// overrides away — an added object by its address, a value by the modification behind it —
        /// so a change that names neither is out of its reach. That is every removal: clearing one
        /// means putting the object back, which is the opposite of what the pass does.
        /// </summary>
        public bool CanRevert =>
            Kind == DesignerChangeKind.AddedGameObject ||
            Kind == DesignerChangeKind.AddedComponent ||
            Sources.Count > 0;

        /// <summary>Shown in the table, and the only thing the user acts on.</summary>
        public bool IsVisible => Problem == null && !Redundant;

        /// <summary>
        /// Identity that survives a recollect, so the table can remember what the user unchecked
        /// without holding on to a stale change list.
        /// </summary>
        public string Key => $"{Kind}|{ObjectPath}|{ComponentType}#{ComponentIndex}|{PropertyPath}";

        public string ObjectLabel => string.IsNullOrEmpty(ObjectPath) ? "<root>" : ObjectPath;

        /// <summary>
        /// The place of a component among the ones of the same type on its object. Together with the
        /// path of the object it names the component in any copy of the prefab.
        /// </summary>
        public static int IndexOfComponent(Component comp)
        {
            var typeName = comp.GetType().Name;
            var index = 0;

            foreach (var other in comp.gameObject.GetComponents<Component>())
            {
                if (other == comp)
                {
                    return index;
                }

                if (other != null && other.GetType().Name == typeName)
                {
                    index++;
                }
            }

            return index;
        }

        public static string GetPath(Transform tf, Transform root)
        {
            if (tf == root || tf == null)
            {
                return "";
            }

            var path = tf.name;
            for (var parent = tf.parent; parent != null && parent != root; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}