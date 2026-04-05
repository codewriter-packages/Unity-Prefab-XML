# Unity Prefab XML

_XML to Prefab importer for Unity_

A simplified XML format designed for **LLM-driven prefab generation**. Instead of asking AI to produce complex Unity `.prefab` YAML files, you describe your prefab in a compact XML — and Unity imports it as a real prefab. Works with any GameObject hierarchy, including UI.

### 🤔 Why not generate .prefab YAML directly?

Unity prefab files are verbose serialized YAML with internal references, fileIDs, GUIDs, and strict formatting rules. Even small mistakes break the file. This makes them impractical for LLM generation:

- ❌ A simple button is ~100 lines of YAML with cryptic numeric IDs
- ❌ Components reference each other via `fileID` — the model has to track and increment IDs correctly
- ❌ GUIDs for scripts, sprites, fonts must be looked up from `.meta` files
- ❌ Formatting errors or missing fields silently corrupt the prefab

**PrefabXML** solves this. Compare the same button:

<table>
<tr><th>Unity .prefab YAML (~80 lines)</th><th>PrefabXML (8 lines)</th></tr>
<tr>
<td>

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &6578955084326400
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4818492398498432}
  - component: {fileID: 8581965827370498}
  - component: {fileID: 3276583080156498}
  m_Layer: 5
  m_Name: MyButton
  m_TagString: Untagged
  ...
  # ~60 more lines of fileIDs,
  # GUIDs, serialized properties
```

</td>
<td>

```xml
<UnityPrefab>
  <GameObject name="MyButton">
    <RectTransform
        anchor="middle-center"
        width="200" height="50" />
    <Image color="#4CAF50" />
    <Button />
    <GameObject name="Label">
      <Text text="Click me"
            fontSize="20"
            color="#FFFFFF"
            alignment="MiddleCenter" />
    </GameObject>
  </GameObject>
</UnityPrefab>
```

</td>
</tr>
</table>

Save as `.prefabxml` — Unity imports it as a prefab automatically.

### ✨ Features

- 🧩 **Any component** — use any Unity component by class name, including custom scripts with full namespace
- ⚓ **Anchor presets** — `stretch`, `middle-center`, `top-left` instead of raw anchorMin/anchorMax numbers
- 🔗 **Object references** — link components to other GameObjects via `#id`
- 🎨 **Asset slots** — expose sprites, fonts, materials to the Inspector with `{variableName}`
- 🌳 **Nested hierarchies** — parent-child relationships through XML nesting
- 🔢 **Value types** — Color (`#FF0000`), Vector2/3/4, Enum, RectOffset, and more

### 🚀 Use cases

- 🤖 **AI-generated prefabs** — ask an LLM to create UI or scene objects from a text description
- ⚡ **Rapid prototyping** — sketch out a prefab in a text editor without opening the Unity Inspector
- 📝 **Diff-friendly UI** — review prefab changes in pull requests as readable XML, not cryptic YAML

### 📖 Format specification

See [FORMAT.md](./FORMAT.md) for the full format reference with examples.

## 📦 How to Install

Library distributed as git package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))
<br>Git URL: `https://github.com/codewriter-packages/Unity-Prefab-XML.git`

## 📄 License

Unity Prefab XML is [MIT licensed](./LICENSE.md).
