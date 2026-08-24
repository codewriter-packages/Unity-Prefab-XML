namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Why a change is shown in the table but never written. There are two kinds, and telling them
    /// apart is the point: a value the format leaves out knowingly is the applier doing its job,
    /// while a value it could not make sense of is a hole in the applier — the same silence, but
    /// only one of the two is worth looking at.
    /// </summary>
    public sealed class DesignerChangeProblem
    {
        /// <summary>The reason, as the table shows it.</summary>
        public readonly string Text;

        /// <summary>
        /// The format leaves the value out on purpose. False when the applier ran out of answers: a
        /// property it does not know, a type it cannot write, an object it cannot find.
        /// </summary>
        public readonly bool ByDesign;

        public DesignerChangeProblem(string text, bool byDesign)
        {
            Text = text;
            ByDesign = byDesign;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// The reasons a change is shown in the table but never written.
    /// </summary>
    public static class DesignerChangeProblems
    {
        // The format has an answer for these, and the answer is not to write them

        public static readonly DesignerChangeProblem Skipped =
            ByDesign("the format never writes this property");

        public static readonly DesignerChangeProblem GameObjectProperty =
            ByDesign("only the name and the active state of an object are written");

        public static readonly DesignerChangeProblem Derived =
            ByDesign("the value is derived from another property of the file");

        public static readonly DesignerChangeProblem Driven =
            ByDesign("a layout component computes this value");

        // Nothing here knew what to do with these

        public static readonly DesignerChangeProblem NoXmlObject =
            Unsupported("the object is not described by the XML");

        public static readonly DesignerChangeProblem NoXmlComponent =
            Unsupported("the component is not described by the XML");

        public static readonly DesignerChangeProblem NoVariant =
            Unsupported("the component is missing from the designer file");

        public static readonly DesignerChangeProblem NotAnAttribute =
            Unsupported("not a valid attribute name");

        public static readonly DesignerChangeProblem NoAttribute =
            Unsupported("no attribute of the format matches this property");

        public static readonly DesignerChangeProblem UnsupportedType =
            Unsupported("the format cannot write this value type");

        public static readonly DesignerChangeProblem ManagedReferences =
            Unsupported("arrays of managed references are not written");

        /// <summary>
        /// A claimer described a change nothing can write. Not something a file can cause — it means
        /// the claimer implements no <see cref="IDesignerChangeWriter"/>, or its writer is missing
        /// from <see cref="DesignerChangeHandlers.ApplyRegistry"/>.
        /// </summary>
        public static readonly DesignerChangeProblem NoWriter =
            Unsupported("nothing in the applier writes this kind of change");

        private static DesignerChangeProblem ByDesign(string text)
        {
            return new DesignerChangeProblem(text, byDesign: true);
        }

        private static DesignerChangeProblem Unsupported(string text)
        {
            return new DesignerChangeProblem(text, byDesign: false);
        }
    }
}