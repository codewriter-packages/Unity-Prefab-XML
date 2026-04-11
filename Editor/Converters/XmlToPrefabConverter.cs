using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML.Converters
{
    public static class XmlToPrefabConverter
    {
        [MenuItem("Assets/PrefabXML/Convert PrefabXML to Prefab", true)]
        private static bool ValidateConvert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (path.EndsWith(".prefabxml"))
                    return true;
            }
            return false;
        }

        [MenuItem("Assets/PrefabXML/Convert PrefabXML to Prefab")]
        private static void Convert()
        {
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".prefabxml")) continue;
                ConvertOne(path);
            }
        }

        private static void ConvertOne(string path)
        {
            var sourceGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (sourceGo == null)
            {
                Debug.LogError($"XmlToPrefab: Cannot load PrefabXML at '{path}'.");
                return;
            }

            var instance = Object.Instantiate(sourceGo);
            instance.name = sourceGo.name;

            try
            {
                var outputPath = Path.ChangeExtension(path, ".prefab");
                PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                Debug.Log($"XmlToPrefab: Converted '{path}' → '{outputPath}'");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}