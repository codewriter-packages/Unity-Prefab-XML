# Unity Prefab XML [![Github license](https://img.shields.io/github/license/codewriter-packages/Unity-Prefab-XML.svg?style=flat-square)](#) [![Unity 6000.0](https://img.shields.io/badge/Unity-6000.0+-2296F3.svg?style=flat-square)](#) ![GitHub package.json version](https://img.shields.io/github/package-json/v/codewriter-packages/Unity-Prefab-XML?style=flat-square) 

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
        m_SizeDelta="200, 50" />
    <Image m_Color="#4CAF50" />
    <Button />
    <GameObject name="Label">
      <RectTransform
          m_AnchorMin="0, 0"
          m_AnchorMax="1, 1"
          m_SizeDelta="0, 0" />
      <TextMeshProUGUI
          m_text="Click me"
          m_fontSize="20"
          m_fontColor="#FFFFFF"
          m_HorizontalAlignment="Center"
          m_VerticalAlignment="Middle" />
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
- ⚓ **Standard Unity field names** — uses `m_` serialized property names, 1:1 mapping to C#
- 🔗 **Object references** — link components to other GameObjects via `#id`
- 🎨 **Asset references** — reference sprites, fonts, materials by project path or via `{name}` bindings assigned in the Inspector
- 🌳 **Nested hierarchies** — parent-child relationships through XML nesting
- 🔢 **Value types** — Color (`#FF0000`), Vector2/3/4, Enum, RectOffset, and more
- 🎮 **Custom MonoBehaviours** — add your own scripts and wire `[SerializeField]` fields via `#id` references directly in XML (see `Samples/Counter`)

### 🚀 Use cases

- 🤖 **AI-generated prefabs** — ask an LLM to create UI or scene objects from a text description
- ⚡ **Rapid prototyping** — sketch out a prefab in a text editor without opening the Unity Inspector
- 📝 **Diff-friendly UI** — review prefab changes in pull requests as readable XML, not cryptic YAML

### 🤖 Getting started with LLM

1. Add [FORMAT.md](./FORMAT.md), [GUIDE.md](./GUIDE.md) and relevant files from `Templates/` to the LLM context
2. Describe the UI you want: *"Create a settings panel with volume slider, music toggle, and resolution dropdown"*
3. Save the generated XML as `.prefabxml` in your Unity project — it imports as a prefab automatically
4. Drag the prefab onto a Canvas

**Tips:**
- Include your existing `.prefabxml` files in the prompt so the LLM matches your project's style and uses correct asset paths
- Ask the LLM to generate both a C# MonoBehaviour and a `.prefabxml` in one go — the script's `[SerializeField]` fields can be wired to UI elements via `#id` references (see `Samples/Counter` for an example)

### 📖 Format & Guide

- [FORMAT.md](./FORMAT.md) — format specification
- [GUIDE.md](./GUIDE.md) — common mistakes and best practices for LLM generation

### 📂 Samples & Templates

- `Templates/` — reference templates for common UI components (Button, Slider, Toggle, Dropdown, etc.) with all field names
- `Samples/` — complete working examples

## 🎨 Syntax Highlighting

`.prefabxml` files are plain XML, so you can enable syntax highlighting in your IDE by associating the extension with the XML file type:

### VS Code

Add to your `.vscode/settings.json`:

```json
{
    "files.associations": {
        "*.prefabxml": "xml"
    }
}
```

### JetBrains Rider

Go to **Settings → Editor → File Types**, find **XML** in the list, and add `*.prefabxml` to the registered patterns.

## 📦 How to Install

Library distributed as git package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))
<br>Git URL: `https://github.com/codewriter-packages/Unity-Prefab-XML.git`

## 📄 License

Unity Prefab XML is [MIT licensed](./LICENSE.md).
