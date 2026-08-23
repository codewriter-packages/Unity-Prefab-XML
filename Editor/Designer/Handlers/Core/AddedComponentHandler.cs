using System.Linq;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// A component the designer file added, written behind the components the object already has and
    /// in front of its child objects.
    /// </summary>
    public sealed class AddedComponentHandler : IDesignerChangeWriter
    {
        public void Apply(DesignerChange change, DesignerChangeSet set)
        {
            if (change.PayloadElement == null)
            {
                return;
            }

            var lastComp = change.TargetElement.Elements().LastOrDefault(PrefabXmlUtils.IsComponentElement);
            if (lastComp != null)
            {
                PrefabXmlUtils.AddAfter(lastComp, change.PayloadElement);
            }
            else
            {
                PrefabXmlUtils.AddFirstChild(change.TargetElement, change.PayloadElement);
            }
        }
    }
}