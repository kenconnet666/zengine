# P4 UI Status

Status: P4A shared typed DSL, Blazor DOM adapter and native layout/text discovery in progress.

## Implemented

- `Zui.Core`, `Zui.Basic`, `Zui.Backend.Blazor`, `Zui.Rendering` and `Zui.Platform.Windows` package boundaries.
- Generic `Component<TTheme>` and inherited `SysTheme` token model.
- Single-scope uppercase `DIV`, `SPAN`, `H1`, `INPUT`, `BUTTON` and `TEXT` DSL.
- Box-local typed style accessors for size, color, padding, margin, gap, flex, border, radius, shadow and typography.
- Static model callback overload for closure-free repeated content.
- DOM-like UI IR with stable NodeId, role, semantic label and typed event delegates.
- Reactive `State<T>` dependency tracking and no-compose stable frames.
- Tree diff with stable IDs.
- Blazor `RenderTreeBuilder` backend and typed CSS compiler; no HTML template strings or property-by-property JS glue.
- Native block/flex layout and paint-list compiler.
- Windows installed-font discovery with TrueType/TTC cmap coverage checks.
- Glyph-atlas slot lifecycle and Imm32 composition adapter boundary.
- Shared Chinese `LoginCard` component used by both backends.

## P4A validation evidence

- Stable `LoginCard.Render(theme)` returned the identical tree for 10,000 frames and measured zero managed bytes.
- Input event changed `State<string>`, preserved NodeIds, and produced an update-only tree diff.
- `HtmlRenderer` produced real `div/input/button` markup, typed inline CSS and valid numeric HTML entities for Chinese text.
- Native layout produced non-negative bounds plus box and Chinese text paint commands.
- Windows catalog resolved an installed system font whose cmap covers `中文 ZEngine`.
- Real .NET 11 Preview 7 browser-wasm build loaded in the in-app browser with one textbox and one button.
- Browser computed style evidence for the card: width 420px, padding 24px, radius 16px, background rgb(24,32,44).
- Filling `狮心` updated visible reactive text; clicking 登录 advanced it to `狮心 已点击登录 1 次` with no browser console errors.

## Remaining P4

- Vulkan box/text renderer and Native UiLab window.
- Real glyph rasterization/upload from the selected system font and atlas eviction validation.
- 10,000-node localized dirty-subtree update benchmark.
- Basic/portable/platform enhancement registry and conflict diagnostics.
- Browser publish/serve acceptance without relying on the preview dev-server package slated for removal.
