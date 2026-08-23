using System.Collections.Generic;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The handlers a run of the applier is built from, kept in the two orders that matter:
    /// <see cref="ClaimRegistry"/> decides who describes a modification, <see cref="ApplyRegistry"/>
    /// decides in which order the document is edited. Both are open — a project that needs its own
    /// handler inserts it where it belongs and nothing else changes.
    /// </summary>
    public static class DesignerChangeHandlers
    {
        // Read off the property modifications of the designer file
        public static readonly GameObjectNameHandler GameObjectName = new GameObjectNameHandler();
        public static readonly GameObjectActiveHandler GameObjectActive = new GameObjectActiveHandler();
        public static readonly SkippedPropertyHandler SkippedProperty = new SkippedPropertyHandler();
        public static readonly GameObjectPropertyHandler GameObjectProperty = new GameObjectPropertyHandler();
        public static readonly UnmappedComponentHandler UnmappedComponent = new UnmappedComponentHandler();
        public static readonly TmpFontFeaturesHandler TmpFontFeatures = new TmpFontFeaturesHandler();
        public static readonly ArrayFieldHandler ArrayField = new ArrayFieldHandler();
        public static readonly AttributeValueHandler AttributeValue = new AttributeValueHandler();
        public static readonly UnknownPropertyHandler UnknownProperty = new UnknownPropertyHandler();

        // Read off the added and removed lists of the prefab instead
        public static readonly AddedComponentHandler AddedComponent = new AddedComponentHandler();
        public static readonly AddedGameObjectHandler AddedGameObject = new AddedGameObjectHandler();
        public static readonly RemovedElementHandler RemovedElement = new RemovedElementHandler();

        /// <summary>
        /// Tried in order, and the first claimer to take a modification owns it. A claimer for one
        /// property or one type belongs in front of the general ones; the last entry claims whatever
        /// is left, which is what keeps a property the format has no place for out of the file and
        /// in the table instead.
        /// </summary>
        public static readonly List<IDesignerChangeClaimer> ClaimRegistry = new List<IDesignerChangeClaimer>
        {
            // The name of an object is a skipped property on a component and the one thing the
            // format writes about the object itself, so both go in front of the skip list
            GameObjectName,
            GameObjectActive,
            SkippedProperty,
            GameObjectProperty,

            // Nothing can be written about a component the mapping does not cover, and writing to
            // the wrong element would be worse than saying so
            UnmappedComponent,

            TmpFontFeatures,
            ArrayField,
            AttributeValue,
            UnknownProperty,
        };

        /// <summary>
        /// The order the document is edited in: values first, then what the designer file added,
        /// then what it dropped. An element has to exist before anything is written into it, and
        /// removing one last keeps the rest of the run working on a document that still describes
        /// every object the changes were collected from.
        /// </summary>
        public static readonly List<IDesignerChangeWriter> ApplyRegistry = new List<IDesignerChangeWriter>
        {
            AttributeValue,
            ArrayField,
            GameObjectName,
            GameObjectActive,
            AddedComponent,
            AddedGameObject,
            RemovedElement,
        };

        /// <summary>
        /// The claimer that owns the modification, and the key its change is folded under. Never
        /// null: the last claimer of the registry takes everything.
        /// </summary>
        public static IDesignerChangeClaimer Claim(DesignerChangeRequest request, out string key)
        {
            foreach (var claimer in ClaimRegistry)
            {
                if (claimer.TryClaim(request, out key))
                {
                    return claimer;
                }
            }

            key = request.PropertyPath;
            return UnknownProperty;
        }
    }
}