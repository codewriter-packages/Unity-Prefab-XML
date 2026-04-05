using UnityEditor.AssetImporters;

namespace UnityPrefabXML
{
    [ScriptedImporter(1, "prefabxml")]
    public class PrefabXmlImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var buildContext = new PrefabXmlBuildContext(ctx);
            buildContext.Execute(ctx.assetPath);
        }
    }
}