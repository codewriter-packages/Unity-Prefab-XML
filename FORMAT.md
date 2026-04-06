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

To reference a specific component type on a GameObject, use `#id/ComponentType`:

```xml
<MyComponent listener="#MyButton/ButtonClickBinder" />
```

This calls `GetComponent("ButtonClickBinder")` on the GameObject with `id="MyButton"`. Useful when a GameObject has multiple components of compatible types and you need to reference a specific one.

## Asset references

Reference assets (sprites, fonts, materials, etc.) by their project path:

```xml
<Image m_Sprite="Assets/Sprites/icon.png" />
<TextMeshProUGUI m_fontAsset="Assets/Fonts/Roboto SDF.asset" m_fontSize="24" m_fontColor="#FFFFFF" />
<Image m_Material="Assets/Materials/UIBlur.mat" />
```

The importer loads assets via `AssetDatabase.LoadAssetAtPath` and automatically tracks dependencies — the prefab reimports when referenced assets change.

## Asset bindings

Use `{name}` syntax to declare an asset binding — a named slot that can be assigned in the Inspector instead of hardcoding a path:

```xml
<Image m_Sprite="{heroIcon}" />
<TextMeshProUGUI m_fontAsset="{mainFont}" />
<Image m_Material="{backgroundMaterial}" />
```

When the file is imported, the Inspector shows an **Asset Bindings** section with a typed field for each `{name}` found in the XML. The field type is determined automatically from the property where the binding is used (e.g. `m_Sprite` → `Sprite`, `m_fontAsset` → `TMP_FontAsset`).

Bindings are useful when:
- The LLM doesn't know what assets exist in the project
- You want to decouple prefab structure from specific asset paths
- You want a designer to assign assets after the XML is generated

The same binding name can be used in multiple places — they all share the same assigned asset. Using the same name with different property types is an error.

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

For arrays of object references, use `ref` attribute with `#id`:

```xml
<ViewContext>
    <Field name="listeners">
        <Item ref="#Button1" />
        <Item ref="#TextDisplay" />
    </Field>
</ViewContext>
```

## Managed references (SerializeReference)

For `[SerializeReference]` fields, define `<Ref>` elements inside the component tag, then reference them by `rid` from `<Item>`:

```xml
<MyComponent>
    <Ref id="myVar" type="MyAssembly MyNamespace.MyConcreteType"
        someField="value" otherField="42" />
    <Ref id="myEvent" type="MyAssembly MyNamespace.MyEventType"
        name="click" />
    <Field name="items">
        <Item rid="myVar" />
        <Item rid="myEvent" />
        <Item />  <!-- null reference -->
    </Field>
</MyComponent>
```

- `<Ref>` — defines a managed reference instance, scoped to the component
  - `id` — unique identifier within the component (required)
  - `type` — fully qualified type in Unity YAML format: `"AssemblyName Namespace.ClassName"` (required)
  - Other attributes are set as properties on the created instance
- `<Item rid="...">` — references a `<Ref>` by id
- `<Item />` without `rid` in a `[SerializeReference]` array — null element

The same `rid` can be used in multiple `<Item>` elements to share the same managed reference instance. Object reference properties inside `<Ref>` support `#id` syntax:

```xml
<Ref id="counter" type="CodeWriter.ViewBinding CodeWriter.ViewBinding.ViewVariableInt"
    name="counter" context="#Root" />
```

### Standalone managed references

For non-array `[SerializeReference]` fields, use `@refId` syntax in the component attribute:

```xml
<MyComponent myAction="@action1">
    <Ref id="action1" type="MyAssembly MyGame.PlaySound" clip="boom" />
</MyComponent>
```

### Nested managed references

When a `[SerializeReference]` object contains another `[SerializeReference]` field, use `@refId` to link them. All `<Ref>` elements are declared flat at the component level:

```xml
<MyComponent>
    <Ref id="cond1" type="MyAssembly MyGame.IsAlive" threshold="0.5" />
    <Ref id="action1" type="MyAssembly MyGame.PlaySound" clip="boom" condition="@cond1" />
    <Field name="actions">
        <Item rid="action1" />
    </Field>
</MyComponent>
```

Here `condition="@cond1"` on `action1` assigns the `cond1` managed reference instance to the `condition` field of `PlaySound`. The `@` prefix distinguishes managed reference links from string values.

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
| ObjectReference | `"{name}"` | asset binding, assigned in Inspector |
| ManagedReference | `"@refId"` | reference to a `<Ref>` by id |

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
