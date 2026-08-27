# P8 Native UI Quality Lab Plan

Status: P8A accepted on 2026-08-27; P8B text shaping and font metrics is next.

## 1. Problem statement

The Native Vulkan UI is functionally correct, supports Chinese system fonts and passes Vulkan validation, but it is not ready for a visual-quality or performance-oriented API freeze.

The 2026-08-27 RTX 3050 validation produced these current baselines:

| Surface | UI nodes | Paint commands | Generated quads | Vulkan errors |
|---|---:|---:|---:|---:|
| Native UiLab | 15 | 14 | 3,351 | 0 |
| Editor Lab | 57 | 52 | 26,288 | 0 |

The screenshots are readable, but small text is soft and uneven, spacing is approximate, rounded corners and shadow blur are not actually rendered, and the editor lacks the hierarchy and restraint expected from a production tool UI.

## 2. Verified root causes

### 2.1 Text coverage is quantized into horizontal rectangles

`VulkanUiScene.AddTextQuads` does not upload or sample a glyph bitmap. It:

- drops coverage below 16;
- merges adjacent coverage values when their difference is below 24;
- turns every merged row segment into a one-pixel-high flat-color quad;
- submits one Vulkan draw for every resulting quad.

This loses edge coverage before the GPU sees the glyph and explains both the soft/jagged appearance and the 26,288-quad editor frame.

### 2.2 The current font path is a correctness baseline

`GdiGlyphRasterizer` uses `GetGlyphOutlineW(GGO_GRAY8_BITMAP)` for one BMP rune at one integer pixel size. It has no:

- glyph shaping or kerning;
- font fallback run construction;
- supplementary-plane support;
- variable-font axes;
- real line, ascent, descent and leading propagation into layout;
- DPI-aware render parameters.

### 2.3 Layout uses estimated text metrics

Native intrinsic width is currently `runeCount * fontSize * 0.65`, line height is `fontSize * 1.25`, and the Vulkan text baseline is `bounds.Y + fontSize`. These estimates can clip, drift or misalign mixed Chinese/Latin text even if rasterization becomes sharper.

### 2.4 Native scale is not a first-class input

The current Win32/UI path has no per-monitor DPI scale contract, `WM_DPICHANGED` handling or explicit logical-DIP to physical-pixel boundary. Fractional layout coordinates are not snapped according to primitive type, so a high-quality atlas alone would still blur at 125% and 150% scale.

### 2.5 Box effects are recorded but not rendered

`PaintBox` carries border radius and shadow blur, but `VulkanUiScene` emits only rectangular flat-color quads. Shadow blur is ignored and a rounded card remains rectangular. `ui.hlsl` has no UV, texture, signed distance or primitive-kind input.

### 2.6 Color and blending need an explicit contract

`UiColor` bytes are passed directly as shader float values while the validated swapchain format is sRGB. The renderer needs one documented sRGB-to-linear conversion point and premultiplied-alpha semantics so text, translucent borders and shadows compose predictably.

## 3. P8 goals

P8 will make the Native UI visually credible and batching-efficient without changing the public Component/State/Theme DSL.

Required outcomes:

1. Sharp Chinese and Latin text at 100%, 125%, 150% and 200% scale.
2. Real glyph metrics, shaping, kerning and fallback behind portable contracts.
3. One textured glyph quad per glyph, not per raster row.
4. Bounded UI draw count independent of text pixel coverage.
5. Real rounded corners, borders and soft shadows.
6. Correct sRGB/premultiplied-alpha composition.
7. Stable frames perform no atlas upload, no UI rebuild and no managed allocation.
8. Browser semantic parity remains intact; pixel identity is not required.

## 4. Explicit non-goals

P8 will not:

- implement a complete W3C text/layout engine;
- make Browser and Vulkan screenshots pixel-identical;
- start with MSDF for ordinary 12-24 px tool text;
- add rich-text editing, bidirectional editing or full international input in the first slice;
- redesign the Component/State/Event DSL;
- hide filesystem watchers inside the release runtime.

Bitmap grayscale coverage is the initial quality target for fixed-size UI text. MSDF remains a later option for large zoom ranges, world-space labels and transformed text.

## 5. Target pipeline

```text
Component / State / Theme
    -> UI tree
    -> DPI-aware logical layout
    -> TextShaper + FontFallback
    -> GlyphRun + real font metrics
    -> variable-cell R8 glyph atlas
    -> dirty-rectangle GPU upload
    -> box and glyph instance buffers
    -> bounded Vulkan draws
    -> sRGB swapchain
```

The portable layer owns glyph runs and metrics contracts. DirectWrite is the first Windows implementation; a future Linux adapter can provide the same contracts through FreeType/HarfBuzz without leaking those APIs into Zui.Core.

## 6. Implementation sequence

### P8A - Visual baseline, DPI and pixel rules

Deliverables:

- Add a `UiScaleContext` carrying logical size, physical size, DPI and scale.
- Make the Win32 process Per-Monitor DPI Aware V2 and handle `WM_DPICHANGED`.
- Keep layout in logical DIPs and convert once at the paint/backend boundary.
- Define snapping rules:
  - boxes and one-pixel borders snap to physical pixel boundaries;
  - text baselines snap according to the raster mode;
  - animation transforms may remain fractional.
- Define sRGB input, linear shader math and premultiplied-alpha output.
- Add a repeatable capture matrix for 100%, 125%, 150% and 200% scale.
- Add UI renderer telemetry: draw count, instance count, glyph cache hit/miss, atlas upload bytes and compile time.

Acceptance:

- Layout bounds remain deterministic at each scale.
- One-pixel borders do not alternate between one and two physical pixels.
- Captures contain no half-pixel drift after resize or DPI changes.
- Stable unchanged UI still allocates zero managed bytes on the measured thread.

### P8B - Shaping, font fallback and real metrics

Deliverables:

- Replace the rune-at-a-time rendering contract with:
  - `FontFaceId`;
  - `GlyphId`;
  - `FontMetrics`;
  - `GlyphPlacement`;
  - `GlyphRun`;
  - `ITextShaper` and `IGlyphRasterizer` boundaries.
- Add a Windows DirectWrite implementation for shaping, fallback and grayscale glyph coverage.
- Cache by face, glyph id, pixel size, weight, scale and raster mode.
- Support surrogate pairs and mixed Chinese/Latin text.
- Feed ascent, descent, leading and advances into layout instead of `0.65` and `1.25` estimates.
- Retain the GDI implementation temporarily as a diagnostic fallback, not the production default.

Acceptance:

- Mixed `中文 ZEngine 123` runs have stable advances and no overlap.
- Latin kerning pairs such as `AV` use shaped positions.
- Supplementary-plane glyphs do not throw solely because they are outside BMP.
- Fallback face selection is deterministic and reported in diagnostics.
- Baselines align across headings, labels and controls.

### P8C - Sampled glyph atlas and instanced batching

Deliverables:

- Extend the Vulkan Raw slice only for the required image, sampler and descriptor commands.
- Establish a portable single-atlas descriptor-set correctness path first; keep it isolated behind a UI texture-binding boundary so Descriptor Buffer and Descriptor Heap implementations can follow without changing ZUI contracts.
- Create a 2048x2048 or 4096x4096 `R8_UNORM` atlas with:
  - variable-size skyline/shelf packing;
  - one-texel padding;
  - generation-safe eviction;
  - dirty-rectangle uploads;
  - timeline retirement for replaced atlas resources.
- Emit one instance per glyph with rectangle, UV, color and clip data.
- Upload box/glyph instance data through a frame-owned ring buffer.
- Draw glyphs in bounded batches rather than one draw per row segment.
- Sample texel centers and validate nearest/linear policy at every supported scale.

Acceptance:

- Native UiLab falls from 3,351 quads to fewer than 200 UI instances.
- Editor Lab falls from 26,288 quads to fewer than 1,500 UI instances.
- Complete Editor UI uses at most 8 UI draw calls in the steady frame.
- A stable frame uploads zero glyph bytes and performs zero atlas mutations.
- Atlas rebuild/replacement produces zero Vulkan validation errors.
- The RTX 3050 DescriptorBuffer machine remains fully valid; the binding boundary keeps the previous Radeon DescriptorHeap route implementable and testable when that hardware profile is available again.

### P8D - Analytic boxes, borders, shadows and clipping

Deliverables:

- Add primitive kind and per-instance parameters to the UI shader.
- Render rounded rectangles and borders from analytic signed distance in the fragment shader.
- Implement soft shadow falloff using bounded analytic distance or a dedicated cached shadow strategy.
- Add clip rectangles/scissors for panels and nested content.
- Apply opacity and premultiplied alpha consistently.
- Remove the current extra inner/outer box workaround for borders.

Acceptance:

- `BorderRadius` visibly affects Native output and matches the declared geometry.
- `UiShadow.Blur` changes falloff rather than only expanding a flat rectangle.
- Nested panel content cannot paint outside its clip.
- Translucent text, borders and shadows have no dark or bright halo.

### P8E - Editor visual system and regression gates

Deliverables:

- Define editor typography roles: title, panel heading, body, label, metric and muted metadata.
- Reduce default density and establish spacing/elevation tokens instead of per-panel ad hoc values.
- Add hover, pressed, focus and selected visual states.
- Rework the three-column editor composition around clear hierarchy and a dominant viewport.
- Capture Native UiLab, Editor Lab and Game Slice golden images at the four DPI scales.
- Keep semantic-tree and browser parity tests separate from backend-specific visual goldens.

Acceptance:

- Chinese and Latin text remain readable at the smallest supported editor size.
- The viewport is visually dominant; panels, labels and status data have clear hierarchy.
- Focus indication is visible and not color-only.
- Screenshot diffs stay within backend-specific tolerances.
- Full Release and Debug validation remain green on the current multi-GPU machine.

## 7. Performance and quality gates

P8 is not accepted until all of the following hold:

| Gate | Target |
|---|---:|
| Release tests | all pass, zero skipped |
| Debug validation tests | all pass, zero skipped |
| Vulkan validation errors | 0 |
| Editor steady UI draw calls | <= 8 |
| Editor UI instances | < 1,500 |
| Native UiLab UI instances | < 200 |
| Stable-frame glyph uploads | 0 bytes |
| Stable-frame managed allocation | 0 bytes on measured thread |
| DPI capture profiles | 100%, 125%, 150%, 200% |
| Required scripts | `bootstrap-new-machine.ps1 -FullValidation` and formatting gate pass |

Performance numbers must report CPU/GPU, driver, DPI, validation-layer state and build configuration. The old RX 9070 GRE and current RTX 3050 results must not be compared without those qualifiers.

## 8. Proposed commit sequence

Keep P8 reviewable through focused commits:

1. `test: add native UI quality baselines`
2. `feat: add dpi-aware native UI scale context`
3. `feat: shape native text with directwrite`
4. `feat: upload glyph coverage to a vulkan atlas`
5. `perf: batch native UI glyph instances`
6. `feat: render analytic rounded boxes and shadows`
7. `style: refine editor visual hierarchy`
8. `test: accept p8 native UI quality lab`

Do not combine the DirectWrite contract, Vulkan descriptor/image work and editor restyle in one change. Each step needs its own capture and performance evidence.

## 9. Main risks

| Risk | Mitigation |
|---|---|
| Atlas looks soft at 100% | Pixel-snap baselines and sample texel centers before changing filters |
| ClearType color fringes on translucent surfaces | Start with grayscale coverage; do not store subpixel RGB in the general atlas |
| Descriptor binding work expands P8 | Gate the first atlas through one isolated portable descriptor-set path |
| Atlas eviction invalidates in-flight frames | Use generations and timeline retirement |
| DirectWrite types leak into portable UI | Keep all COM/native types inside `Zui.Platform.Windows` |
| Visual polish hides layout errors | Land metrics and DPI tests before theme restyling |
| Goldens become machine-specific | Separate semantic tests from per-backend, per-DPI goldens and record the font face |

## 10. Recommended immediate next task

Start with P8A, not the atlas implementation itself.

The first change should add the capture/telemetry matrix and a DPI-aware `UiScaleContext`, then record current and pixel-snapped screenshots. That gives the atlas and DirectWrite work objective evidence and prevents a large renderer rewrite from being judged only by subjective screenshots.
