namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// An object or a component the designer file dropped.
    /// </summary>
    public sealed class RemovedElementHandler : IDesignerChangeWriter
    {
        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            change.TargetElement?.Remove();
        }
    }
}