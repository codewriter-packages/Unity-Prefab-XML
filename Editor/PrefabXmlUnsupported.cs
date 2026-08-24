using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityPrefabXML
{
    /// <summary>
    /// Keeps a component type out of what the designer file writes:
    ///
    /// <code>
    /// [assembly: PrefabXmlUnsupported(typeof(AnimationSequencerController),
    ///     "holds managed references the designer cannot write back")]
    /// </code>
    ///
    /// Adding the component in the designer file lands in the unsupported list with the reason its
    /// author gave, instead of an attribute of the XML that comes back as something else. The reason
    /// is the point of the attribute: whoever meets the component next reads what its author knew,
    /// rather than finding it out from a prefab that comes out wrong.
    ///
    /// The importer is not held to any of this. A file that already names the component was written
    /// by someone who meant it, and nothing takes it back out behind their back — the attribute says
    /// what this package should stop writing, not what a project is allowed to have.
    ///
    /// Declared per assembly and read from every assembly that references this one, so a project
    /// puts it wherever it keeps its editor code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class PrefabXmlUnsupportedAttribute : Attribute
    {
        public Type ComponentType { get; }

        /// <summary>Why the type is out, as the importer and the designer table report it.</summary>
        public string Reason { get; }

        public PrefabXmlUnsupportedAttribute(Type componentType, string reason)
        {
            ComponentType = componentType;
            Reason = reason;
        }
    }

    /// <summary>
    /// The component types the project keeps out of what the designer writes, read off the assembly
    /// attributes once per domain. Everything that asks about one comes through here, so no two
    /// callers can disagree about what counts as unsupported.
    /// </summary>
    public static class UnsupportedComponents
    {
        /// <summary>What the attributes declare, null while it was never read.</summary>
        private static Dictionary<Type, string> _declared;

        /// <summary>
        /// The answers already worked out, the supported ones included. Every collect pass asks
        /// about every component the designer file added, and the walk below is a loop over the
        /// declarations.
        /// </summary>
        private static readonly Dictionary<Type, string> Resolved = new Dictionary<Type, string>();

        /// <summary>
        /// The reason the type is kept out, or null while nothing keeps it out. A type derived from
        /// a declared one is out as well: it is that component with more on top.
        /// </summary>
        public static string GetReason(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (Resolved.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var reason = Lookup(type);
            Resolved[type] = reason;
            return reason;
        }

        public static bool IsUnsupported(Type type, out string reason)
        {
            reason = GetReason(type);
            return reason != null;
        }

        /// <summary>The first component of the object or of anything below it that is out.</summary>
        public static bool IsUnsupportedSubtree(GameObject go, out Component component, out string reason)
        {
            foreach (var candidate in go.GetComponentsInChildren<Component>(true))
            {
                // A script the project lost leaves a hole in the list, and nothing can be said
                // about a type that is not there
                if (candidate == null)
                {
                    continue;
                }

                if (IsUnsupported(candidate.GetType(), out reason))
                {
                    component = candidate;
                    return true;
                }
            }

            component = null;
            reason = null;
            return false;
        }

        private static string Lookup(Type type)
        {
            EnsureDeclared();

            if (_declared.TryGetValue(type, out var exact))
            {
                return exact;
            }

            foreach (var declaration in _declared)
            {
                if (declaration.Key.IsAssignableFrom(type))
                {
                    return declaration.Value;
                }
            }

            return null;
        }

        [InitializeOnLoadMethod]
        private static void ResetCache()
        {
            _declared = null;
            Resolved.Clear();
        }

        private static void EnsureDeclared()
        {
            if (_declared != null)
            {
                return;
            }

            _declared = new Dictionary<Type, string>();

            var self = typeof(PrefabXmlUnsupportedAttribute).Assembly;
            var selfName = self.GetName().Name;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Only an assembly that can name the attribute can carry it, and the reference is
                // the cheap way to ask — reading the attributes of every assembly of the domain
                // means resolving the types behind all of them
                if (assembly != self &&
                    assembly.GetReferencedAssemblies().All(name => name.Name != selfName))
                {
                    continue;
                }

                foreach (PrefabXmlUnsupportedAttribute declaration in
                         assembly.GetCustomAttributes(typeof(PrefabXmlUnsupportedAttribute), false))
                {
                    Declare(assembly, declaration);
                }
            }
        }

        private static void Declare(Assembly assembly, PrefabXmlUnsupportedAttribute declaration)
        {
            var type = declaration.ComponentType;

            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogWarning(
                    $"PrefabXml: [assembly: PrefabXmlUnsupported] in '{assembly.GetName().Name}' names " +
                    $"'{type?.FullName ?? "null"}', which is not a Component. The declaration is ignored.");
                return;
            }

            // Two assemblies naming the same type do not disagree about anything worth reporting:
            // both say it is out, and either reason answers the question its reader has
            if (!_declared.ContainsKey(type))
            {
                _declared[type] = declaration.Reason;
            }
        }
    }
}