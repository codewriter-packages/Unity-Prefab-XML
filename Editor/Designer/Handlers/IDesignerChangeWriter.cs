namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Writes a change into the document.
    ///
    /// <see cref="DesignerChangeHandlers.ApplyRegistry"/> lists the writers in the order a run uses
    /// them, so the order the document is edited in is the order of that array and nothing else.
    /// Values are written before objects and components are added, and removals come last.
    /// </summary>
    public interface IDesignerChangeWriter
    {
        /// <summary>
        /// Writes the change. Only ever called for a change the user selected and that carries no
        /// problem, so everything it needs was worked out while the change was collected.
        /// </summary>
        void Apply(DesignerChange change, DesignerChangeSet set);
    }
}