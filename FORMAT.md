# PrefabXML Format

PrefabXML (`.prefabxml`) is a simplified XML format for describing Unity UI prefabs.

## Structure

```xml
<UnityPrefab>
    <GameObject name="...">
        <Component attr="value" />
        <GameObject name="...">
            ...child objects...
        </GameObject>
    </GameObject>
</UnityPrefab>
```

- `<UnityPrefab>` — root element, exactly one per file.
- `<GameObject name="..." active="true">` — a Unity GameObject. Nesting creates parent-child hierarchy. The `active` attribute is optional (default `"true"`), set to `"false"` to create an inactive object.
- Any other tag inside `<GameObject>` is a **component** (e.g. `<RectTransform>`, `<Image>`, `<Text>`, `<Button>`).

## Object references

Use the `id` attribute on `<GameObject>` to give it a unique identifier. Then reference it by `#id` in component properties:

```xml
<GameObject name="Scroll Area">
    <ScrollRect content="#Content" viewport="#Viewport" />

    <GameObject name="Viewport" id="Viewport">
        <Image />
        <Mask showMaskGraphic="false" />

        <GameObject name="Content" id="Content">
            <RectTransform width="400" height="1000" />
            <VerticalLayoutGroup spacing="8" padding="10" />
        </GameObject>
    </GameObject>
</GameObject>
```

The `id` is optional — only add it when the object needs to be referenced from another component. The `id` value must be unique within the file.

## Exposed properties (asset references)

Use `{variableName}` as a property value to expose it in the ScriptedImporter inspector. This allows assigning assets (sprites, fonts, materials, etc.) via the Unity Editor:

```xml
<Image sprite="{iconSprite}" />
<Text font="{headerFont}" fontSize="24" color="#FFFFFF" />
<Image sprite="{backgroundImage}" material="{customMaterial}" />
```

Regular values (like `fontSize="24"`) are parsed from XML. Values wrapped in `{}` become editable fields in the importer inspector. The field type is inferred automatically from the component property (e.g. `sprite` → `Sprite`, `font` → `Font`, `material` → `Material`).

## Setting component properties

### Short form (attributes)

For simple values, write properties as XML attributes directly on the component tag:

```xml
<Image color="#FF0000" />
<Text text="Hello" fontSize="24" color="#FFFFFF" alignment="MiddleCenter" />
<RectTransform width="200" height="100" />
```

If a component has no properties, use a self-closing tag:

```xml
<Button />
```

### Long form (Field tag)

For complex values such as arrays, lists, or multi-line content, use `<Field>` child elements:

```xml
<Dropdown>
    <Field name="options">
        <Item value="Option A" />
        <Item value="Option B" />
        <Item value="Option C" />
    </Field>
</Dropdown>
```

Both forms can be mixed within one component:

```xml
<Dropdown captionText="Choose...">
    <Field name="options">
        <Item value="Option A" />
        <Item value="Option B" />
    </Field>
</Dropdown>
```

## Property value types

Values are written as strings and parsed by type:

| Type | Example | Notes |
|------|---------|-------|
| int / float | `"100"`, `"0.5"` | |
| bool | `"true"`, `"false"` | |
| string | `"Hello World"` | |
| Color | `"#FF0000"`, `"#FF000080"` | hex RRGGBB or RRGGBBAA |
| Enum | `"MiddleCenter"`, `"Bold"` | enum member name |
| Vector2 | `"10, 20"` | |
| Vector3 | `"1, 2, 3"` | |
| Vector4 | `"1, 2, 3, 4"` | |
| RectOffset | `"16"`, `"16, 16, 8, 8"` | 1 value = all sides; 4 values = left, right, top, bottom (matches `new RectOffset(l, r, t, b)`) |

## Available components

Any Unity component can be used by its class name. For built-in Unity components from `UnityEngine` and `UnityEngine.UI` namespaces, the short class name is enough:

```xml
<Image color="#FF0000" />
<Button />
```

For custom or third-party components, use the fully qualified name (namespace + class):

```xml
<MyGame.UI.HealthBar maxValue="100" currentValue="75" />
<TMPro.TextMeshProUGUI text="Hello" fontSize="24" />
```

### RectTransform anchor presets

The `anchor` attribute on `RectTransform` provides a shorthand for common `anchorMin` / `anchorMax` combinations:

| Preset | anchorMin | anchorMax | Description |
|--------|-----------|-----------|-------------|
| `top-left` | `0, 1` | `0, 1` | top-left corner |
| `top-center` | `0.5, 1` | `0.5, 1` | top edge, centered |
| `top-right` | `1, 1` | `1, 1` | top-right corner |
| `middle-left` | `0, 0.5` | `0, 0.5` | left edge, centered |
| `middle-center` | `0.5, 0.5` | `0.5, 0.5` | center of parent |
| `middle-right` | `1, 0.5` | `1, 0.5` | right edge, centered |
| `bottom-left` | `0, 0` | `0, 0` | bottom-left corner |
| `bottom-center` | `0.5, 0` | `0.5, 0` | bottom edge, centered |
| `bottom-right` | `1, 0` | `1, 0` | bottom-right corner |
| `stretch-horizontal` | `0, 0.5` | `1, 0.5` | stretch to parent width |
| `stretch-vertical` | `0.5, 0` | `0.5, 1` | stretch to parent height |
| `stretch` | `0, 0` | `1, 1` | fill entire parent |

Examples:

```xml
<!-- centered panel with fixed size -->
<RectTransform anchor="middle-center" width="400" height="300" />

<!-- top bar stretching full width -->
<RectTransform anchor="stretch-horizontal" height="60" />

<!-- fill parent completely -->
<RectTransform anchor="stretch" />
```

You can still use `anchorMin` and `anchorMax` directly for non-standard values:

```xml
<RectTransform anchorMin="0.1, 0.1" anchorMax="0.9, 0.9" />
```

### Common UI components

| Component | Common properties |
|-----------|-------------------|
| `RectTransform` | `width`, `height`, `anchor`, `pivot`, `anchoredPosition`, `anchorMin`, `anchorMax` |
| `Image` | `color`, `sprite`, `type`, `raycastTarget` |
| `Text` | `text`, `fontSize`, `fontStyle`, `color`, `alignment` |
| `Button` | `interactable`, `transition` |
| `Toggle` | `isOn`, `interactable` |
| `InputField` | `text`, `placeholder`, `characterLimit` |
| `Slider` | `minValue`, `maxValue`, `value`, `wholeNumbers` |
| `ScrollRect` | `horizontal`, `vertical` |
| `Dropdown` | `captionText`, `options` (use Field form) |
| `HorizontalLayoutGroup` | `spacing`, `padding`, `childAlignment`, `childForceExpandWidth`, `childForceExpandHeight` |
| `VerticalLayoutGroup` | `spacing`, `padding`, `childAlignment`, `childForceExpandWidth`, `childForceExpandHeight` |
| `GridLayoutGroup` | `cellSize`, `spacing`, `constraint`, `constraintCount` |
| `LayoutElement` | `minWidth`, `minHeight`, `preferredWidth`, `preferredHeight`, `flexibleWidth`, `flexibleHeight` |
| `ContentSizeFitter` | `horizontalFit`, `verticalFit` |
| `CanvasGroup` | `alpha`, `interactable`, `blocksRaycasts` |
| `Mask` | `showMaskGraphic` |

## Full example

Confirmation dialog with a title, message, and Yes/No buttons:

```xml
<UnityPrefab>
    <GameObject name="ConfirmDialog">
        <RectTransform width="400" height="250" />
        <Image color="#222222" />
        <VerticalLayoutGroup padding="16" spacing="12" />

        <GameObject name="Title">
            <RectTransform height="40" />
            <Text text="Подтверждение" fontSize="24" fontStyle="Bold"
                  color="#FFFFFF" alignment="MiddleCenter" />
        </GameObject>

        <GameObject name="Message">
            <RectTransform height="80" />
            <Text text="Вы уверены, что хотите выполнить это действие?"
                  fontSize="18" color="#CCCCCC" alignment="MiddleCenter" />
        </GameObject>

        <GameObject name="Buttons">
            <RectTransform height="50" />
            <HorizontalLayoutGroup spacing="20" childAlignment="MiddleCenter" />

            <GameObject name="ButtonYes">
                <RectTransform width="120" height="40" />
                <Image color="#4CAF50" />
                <Button />
                <GameObject name="Label">
                    <Text text="Да" fontSize="20" color="#FFFFFF" alignment="MiddleCenter" />
                </GameObject>
            </GameObject>

            <GameObject name="ButtonNo">
                <RectTransform width="120" height="40" />
                <Image color="#F44336" />
                <Button />
                <GameObject name="Label">
                    <Text text="Нет" fontSize="20" color="#FFFFFF" alignment="MiddleCenter" />
                </GameObject>
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```