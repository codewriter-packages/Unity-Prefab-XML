using System.Collections.Generic;
using UnityEditor;

namespace UnityPrefabXML.Designer
{
    /// <summary>
    /// Recognizes the property modifications of the designer file it can describe and turns them
    /// into changes of the XML.
    ///
    /// <see cref="DesignerChangeHandlers.ClaimRegistry"/> tries the claimers in order and the first
    /// one to claim a modification owns it, so a claimer for a single property or a single type goes
    /// in front of the general ones and nothing else has to change.
    ///
    /// A claimer that can also write its changes implements <see cref="IDesignerChangeWriter"/> as
    /// well. The ones that only explain why a change is left out do not.
    /// </summary>
    public interface IDesignerChangeClaimer
    {
        /// <summary>
        /// Whether this claimer owns the modification, and under which key. Modifications answering
        /// with the same key are folded into one change: the x and the y of a vector are one
        /// attribute of the file and one row of the table.
        /// </summary>
        bool TryClaim(DesignerChangeRequest request, out string key);

        /// <summary>
        /// Describes what the modifications claimed under one key add up to. The value is read from
        /// the property as it stands now, not from the modifications — only the property itself
        /// knows what the whole vector looks like after two of its components changed.
        /// </summary>
        DesignerChange Build(DesignerChangeRequest request, string key, List<PropertyModification> mods);
    }
}