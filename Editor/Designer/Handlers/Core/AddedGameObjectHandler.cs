namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// An object the designer file added, appended to its parent. The reorder pass moves it into the
    /// place it has in the designer file, which is why the element is registered under the object.
    /// </summary>
    public sealed class AddedGameObjectHandler : IDesignerChangeWriter
    {
        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            if (change.PayloadElement == null)
            {
                return;
            }

            PrefabXmlUtils.AddChild(change.TargetElement, change.PayloadElement);

            if (change.VariantTransform != null)
            {
                set.Context.VariantToXml[change.VariantTransform.GetInstanceID()] = change.PayloadElement;
            }
        }
    }
}