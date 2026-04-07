# PrefabXML Templates

Reference templates for common UI components. Use these as building blocks.

## Button

```xml
<UnityPrefab>
    <GameObject name="Button">
        <RectTransform m_SizeDelta="220, 60" />
        <Image m_Color="#44D962" />
        <Button
            m_Transition="ColorTint"
            m_Colors.m_NormalColor="#FFFFFF"
            m_Colors.m_HighlightedColor="#E8E8E8"
            m_Colors.m_PressedColor="#C0C0C0"
            m_Colors.m_DisabledColor="#A0A0A080"
            m_Colors.m_FadeDuration="0.1" />
            <!-- m_Transition: None, ColorTint, SpriteSwap, Animation -->
            <!-- m_Navigation.m_Mode: None, Horizontal, Vertical, Automatic, Explicit -->

        <GameObject name="Label">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
            <TextMeshProUGUI
                m_text="Button"
                m_fontSize="24"
                m_fontStyle="Bold"
                m_fontColor="#FFFFFF"
                m_HorizontalAlignment="Center"
                m_VerticalAlignment="Middle"
                m_TextWrappingMode="NoWrap" />
                <!-- m_fontStyle: Normal, Bold, Italic, Underline, Strikethrough -->
                <!-- m_overflowMode: Overflow, Ellipsis, Masking, Truncate, ScrollRect -->
                <!-- m_characterSpacing="0" m_wordSpacing="0" m_lineSpacing="0" -->
                <!-- m_enableAutoSizing="false" m_fontSizeMin="10" m_fontSizeMax="36" -->
                <!-- m_margin="0, 0, 0, 0" (Vector4: left, top, right, bottom) -->
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## Text (TextMeshPro)

```xml
<UnityPrefab>
    <GameObject name="Text (TMP)">
        <RectTransform m_SizeDelta="200, 50" />
        <TextMeshProUGUI
            m_text="New Text"
            m_fontSize="24"
            m_fontColor="#FFFFFF"
            m_HorizontalAlignment="Center"
            m_VerticalAlignment="Middle"
            m_TextWrappingMode="Normal"
            m_RaycastTarget="false" />
            <!-- m_fontStyle: Normal, Bold, Italic, Underline, Strikethrough -->
            <!-- m_HorizontalAlignment: Left, Center, Right, Justified, Flush, Geometry -->
            <!-- m_VerticalAlignment: Top, Middle, Bottom, Baseline, Geometry, Capline -->
            <!-- m_TextWrappingMode: NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap -->
            <!-- m_overflowMode: Overflow, Ellipsis, Masking, Truncate, ScrollRect -->
            <!-- m_fontAsset="Assets/Fonts/Roboto SDF.asset" (asset path) -->
            <!-- m_characterSpacing="0" m_wordSpacing="0" m_lineSpacing="0" m_paragraphSpacing="0" -->
            <!-- m_enableAutoSizing="false" m_fontSizeMin="10" m_fontSizeMax="36" -->
            <!-- m_margin="0, 0, 0, 0" (Vector4: left, top, right, bottom) -->
    </GameObject>
</UnityPrefab>
```

## Image

```xml
<UnityPrefab>
    <GameObject name="Image">
        <RectTransform m_SizeDelta="100, 100" />
        <Image
            m_Color="#FFFFFF"
            m_RaycastTarget="true"
            m_Type="Simple"
            m_PreserveAspect="false" />
            <!-- m_Sprite="Assets/Sprites/icon.png" (asset path) -->
            <!-- m_Type: Simple, Sliced, Tiled, Filled -->
            <!-- m_FillMethod: Horizontal, Vertical, Radial90, Radial180, Radial360 -->
            <!-- m_FillAmount="1" (0-1, only for Filled type) -->
            <!-- m_Maskable="true" -->
            <!-- m_RaycastPadding="0, 0, 0, 0" (Vector4: left, bottom, right, top) -->
    </GameObject>
</UnityPrefab>
```

## RawImage

```xml
<UnityPrefab>
    <GameObject name="RawImage">
        <RectTransform m_SizeDelta="100, 100" />
        <RawImage m_Color="#FFFFFF" m_RaycastTarget="true" />
            <!-- m_Texture="Assets/Textures/photo.png" (asset path) -->
            <!-- m_UVRect="0, 0, 1, 1" (Rect: x, y, width, height) -->
    </GameObject>
</UnityPrefab>
```

## Toggle

```xml
<UnityPrefab>
    <GameObject name="Toggle">
        <RectTransform m_SizeDelta="200, 40" />
        <Image m_Color="#00000000" m_RaycastTarget="true" />
        <Toggle
            m_IsOn="true"
            graphic="#Checkmark"
            m_Transition="ColorTint"
            m_Colors.m_NormalColor="#FFFFFF"
            m_Colors.m_HighlightedColor="#E8E8E8"
            m_Colors.m_PressedColor="#C0C0C0"
            m_Colors.m_DisabledColor="#A0A0A080"
            m_Colors.m_FadeDuration="0.1"
            m_TargetGraphic="#Track" />
            <!-- toggleTransition: None, Fade -->
            <!-- m_Group="#id" (ToggleGroup reference) -->

        <GameObject name="Track" id="Track">
            <RectTransform m_AnchorMin="1, 0.5" m_AnchorMax="1, 0.5"
                m_Pivot="1, 0.5"
                m_SizeDelta="28, 28" />
            <Image m_Color="#44D962" />

            <GameObject name="Checkmark" id="Checkmark">
                <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1"
                    m_SizeDelta="-6, -6" />
                <Image m_Color="#FFFFFF" />
            </GameObject>
        </GameObject>

        <GameObject name="Label">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1"
                m_SizeDelta="-60, 0" />
            <TextMeshProUGUI m_text="Toggle" m_fontSize="18"
                m_fontColor="#FFFFFF" m_RaycastTarget="false"
                m_HorizontalAlignment="Left" m_VerticalAlignment="Middle" />
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## Slider

```xml
<UnityPrefab>
    <GameObject name="Slider">
        <RectTransform m_SizeDelta="240, 24" />
        <Image m_Color="#2A2A3E" />
        <Slider
            m_FillRect="#Fill"
            m_HandleRect="#Handle"
            m_Direction="LeftToRight"
            m_MinValue="0"
            m_MaxValue="1"
            m_Value="0.5"
            m_WholeNumbers="false"
            m_Transition="ColorTint"
            m_Colors.m_NormalColor="#FFFFFF"
            m_Colors.m_HighlightedColor="#E8E8E8"
            m_Colors.m_PressedColor="#C0C0C0"
            m_Colors.m_DisabledColor="#A0A0A080"
            m_Colors.m_FadeDuration="0.1" />
            <!-- m_Direction: LeftToRight, RightToLeft, BottomToTop, TopToBottom -->

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
                <RectTransform m_SizeDelta="28, 28" />
                <Image m_Color="#FFFFFF" />
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## ScrollView

```xml
<UnityPrefab>
    <GameObject name="ScrollView">
        <RectTransform m_SizeDelta="300, 400" />
        <Image m_Color="#1E1E2E" />
        <ScrollRect
            m_Content="#Content"
            m_Viewport="#Viewport"
            m_Horizontal="false"
            m_Vertical="true"
            m_MovementType="Elastic"
            m_Elasticity="0.1"
            m_Inertia="true"
            m_DecelerationRate="0.135"
            m_ScrollSensitivity="1" />
            <!-- m_MovementType: Unrestricted, Elastic, Clamped -->
            <!-- m_HorizontalScrollbar="#id" m_VerticalScrollbar="#id" -->

        <GameObject name="Viewport" id="Viewport">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
            <Image m_Color="#1E1E2E" />
            <Mask m_ShowMaskGraphic="false" />

            <GameObject name="Content" id="Content">
                <RectTransform m_AnchorMin="0, 1" m_AnchorMax="1, 1"
                    m_Pivot="0.5, 1" m_SizeDelta="0, 0" />
                <VerticalLayoutGroup m_Spacing="4"
                    m_ChildControlWidth="true" m_ChildControlHeight="true"
                    m_ChildForceExpandWidth="true" m_ChildForceExpandHeight="false" />
                <ContentSizeFitter m_VerticalFit="PreferredSize" />
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## Dropdown (TMP)

```xml
<UnityPrefab>
    <GameObject name="Dropdown">
        <RectTransform m_SizeDelta="200, 40" />
        <Image m_Color="#2A2A3E" />
        <TMP_Dropdown
            m_Template="#Template"
            m_CaptionText="#CaptionText"
            m_ItemText="#ItemText"
            m_Value="0"
            m_AlphaFadeSpeed="0.15"
            m_Transition="ColorTint"
            m_Colors.m_NormalColor="#FFFFFF"
            m_Colors.m_HighlightedColor="#E8E8E8"
            m_Colors.m_PressedColor="#C0C0C0"
            m_Colors.m_DisabledColor="#A0A0A080"
            m_Colors.m_FadeDuration="0.1">
            <Field name="m_Options.m_Options">
                <Item m_Text="Option A" />
                <Item m_Text="Option B" />
                <Item m_Text="Option C" />
            </Field>
        </TMP_Dropdown>

        <GameObject name="Caption" id="CaptionText">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1"
                m_SizeDelta="-36, 0" m_AnchoredPosition="-8, 0" />
            <TextMeshProUGUI m_text="Option A" m_fontSize="16"
                m_fontColor="#FFFFFF" m_RaycastTarget="false"
                m_HorizontalAlignment="Left" m_VerticalAlignment="Middle" />
        </GameObject>

        <GameObject name="Arrow">
            <RectTransform m_AnchorMin="1, 0.5" m_AnchorMax="1, 0.5"
                m_Pivot="1, 0.5"
                m_SizeDelta="20, 20" m_AnchoredPosition="-10, 0" />
            <Image m_Color="#AAAACC" />
        </GameObject>

        <GameObject name="Template" id="Template" active="false">
            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 0"
                m_Pivot="0.5, 1"
                m_SizeDelta="0, 150" />
            <Image m_Color="#1E1E2E" />
            <ScrollRect m_Vertical="true" m_Horizontal="false"
                m_MovementType="Clamped"
                m_Content="#TemplateContent" m_Viewport="#TemplateViewport" />

            <GameObject name="Viewport" id="TemplateViewport">
                <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
                <Image m_Color="#1E1E2E" />
                <Mask m_ShowMaskGraphic="false" />

                <GameObject name="Content" id="TemplateContent">
                    <RectTransform m_AnchorMin="0, 1" m_AnchorMax="1, 1"
                        m_Pivot="0.5, 1" m_SizeDelta="0, 28" />

                    <GameObject name="Item">
                        <RectTransform m_AnchorMin="0, 0.5" m_AnchorMax="1, 0.5"
                            m_SizeDelta="0, 20" />
                        <Image m_Color="#1E1E2E" />
                        <Toggle m_IsOn="false"
                            m_Transition="ColorTint"
                            m_Colors.m_NormalColor="#FFFFFF"
                            m_Colors.m_HighlightedColor="#3A3A50"
                            m_Colors.m_PressedColor="#44D962"
                            m_Colors.m_SelectedColor="#3A3A50" />

                        <GameObject name="Item Label" id="ItemText">
                            <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1"
                                m_SizeDelta="-20, 0" m_AnchoredPosition="10, 0" />
                            <TextMeshProUGUI m_text="Option" m_fontSize="14"
                                m_fontColor="#CCCCEE" m_RaycastTarget="false"
                                m_HorizontalAlignment="Left" m_VerticalAlignment="Middle" />
                        </GameObject>
                    </GameObject>
                </GameObject>
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## Sample: ConfirmDialog

Complete working example of a modal dialog:

```xml
<UnityPrefab>
    <GameObject name="ConfirmDialog">
        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
        <Image m_Color="#00000099" />

        <GameObject name="Panel">
            <RectTransform m_SizeDelta="450, 220" />
            <Image m_Color="#1E1E2E" />
            <VerticalLayoutGroup m_Spacing="0"
                m_ChildControlWidth="true" m_ChildControlHeight="true"
                m_ChildForceExpandWidth="true" m_ChildForceExpandHeight="false" />

            <GameObject name="Header">
                <RectTransform />
                <Image m_Color="#2A2A3E" />
                <LayoutElement m_MinHeight="52" />
                <GameObject name="TitleText">
                    <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
                    <TextMeshProUGUI m_text="Confirmation" m_fontSize="22" m_fontStyle="Bold"
                          m_fontColor="#E0E0FF" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
                </GameObject>
            </GameObject>

            <GameObject name="Separator">
                <RectTransform />
                <Image m_Color="#4A4A6A" />
                <LayoutElement m_MinHeight="2" />
            </GameObject>

            <GameObject name="Body">
                <RectTransform />
                <LayoutElement m_MinHeight="100" />
                <GameObject name="MessageText">
                    <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="-48, -24" />
                    <TextMeshProUGUI m_text="Are you sure?" m_fontSize="16" m_fontColor="#B0B0CC"
                          m_HorizontalAlignment="Left" m_VerticalAlignment="Top" m_TextWrappingMode="Normal" />
                </GameObject>
            </GameObject>

            <GameObject name="Footer">
                <RectTransform />
                <Image m_Color="#252538" />
                <LayoutElement m_MinHeight="66" />
                <HorizontalLayoutGroup m_Spacing="12" m_ChildAlignment="MiddleCenter"
                    m_ChildControlWidth="false" m_ChildControlHeight="false"
                    m_ChildForceExpandWidth="false" m_ChildForceExpandHeight="false" />

                <GameObject name="ButtonCancel">
                    <RectTransform m_SizeDelta="130, 42" />
                    <Image m_Color="#3A3A50" />
                    <Button />
                    <LayoutElement m_MinWidth="130" m_MinHeight="42" />
                    <GameObject name="Label">
                        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
                        <TextMeshProUGUI m_text="Cancel" m_fontSize="16"
                              m_fontColor="#B0B0CC" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
                    </GameObject>
                </GameObject>

                <GameObject name="ButtonConfirm">
                    <RectTransform m_SizeDelta="130, 42" />
                    <Image m_Color="#5B68E0" />
                    <Button />
                    <LayoutElement m_MinWidth="130" m_MinHeight="42" />
                    <GameObject name="Label">
                        <RectTransform m_AnchorMin="0, 0" m_AnchorMax="1, 1" m_SizeDelta="0, 0" />
                        <TextMeshProUGUI m_text="Confirm" m_fontSize="16" m_fontStyle="Bold"
                              m_fontColor="#FFFFFF" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
                    </GameObject>
                </GameObject>
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```

## Sample: Counter with MonoBehaviour

PrefabXML with custom script wiring via `#id` references:

```xml
<UnityPrefab>
    <GameObject name="Counter">
        <RectTransform m_SizeDelta="200, 80" />
        <Image m_Color="#1E1E2E" />
        <CounterController
            counterText="#CounterText"
            incrementButton="#IncrementBtn"
            decrementButton="#DecrementBtn" />

        <GameObject name="DecrementBtn" id="DecrementBtn">
            <RectTransform m_SizeDelta="50, 80" m_AnchoredPosition="-75, 0" />
            <Image m_Color="#FF5577" />
            <Button />
            <GameObject name="Label">
                <RectTransform />
                <TextMeshProUGUI m_text="-" m_fontSize="28" m_fontStyle="Bold"
                    m_fontColor="#FFFFFF" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
            </GameObject>
        </GameObject>

        <GameObject name="Display" id="CounterText">
            <RectTransform m_SizeDelta="100, 80" />
            <TextMeshProUGUI m_text="0" m_fontSize="36" m_fontStyle="Bold"
                m_fontColor="#FFFFFF" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
        </GameObject>

        <GameObject name="IncrementBtn" id="IncrementBtn">
            <RectTransform m_SizeDelta="50, 80" m_AnchoredPosition="75, 0" />
            <Image m_Color="#44D962" />
            <Button />
            <GameObject name="Label">
                <RectTransform />
                <TextMeshProUGUI m_text="+" m_fontSize="28" m_fontStyle="Bold"
                    m_fontColor="#FFFFFF" m_HorizontalAlignment="Center" m_VerticalAlignment="Middle" />
            </GameObject>
        </GameObject>
    </GameObject>
</UnityPrefab>
```

Corresponding C# script:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CounterController : MonoBehaviour
{
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Button incrementButton;
    [SerializeField] private Button decrementButton;

    private int count;

    private void OnEnable()
    {
        incrementButton.onClick.AddListener(Increment);
        decrementButton.onClick.AddListener(Decrement);
        UpdateText();
    }

    private void OnDisable()
    {
        incrementButton.onClick.RemoveListener(Increment);
        decrementButton.onClick.RemoveListener(Decrement);
    }

    private void Increment() { count++; UpdateText(); }
    private void Decrement() { count--; UpdateText(); }
    private void UpdateText() { counterText.text = count.ToString(); }
}
```
