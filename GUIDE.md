# PrefabXML Guide for LLMs

Common mistakes and rules to follow when generating `.prefabxml` files.

## Use exact Unity serialized field names

All property names must match Unity's serialized field names exactly. Built-in Unity components use `m_` prefix, but custom components may use any naming convention (e.g. `health`, `maxSpeed`). Do NOT invent shorthand names.

```xml
<!-- WRONG -->
<RectTransform width="100" height="50" anchor="center" />
<Image color="#FF0000" />
<TextMeshProUGUI text="Hello" fontSize="24" alignment="Center" />

<!-- CORRECT -->
<RectTransform m_SizeDelta="100, 50" />
<Image m_Color="#FF0000" />
<TextMeshProUGUI m_text="Hello" m_fontSize="24"
    m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
```

## TextMeshPro field names

TMP uses lowercase after `m_` (unlike standard Unity components). Common mistakes:

```xml
<!-- WRONG -->
<TextMeshProUGUI m_Text="..." m_FontSize="24" m_Color="#FFF" m_textAlignment="Center" m_enableWordWrapping="true" />

<!-- CORRECT -->
<TextMeshProUGUI m_text="..." m_fontSize="24" m_fontColor="#FFFFFF"
    m_HorizontalAlignment="Center" m_VerticalAlignment="Middle"
    m_TextWrappingMode="Normal" />
```

Key TMP fields:
- `m_text` — text content
- `m_fontSize` — font size
- `m_fontStyle` — `"Normal"`, `"Bold"`, `"Italic"` (use enum name, NOT number)
- `m_fontColor` — text color (NOT `m_Color`)
- `m_HorizontalAlignment` — `"Left"`, `"Center"`, `"Right"`, `"Justified"`
- `m_VerticalAlignment` — `"Top"`, `"Middle"`, `"Bottom"`
- `m_TextWrappingMode` — `"NoWrap"`, `"Normal"` (NOT `m_enableWordWrapping`)
- `m_overflowMode` — `"Overflow"`, `"Ellipsis"`, `"Truncate"`
- `m_isRichText` — `"true"` / `"false"`
- `m_RaycastTarget` — set to `"false"` for non-interactive text

## Enum values must use names, not numbers

```xml
<!-- WRONG -->
<TextMeshProUGUI m_fontStyle="1" />

<!-- CORRECT -->
<TextMeshProUGUI m_fontStyle="Bold" />
```

## One Graphic per GameObject

`Image`, `RawImage`, and `TextMeshProUGUI` all inherit from `Graphic`. Unity allows only ONE `Graphic` per GameObject. If you need background + text, use a child object:

```xml
<!-- WRONG: two Graphics on one object -->
<GameObject name="Label">
    <Image m_Color="#222222" />
    <TextMeshProUGUI m_text="Hello" />
</GameObject>

<!-- CORRECT: text in child object -->
<GameObject name="Label">
    <Image m_Color="#222222" />
    <GameObject name="Text">
        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
        <TextMeshProUGUI m_text="Hello" m_fontSize="16" m_fontColor="#FFFFFF"
            m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
    </GameObject>
</GameObject>
```

## Selectable components need Image

`Button`, `Toggle`, `Slider`, `Dropdown` inherit from `Selectable` which requires an `Image` component on the same GameObject for `targetGraphic`. Always add `<Image>` BEFORE these components:

```xml
<!-- WRONG: Button without Image -->
<GameObject name="MyButton">
    <Button />
</GameObject>

<!-- CORRECT -->
<GameObject name="MyButton">
    <Image m_Color="#44D962" />
    <Button />
</GameObject>
```

## Toggle requires checkmark structure

A bare `<Toggle />` won't display a checkbox. Use the structure from `Templates/Toggle.prefabxml`:

```xml
<GameObject name="Toggle">
    <Image m_Color="#00000000" m_RaycastTarget="true" />
    <Toggle m_IsOn="true" graphic="#Checkmark" m_TargetGraphic="#Background" />

    <GameObject name="Background" id="Background">
        <RectTransform m_SizeDelta="28, 28" />
        <Image m_Color="#2A2A3E" />
        <GameObject name="Checkmark" id="Checkmark">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="-6, -6" />
            <Image m_Color="#44D962" />
        </GameObject>
    </GameObject>
</GameObject>
```

## Slider requires FillRect

A bare `<Slider />` will error — it needs at least `m_FillRect`. `m_HandleRect` is optional (only needed if you want a visible handle). Use the structure from `Templates/Slider.prefabxml`:

```xml
<Slider m_FillRect="#Fill" m_HandleRect="#Handle" m_Value="0.5" />

<GameObject name="FillArea">
    <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
    <GameObject name="Fill" id="Fill">
        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="0.5, 1" m_SizeDelta="0, 0" />
        <Image m_Color="#44D962" />
    </GameObject>
</GameObject>
<GameObject name="HandleArea">
    <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
    <GameObject name="Handle" id="Handle">
        <RectTransform m_SizeDelta="20, 0" />
        <Image m_Color="#FFFFFF" />
    </GameObject>
</GameObject>
```

## RectTransform defaults

`m_AnchorMin` and `m_AnchorMax` default to `0.5, 0.5` (center). If you want a centered element, just set size and position:

```xml
<!-- No need to specify anchors for centered elements -->
<RectTransform m_SizeDelta="200, 80" />
<RectTransform m_SizeDelta="200, 80" m_AnchoredPosition="0, 50" />
```

Only specify anchors when you need stretch or non-center alignment:

```xml
<!-- Stretch to fill parent -->
<RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
```

## RectTransform must be first component

When present, `<RectTransform>` must be the first component tag inside `<GameObject>`:

```xml
<!-- WRONG -->
<GameObject name="Panel">
    <Image m_Color="#222222" />
    <RectTransform m_SizeDelta="400, 300" />
</GameObject>

<!-- CORRECT -->
<GameObject name="Panel">
    <RectTransform m_SizeDelta="400, 300" />
    <Image m_Color="#222222" />
</GameObject>
```

## `id` goes on GameObject, not on components

```xml
<!-- WRONG -->
<TextMeshProUGUI id="CaptionText" m_text="Hello" />

<!-- CORRECT -->
<GameObject name="Caption" id="CaptionText">
    <TextMeshProUGUI m_text="Hello" />
</GameObject>
```

## LayoutGroup needs ChildControl flags

`VerticalLayoutGroup` and `HorizontalLayoutGroup` won't resize children without `m_ChildControlWidth` / `m_ChildControlHeight`:

```xml
<VerticalLayoutGroup m_Spacing="8"
    m_ChildControlWidth="true" m_ChildControlHeight="true"
    m_ChildForceExpandWidth="true" m_ChildForceExpandHeight="false" />
```

## Use only necessary attributes

Do NOT set every possible attribute. Set only what differs from defaults. Compare:

```xml
<!-- TOO VERBOSE -->
<TextMeshProUGUI m_text="Hello" m_fontSize="24" m_fontStyle="Normal"
    m_fontColor="#FFFFFF" m_HorizontalAlignment="Left" m_VerticalAlignment="Top"
    m_TextWrappingMode="Normal" m_overflowMode="Overflow" m_isRichText="true"
    m_RaycastTarget="true" m_Maskable="true" m_enableAutoSizing="false" />

<!-- BETTER: only set what you need -->
<TextMeshProUGUI m_text="Hello" m_fontSize="24" m_fontColor="#FFFFFF"
    m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
```

## Padding on LayoutGroups

`m_Padding` is a `RectOffset` (nested struct). Use dot notation to set individual sides:

```xml
<VerticalLayoutGroup m_Spacing="8"
    m_Padding.m_Left="16" m_Padding.m_Right="16"
    m_Padding.m_Top="12" m_Padding.m_Bottom="12"
    m_ChildControlWidth="true" m_ChildControlHeight="true"
    m_ChildForceExpandWidth="true" m_ChildForceExpandHeight="false" />
```

## Image with Sprite inside LayoutGroup

When an `Image` has `m_Sprite` set, it reports the sprite's native size as its preferred size to the layout system. This overrides `LayoutElement.m_MinWidth`/`m_MinHeight`. To control the size, set `m_PreferredWidth`/`m_PreferredHeight` on LayoutElement — they take priority over Image's preferred size:

```xml
<!-- WRONG: Image sprite size overrides min size -->
<GameObject name="Icon">
    <LayoutElement m_MinWidth="48" m_MinHeight="48" />
    <Image m_Sprite="Assets/Sprites/avatar.png" />
</GameObject>

<!-- CORRECT: preferred size takes priority -->
<GameObject name="Icon">
    <LayoutElement m_MinWidth="48" m_MinHeight="48" m_PreferredWidth="48" m_PreferredHeight="48" />
    <Image m_Sprite="Assets/Sprites/avatar.png" m_PreserveAspect="true" />
</GameObject>
```

## Use Templates as reference

When unsure about field names, check `Templates/` folder for correct serialized property names:

- `Templates/Button-TextMeshPro.prefabxml` — Button with all ColorTint fields
- `Templates/Toggle.prefabxml` — Toggle with checkmark structure
- `Templates/Slider.prefabxml` — Slider with fill/handle structure
- `Templates/Dropdown.prefabxml` — Full TMP_Dropdown structure
- `Templates/ScrollView.prefabxml` — ScrollRect with viewport/mask/content
- `Templates/Text-TextMeshPro.prefabxml` — All TMP field names
- `Templates/Image.prefabxml` — All Image field names

## Nested property paths

Use dot notation for nested struct properties:

```xml
<Button m_Transition="ColorTint"
    m_Colors.m_NormalColor="#FFFFFF"
    m_Colors.m_HighlightedColor="#E8E8E8"
    m_Colors.m_PressedColor="#C0C0C0"
    m_Colors.m_DisabledColor="#A0A0A080"
    m_Colors.m_FadeDuration="0.1" />
```