namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// The reasons a change is shown in the table but never written.
    /// </summary>
    public static class DesignerChangeProblems
    {
        public const string NoXmlObject = "the object is not described by the XML";
        public const string NoXmlComponent = "the component is not described by the XML";
        public const string NoVariant = "the component is missing from the designer file";
        public const string NotAnAttribute = "not a valid attribute name";
        public const string NoAttribute = "no attribute of the format matches this property";
        public const string UnsupportedType = "the format cannot write this value type";
        public const string ManagedReferences = "arrays of managed references are not written";
        public const string GameObjectProperty = "only the name and the active state of an object are written";
        public const string Skipped = "the format never writes this property";
        public const string Derived = "the value is derived from another property of the file";

        /// <summary>
        /// A claimer described a change nothing can write. Not something a file can cause — it means
        /// the claimer implements no <see cref="IDesignerChangeWriter"/>, or its writer is missing
        /// from <see cref="DesignerChangeHandlers.ApplyRegistry"/>.
        /// </summary>
        public const string NoWriter = "nothing in the applier writes this kind of change";
    }
}