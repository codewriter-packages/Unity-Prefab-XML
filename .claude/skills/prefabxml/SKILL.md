---
name: prefabxml
description: Generate Unity UI prefabs in PrefabXML format (.prefabxml). Use when you need to create a UI element, screen, dialog, panel, or any prefab for Unity from a text description.
argument-hint: [UI description] [path/to/file.prefabxml]
allowed-tools: Read, Write, Edit, Glob, Grep
user-invocable: true
---

# PrefabXML Generator

You are a Unity UI prefab generator using the PrefabXML format.

## Input

`$ARGUMENTS` — description of the desired UI and optionally a file path.

If no path is provided, ask where to save or suggest a reasonable path in the current project.

## Workflow

1. Read the format specification: [FORMAT.md](FORMAT.md) — in this skill's directory
2. Read the common mistakes guide: [GUIDE.md](GUIDE.md) — in this skill's directory
3. Read the templates: [TEMPLATES.md](TEMPLATES.md) — reference examples for all UI components
4. If the project already has `.prefabxml` files, study their style via Glob/Read to stay consistent
5. Generate the `.prefabxml` file
6. If a MonoBehaviour controller is needed, generate the C# script alongside it

## PrefabXML Generation Rules

### File structure
- Root element: `<UnityPrefab>`
- Exactly one root `<GameObject>` inside
- Components are XML tags inside `<GameObject>` (not `<GameObject>` or `<Field>`)
- Nested `<GameObject>` elements create parent-child hierarchy

### Property names
- Use ONLY Unity serialized field names (m_ prefixed for built-in components)
- Do NOT invent shorthand names (width, color, text — WRONG)
- Nested properties via dot notation: `m_Colors.m_NormalColor`

### TextMeshPro — lowercase after m_
- `m_text` (NOT m_Text)
- `m_fontSize` (NOT m_FontSize)
- `m_fontColor` (NOT m_Color)
- `m_fontStyle` — `"Normal"`, `"Bold"`, `"Italic"`, `"Bold, Italic"` (flags enum, comma-separated)
- `m_HorizontalAlignment` — `"Left"`, `"Center"`, `"Right"`, `"Justified"`
- `m_VerticalAlignment` — `"Top"`, `"Middle"`, `"Bottom"`
- `m_TextWrappingMode` — `"NoWrap"`, `"Normal"`
- `m_RaycastTarget` — `"false"` for non-interactive text

### Critical rules
- **RectTransform first**: must always be the first component in `<GameObject>`
- **One Graphic per GO**: Image, RawImage, TextMeshProUGUI — only one per object. Text over background = child GO
- **Selectable needs Image**: Button, Toggle, Slider, Dropdown — require `<Image>` BEFORE them
- **Slider needs m_FillRect**: bare `<Slider />` will break
- **Toggle needs structure**: Background + Checkmark (see Toggle template in TEMPLATES.md)
- **id on GameObject**: `id` goes on `<GameObject>`, NOT on component
- **Enum — names, not numbers**: `m_fontStyle="Bold"`, not `"1"`. For flags: `m_fontStyle="Bold, Italic"`
- **LayoutGroup needs flags**: `m_ChildControlWidth`, `m_ChildControlHeight`

### Property value types
| Type | Format | Example |
|------|--------|---------|
| Color | hex RRGGBB or RRGGBBAA | `"#FF0000"`, `"#FF000080"` |
| Vector2 | `"x, y"` | `"200, 50"` |
| Vector3 | `"x, y, z"` | `"1, 2, 3"` |
| Vector2Int | `"x, y"` | `"10, 20"` |
| Vector3Int | `"x, y, z"` | `"1, 2, 3"` |
| Quaternion | `"x, y, z, w"` | `"0, 0, 0, 1"` |
| Rect | `"x, y, w, h"` | `"0, 0, 100, 50"` |
| Bounds | `"cx, cy, cz, sx, sy, sz"` | `"0, 0, 0, 1, 1, 1"` |
| LayerMask | integer bitmask | `"256"` |
| Boolean | `"true"` / `"false"` | |
| Enum | value name | `"Center"`, `"Bold"` |
| Flags Enum | comma-separated names | `"Bold, Italic"` |
| Object ref | `"#id"` | reference to GO by id |
| Asset ref | project path | `"Assets/Sprites/icon.png"` |

### Arrays — via Field/Item
```xml
<TMP_Dropdown m_Value="0">
    <Field name="m_Options.m_Options">
        <Item m_Text="Option A" />
        <Item m_Text="Option B" />
    </Field>
</TMP_Dropdown>
```

### Padding on LayoutGroup — dot notation
```xml
<VerticalLayoutGroup m_Padding.m_Left="16" m_Padding.m_Right="16"
    m_Padding.m_Top="12" m_Padding.m_Bottom="12" />
```

## Style

- Set ONLY properties that differ from defaults
- Keep attributes minimal
- Stretch layout: `m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0"`
- Centered element: just `m_SizeDelta="200, 80"` is enough

## Output

Show the user the generated XML and save it to a file. Briefly describe the structure.
