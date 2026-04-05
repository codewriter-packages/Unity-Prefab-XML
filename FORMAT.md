# PrefabXML Format

PrefabXML (`.prefabxml`) is a simplified XML format for describing Unity prefabs.

## Structure

```xml
<UnityPrefab>
    <GameObject name="...">
        <Component m_Property="value" />
        <GameObject name="...">
            ...child objects...
        </GameObject>
    </GameObject>
</UnityPrefab>
```

- `<UnityPrefab>` — root element, exactly one per file.
- `<GameObject name="..." active="true">` — a Unity GameObject. Nesting creates parent-child hierarchy. The `active` attribute is optional (default `"true"`), set to `"false"` to create an inactive object.
- Any other tag inside `<GameObject>` is a **component** (e.g. `<RectTransform>`, `<Image>`, `<Button>`). Components are processed in document order — `<RectTransform>` must be the first component if present.

## Property names

Property names match Unity's **serialized field names** exactly (the `m_` prefixed names used by SerializedObject). These are the same names you see in `.prefab` YAML files and in the Unity debug Inspector.

```xml
<RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="400, 250" />
<Image m_Color="#FF0000" m_RaycastTarget="true" />
<TextMeshProUGUI m_text="Hello" m_fontSize="24" m_fontColor="#FFFFFF" />
```

Nested properties use dot notation:

```xml
<Button m_Transition="ColorTint" m_Colors.m_NormalColor="#FFFFFF" m_Colors.m_PressedColor="#CCCCCC" />
```

## Object references

Use the `id` attribute on `<GameObject>` to give it a unique identifier. Then reference it by `#id` in component properties:

```xml
<GameObject name="Scroll Area">
    <ScrollRect m_Horizontal="false" m_Vertical="true"
        m_Content="#Content" m_Viewport="#Viewport" />

    <GameObject name="Viewport" id="Viewport">
        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
        <Image m_Color="#1E1E2E" />
        <Mask m_ShowMaskGraphic="false" />

        <GameObject name="Content" id="Content">
            <RectTransform m_AnchorMin="0, 1" m_AnchorMax="1, 1"
                m_Pivot="0.5, 1" m_SizeDelta="0, 0" />
            <VerticalLayoutGroup m_Spacing="8"
                m_ChildControlWidth="true" m_ChildControlHeight="true"
                m_ChildForceExpandWidth="true" m_ChildForceExpandHeight="false" />
        </GameObject>
    </GameObject>
</GameObject>
```

The `id` is optional — only add it when the object needs to be referenced from another component. The `id` value must be unique within the file. The importer automatically resolves the reference type (e.g. `m_FillRect` expects `RectTransform`, so it calls `GetComponent<RectTransform>()` on the referenced GameObject).

## Asset references

Reference assets (sprites, fonts, materials, etc.) by their project path:

```xml
<Image m_Sprite="Assets/Sprites/icon.png" />
<TextMeshProUGUI m_fontAsset="Assets/Fonts/Roboto SDF.asset" m_fontSize="24" m_fontColor="#FFFFFF" />
<Image m_Material="Assets/Materials/UIBlur.mat" />
```

The importer loads assets via `AssetDatabase.LoadAssetAtPath` and automatically tracks dependencies — the prefab reimports when referenced assets change.

## Setting component properties

### Short form (attributes)

For simple values, write properties as XML attributes directly on the component tag:

```xml
<Image m_Color="#FF0000" />
<RectTransform m_SizeDelta="200, 100" />
<TextMeshProUGUI m_text="Hello" m_fontSize="24" m_fontColor="#FFFFFF"
    m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
```

If a component has no properties, use a self-closing tag:

```xml
<Button />
```

### Long form (Field tag)

For arrays and lists, use `<Field>` child elements with `<Item>` entries:

```xml
<TMP_Dropdown m_Value="0">
    <Field name="m_Options.m_Options">
        <Item m_Text="Option A" />
        <Item m_Text="Option B" />
        <Item m_Text="Option C" />
    </Field>
</TMP_Dropdown>
```

## Property value types

Values are written as strings and parsed by `SerializedPropertyType`:

| Type | Example | Notes |
|------|---------|-------|
| Integer | `"100"` | |
| Float | `"0.5"` | |
| Boolean | `"true"`, `"false"` | |
| String | `"Hello World"` | |
| Color | `"#FF0000"`, `"#FF000080"` | hex RRGGBB or RRGGBBAA |
| Enum | `"MiddleCenter"`, `"Bold"` | enum member name (case-insensitive) |
| Vector2 | `"10, 20"` | |
| Vector3 | `"1, 2, 3"` | |
| Vector4 | `"1, 2, 3, 4"` | |
| ObjectReference | `"#id"` | reference to GameObject by id |

## Available components

Any Unity component can be used by its class name. For built-in Unity components from `UnityEngine` and `UnityEngine.UI` namespaces, the short class name is enough:

```xml
<Image m_Color="#FF0000" />
<Button />
```

Common third-party components like `TextMeshProUGUI`, `TMP_Dropdown` also work by short name. For custom components with namespace collisions, use the fully qualified name:

```xml
<TextMeshProUGUI m_text="Hello" m_fontSize="24" />
<MyGame.UI.HealthBar maxValue="100" currentValue="75" />
```

## Templates

See the `Templates/` folder for ready-to-use reference templates with all common properties:

- `Image.prefabxml` — Image with all m_ fields
- `RawImage.prefabxml` — RawImage
- `Text-TextMeshPro.prefabxml` — TextMeshProUGUI with alignment, wrapping, overflow
- `Button-TextMeshPro.prefabxml` — Button with ColorTint and TMP label
- `Toggle.prefabxml` — Toggle with track, checkmark, and label
- `Slider.prefabxml` — Slider with fill and handle
- `ScrollView.prefabxml` — ScrollRect with viewport, mask, and content
- `Dropdown.prefabxml` — TMP_Dropdown with template and options

## Samples

See the `Samples/` folder for complete working examples:

- `ConfirmDialog.prefabxml` — Confirmation dialog with overlay, header, message, and Cancel/Confirm buttons
