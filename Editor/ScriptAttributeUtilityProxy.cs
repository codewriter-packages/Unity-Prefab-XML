using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnityPrefabXML
{
    internal static class ScriptAttributeUtilityProxy
    {
        private static readonly MethodProxy GetFieldInfoAndStaticTypeFromPropertyMethod;

        static ScriptAttributeUtilityProxy()
        {
            var unityEditorAssemblyTypes = typeof(UnityEditor.Editor).Assembly.GetTypes();

            GetFieldInfoAndStaticTypeFromPropertyMethod = new MethodProxy
            {
                methodInfo = unityEditorAssemblyTypes
                    .First(t => t.Name == "ScriptAttributeUtility")
                    .GetMethod("GetFieldInfoAndStaticTypeFromProperty", BindingFlags.Static | BindingFlags.NonPublic),
                parameters = new object[2],
            };
        }

        /// <summary>
        /// The enum a property holds, or null when the type cannot be resolved. Read from the
        /// static type of the property rather than from the field: inside an array the field is
        /// the List, and only the static type is the element itself.
        /// </summary>
        public static System.Type GetEnumType(SerializedProperty property)
        {
            var fieldInfo = GetFieldInfoAndStaticTypeFromProperty(property, out var type);

            if (type != null && type.IsEnum)
            {
                return type;
            }

            return fieldInfo != null && fieldInfo.FieldType.IsEnum ? fieldInfo.FieldType : null;
        }

        public static FieldInfo GetFieldInfoAndStaticTypeFromProperty(SerializedProperty property, out System.Type type)
        {
            var proxy = GetFieldInfoAndStaticTypeFromPropertyMethod;

            proxy.parameters[0] = property;
            proxy.parameters[1] = null;

            var result = proxy.methodInfo.Invoke(null, proxy.parameters);

            type = (System.Type) proxy.parameters[1];

            proxy.parameters[0] = null;
            proxy.parameters[1] = null;

            return (FieldInfo) result;
        }

        private class MethodProxy
        {
            public MethodInfo methodInfo;
            public object[] parameters;
        }
    }
}