# P4 UI Status

Status: P4 UI Lab accepted on real browser-wasm and Win11 Vulkan profiles.

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
- Win32 GDI rasterizer produced non-empty grayscale pixels for the installed Microsoft YaHei UI glyph `中`.
- Real .NET 11 Preview 7 browser-wasm build loaded in the in-app browser with one textbox and one button.
- Browser computed style evidence for the card: width 420px, padding 24px, radius 16px, background rgb(24,32,44).
- Filling `狮心` updated visible reactive text; clicking 登录 advanced it to `狮心 已点击登录 1 次` with no browser console errors.
- Vulkan RenderGraph includes `Frame.Ui`, compiling 13 paint commands into 3,349 alpha-blended box/glyph-run quads with zero validation errors.
- Computer Use visual evidence after viewport correction shows readable `欢迎回来` and the full Chinese explanatory sentence in the Native Vulkan window.
- A 10,000-component surface changed only index 4,321; 9,999 retained their exact previous trees and compose count.
- Enhancement registry raises `ZUI4102` for two primary renderers and allows replacement only after the first scoped registration is disposed.
- PluginScope disposal removes a UI enhancement registration, so the engine registry retains no plugin factory delegate.

## Native renderer evidence

- `Frame.Ui` is a real RenderGraph pass between triangle and present.
- UI vertex/fragment SPIR-V modules pass `spirv-val` and use one 48-byte push-constant block per quad.
- Initial box-only screenshot exposed and led to correction of the Vulkan positive-viewport Y mapping.
- Corrected screenshot shows the card at the top, system-font `欢迎回来`, full Chinese explanatory text, button/status/badges and the scene triangle together.
- The GDI system-font correctness baseline compiles 3,349 horizontal alpha runs; this is intentionally not the final batching strategy.
- Native UiLab completes with zero Vulkan validation errors.

## Deferred after P4

- Replace the per-row glyph quad correctness baseline with a sampled texture atlas to reduce Native text draw calls.
- Live Chinese IME composition requires an interactive IME-enabled session; the Imm32 adapter contract is present but not synthesized in automation.
- Enhancement package ALC reload stress will be combined with a real UI extension fixture in P5/P7.
- Browser publish/serve acceptance without relying on the preview dev-server package slated for removal.
- Build-time portable/platform reference analyzer is deferred to the unified Zui generator package; current project references already keep Zui.Core/Basic free of Windows/Web dependencies.
