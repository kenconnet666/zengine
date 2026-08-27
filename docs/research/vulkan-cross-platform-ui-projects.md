# Vulkan Cross-Platform UI Project Survey

Status: research snapshot on 2026-08-27. No third-party UI runtime is adopted and no ZEngine implementation change is authorized by this document.

## 1. Research question

ZEngine already has a retained, strongly typed C# UI tree with Blazor and Native Vulkan projections. The current problem is not the absence of a component model; it is the Native renderer's text quality, atlas, batching, clipping and visual effects.

This survey asks:

1. Which actively maintained cross-platform UI projects can render through Vulkan?
2. Which ones expose enough source to study their real frame and text pipelines?
3. How do they convert UI intent into GPU resources and draw calls?
4. Which parts are applicable to ZEngine without replacing its C# DSL, plugin model or Blazor backend?

## 2. Executive conclusion

There is no single project that should replace ZUI.

The strongest references are complementary:

| ZEngine concern | Best primary reference | Why |
|---|---|---|
| Minimal Vulkan texture/draw backend | Dear ImGui | Small, explicit Vulkan backend with atlas texture updates, vertex/index rings, descriptor binding and scissors |
| Dirty glyph atlas and WGPU/Vulkan upload | egui/epaint | Clear CPU paint IR, growing atlas, one-pixel padding, dirty rectangles and partial texture deltas |
| Retained HTML/CSS-like renderer boundary | RmlUi | Closest public contract to ZUI: vertices, indices, textures, transforms, scissors and replaceable font/render interfaces |
| Backend/renderer separation | Slint | Declarative retained UI with interchangeable software, FemtoVG/WGPU and Skia/Vulkan renderers |
| Retained scene graph and batching | Qt Quick | Industrial example of retained nodes, material batching, geometry retention and multiple text render modes |
| Shaped glyph run / atlas boundary | Flutter Impeller | Separates shaping from rendering: Typographer consumes shaped glyph runs and rasterizes them into atlases |
| Game UI render-thread integration | NoesisGUI | Explicit UI/render thread handoff, offscreen/onscreen phases, dynamic texture updates and glyph-cache controls |

Recommended direction:

- Keep ZUI's Component/State/Theme and Blazor backend.
- Use Dear ImGui and egui as implementation references for P8C Vulkan atlas/upload/batching.
- Use Impeller and RmlUi as references for the P8B shaped-glyph contract.
- Use Qt Quick and NoesisGUI as references for P8D retained batching, effects and renderer-thread ownership.
- Do not import a full third-party UI runtime until a small isolated spike proves that replacing ZUI is cheaper than completing P8.

## 3. Project comparison

| Project | UI model | Vulkan path | Text path | Integration fit | License |
|---|---|---|---|---|---|
| Dear ImGui | Immediate mode | Official direct Vulkan backend | Dynamic font atlas; optional rasterizer paths | Excellent renderer reference; poor ZUI replacement | MIT |
| egui | Immediate mode with retained internal state | `egui-wgpu` -> wgpu -> Vulkan | `harfrust` shaping, CPU atlas, textured triangle meshes | Excellent atlas/mesh reference; Rust/wgpu prevents direct reuse | MIT OR Apache-2.0 |
| RmlUi | Retained DOM + HTML/CSS-like layout | Official Vulkan sample or custom `RenderInterface` | FreeType default, HarfBuzz shaping sample, fallback fonts | Closest semantic/render boundary; C++ core and sample backend caveats | MIT |
| Slint | Declarative retained item tree | Skia Vulkan or FemtoVG over WGPU/Vulkan | Renderer-dependent; Parley/Fontique direction, platform/system rasterizers | Architecture reference; not a small dependency | GPLv3 / royalty-free / commercial |
| Qt Quick | Retained scene graph | QRhi -> Vulkan | Distance field, native platform rasterization or GPU curve rendering | Strong industrial reference; too large and conflicts with Vulkan-native scope | LGPLv3/GPLv3 or commercial |
| Flutter/Impeller | Retained Flutter tree -> DisplayList | Native Impeller Vulkan backend | SkParagraph shapes; Impeller Typographer atlases shaped glyph runs | Excellent architecture reference; unsuitable as embedded dependency | BSD-3-Clause |
| NoesisGUI | Retained XAML | Custom/reference RenderDevice including Vulkan | Proprietary shaping/raster pipeline and configurable glyph cache | Strong game UI reference; commercial/closed-core boundary | Commercial/evaluation terms |

## 4. Dear ImGui

### 4.1 Architecture

Dear ImGui deliberately separates:

- platform backend: input, cursor, timing and windowing;
- renderer backend: texture creation and rendering `ImDrawData`.

The official backend overview describes each backend as a small self-contained pair of files. The renderer backend is responsible for the atlas texture and draw data, while Win32/SDL/GLFW backends own platform events. See [Dear ImGui backend guide](https://github.com/ocornut/imgui/blob/master/docs/BACKENDS.md).

### 4.2 Vulkan frame path

The most useful source file is [imgui_impl_vulkan.cpp](https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp), especially:

- `ImGui_ImplVulkan_RenderDrawData`;
- `ImGui_ImplVulkan_UpdateTexture`;
- `ImGui_ImplVulkan_SetupRenderState`;
- `CreateOrResizeBuffer`.

Its path is approximately:

```text
ImGui widget calls
  -> ImDrawList vertex/index/command arrays
  -> one contiguous vertex buffer per in-flight frame
  -> one contiguous index buffer per in-flight frame
  -> bind pipeline and projection constants
  -> for each ImDrawCmd:
       framebuffer-scale ClipRect
       vkCmdSetScissor
       bind texture descriptor only when changed
       vkCmdDrawIndexed
```

Important implementation details:

- It multiplies display size and clip rectangles by `FramebufferScale`, so logical coordinates and framebuffer pixels remain separate on Retina/high-DPI surfaces.
- It grows vertex/index buffers only when the current capacity is insufficient.
- It uploads all command-list geometry into contiguous mapped buffers.
- It keeps one buffer set for each swapchain image/in-flight frame.
- It changes descriptor sets only when the draw command's texture changes.
- It restores a full-frame scissor after UI rendering to reduce state leakage into the host engine.
- The backend supports Vulkan dynamic rendering.

The public backend header documents dynamic texture updates and sampled-image descriptor usage: [imgui_impl_vulkan.h](https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.h).

### 4.3 Font atlas

The current backend supports dynamic font-atlas texture updates instead of treating the atlas as a one-time startup upload. Renderer texture state is carried through `ImDrawData.Textures`; the Vulkan backend catches up on non-OK texture states before drawing.

The Vulkan texture object owns:

```text
VkImage
VkImageView
VkDeviceMemory
VkDescriptorSet
```

Texture upload uses a staging buffer, `vkCmdCopyBufferToImage`, image-layout transitions and a descriptor set. This is the smallest complete direct reference for ZEngine's first `R8_UNORM` atlas path.

### 4.4 What ZEngine should copy conceptually

- Separate platform and renderer responsibilities.
- Per-frame grow-only vertex/index buffers.
- Logical display scale applied to viewport and scissors at render time.
- Dirty texture updates processed before draw submission.
- Texture-change-aware batching.
- Full scissor restoration after the overlay pass.

### 4.5 What ZEngine should not copy

- Immediate-mode widget construction as the primary UI API.
- Rebuilding all widget commands every frame when ZUI already retains stable trees.
- A single global UI context.
- Text semantics limited to Dear ImGui's tool-focused model.

Dear ImGui is MIT licensed: [LICENSE.txt](https://github.com/ocornut/imgui/blob/master/LICENSE.txt).

## 5. egui and epaint

### 5.1 Architecture

egui's main UI layer produces `epaint` shapes. `epaint` turns 2D shapes and text into textured triangles. `egui-wgpu` paints those meshes through wgpu, which can select Vulkan on native platforms. See [egui architecture](https://github.com/emilk/egui/blob/main/ARCHITECTURE.md) and [egui integration guide](https://github.com/emilk/egui/blob/main/README.md).

```text
egui immediate UI
  -> epaint Shape
  -> tessellated ClippedPrimitive
  -> Mesh { vertices, indices, texture_id }
  -> egui-wgpu buffers and bind groups
  -> Vulkan through wgpu
```

### 5.2 Font and shaping path

The source in [epaint text/fonts.rs](https://github.com/emilk/egui/blob/main/crates/epaint/src/text/fonts.rs) shows that `FontsImpl` owns:

- font definitions and faces;
- a `TextureAtlas`;
- family/fallback caches;
- a recycled `harfrust::UnicodeBuffer` to avoid allocating a shaping buffer for every layout.

The atlas is part of the font collection, and the tessellator receives the same atlas to generate textured geometry.

This validates the P8B/P8C boundary:

```text
shape text once
  -> glyph ids and placements
  -> allocate/rasterize missing glyph coverage
  -> retain atlas coordinates
  -> tessellate glyph rectangles with UVs
```

### 5.3 Atlas implementation

The most useful source is [epaint texture_atlas.rs](https://github.com/emilk/egui/blob/main/crates/epaint/src/texture_atlas.rs).

Notable behavior:

- Atlas starts at the maximum configured width but only 32 pixels high, keeping the initial GPU upload small.
- Height grows as required.
- It uses row/shelf allocation.
- Each allocation receives one pixel of padding to prevent adjacent glyphs bleeding on lower-precision GPUs.
- A dirty rectangle tracks every modified region.
- `take_delta()` returns either a complete image or a partial dirty-region `ImageDelta`.
- Font textures use linear filtering.
- Overflow is explicit and signals that the atlas should be recreated.

This is directly applicable to ZEngine's planned atlas telemetry:

```text
AtlasUploadBytes == area(dirty rect) * bytesPerPixel
stable frame -> no dirty rect -> no upload
```

### 5.4 WGPU renderer

The relevant source is [egui-wgpu renderer.rs](https://github.com/emilk/egui/blob/main/crates/egui-wgpu/src/renderer.rs).

Its renderer separates:

- `update_texture` for full or partial texture deltas;
- `update_buffers` for mesh geometry;
- `render` for bind groups, scissors and indexed draws.

During render it:

- converts logical clip rectangles using pixels-per-point;
- skips zero-size clip regions;
- calls `set_scissor_rect` per clipped primitive;
- binds the mesh's texture bind group;
- binds pre-uploaded index and vertex slices;
- issues one indexed draw for the mesh.

### 5.5 Lessons and cautions

Good references:

- recycled shaping buffers;
- a CPU atlas with dirty deltas;
- one-pixel atlas padding;
- explicit pixels-per-point in clip conversion;
- separation of update and render phases.

Cautions:

- wgpu hides Vulkan resource details that ZEngine intentionally owns.
- egui is immediate-mode and Rust-native.
- egui has had its own text antialiasing quality discussions; its architecture is more valuable here than copying its exact rasterizer.

The project is dual licensed MIT OR Apache-2.0, as documented in its official repository.

## 6. RmlUi

### 6.1 Architecture

RmlUi is the closest high-level semantic comparison to ZUI. It is a retained XHTML/CSS-like library that owns layout, DOM, styling, controls and events, while the host owns the update loop, input and rendering.

The official readme states that RmlUi converts documents into vertices, indices, textures and draw commands for the host to render: [RmlUi readme](https://github.com/mikke89/RmlUi/blob/master/readme.md).

### 6.2 Render interface

The central contract is [RenderInterface.h](https://github.com/mikke89/RmlUi/blob/master/Include/RmlUi/Core/RenderInterface.h):

```text
CompileGeometry(vertices, indices)
RenderGeometry(compiled_geometry, translation, texture)
ReleaseGeometry
LoadTexture
GenerateTexture(premultiplied RGBA pixels)
ReleaseTexture
EnableScissorRegion
SetScissorRegion
SetTransform
layer/filter/mask operations
```

This is close to the boundary ZEngine needs between retained ZUI paint output and Vulkan execution. Particularly useful details:

- Geometry compilation is separated from rendering.
- Texture generation accepts premultiplied RGBA.
- Scissor regions are expressed in window coordinates.
- Advanced effects use explicit layer, mask and filter operations rather than leaking renderer objects into DOM nodes.

### 6.3 Font engine

RmlUi defaults to FreeType and allows the font engine to be replaced. Its font system supports on-demand character loading and fallback faces. Text-shaping hooks and language/direction inputs are present; the project includes an optional HarfBuzz shaping sample with right-to-left and fallback-font behavior. See [RmlUi font-engine changelog](https://github.com/mikke89/RmlUi/blob/master/changelog.md) and [font loading documentation](https://mikke89.github.io/RmlUiDoc/pages/lua_manual/fonts.html).

This is useful for ZEngine because it demonstrates a practical split:

```text
retained layout engine
  -> replaceable FontEngineInterface
  -> font metrics + shaped characters
  -> generated textures against the active RenderManager
```

### 6.4 Vulkan backend caveat

RmlUi ships an official Vulkan sample backend: [RmlUi_Renderer_VK.h](https://github.com/mikke89/RmlUi/blob/master/Backends/RmlUi_Renderer_VK.h).

However, the current sample backend is not a drop-in overlay for an engine that already owns Vulkan. In an official maintainer discussion, the project confirms that the backend creates its own instance and device; integrating with an existing engine currently requires modifying the renderer to accept externally owned Vulkan objects: [RmlUi Vulkan integration discussion](https://github.com/mikke89/RmlUi/discussions/811).

Therefore:

- RmlUi Core + a custom ZEngine `RenderInterface` is technically plausible.
- The bundled Vulkan backend should not be imported unchanged.
- Adopting the full RmlUi core would duplicate ZUI's retained tree, typed styles, events, Blazor projection and Agent semantics.

RmlUi is MIT licensed: [official license section](https://github.com/mikke89/RmlUi/blob/master/readme.md#license).

## 7. Slint

### 7.1 Architecture

Slint is a full declarative retained UI toolkit with a compiler, reactive properties, item tree, backend selector and multiple renderers. Its renderer choices include:

- software;
- FemtoVG;
- FemtoVG over WGPU, reaching Vulkan/Metal/D3D;
- Skia, including a Vulkan selection.

See [Slint backend and renderer documentation](https://docs.slint.dev/latest/docs/slint/guide/backends-and-renderers/backends_and_renderers/) and [renderer feature declarations](https://github.com/slint-ui/slint/blob/master/api/rs/slint/Cargo.toml).

### 7.2 What it demonstrates

- UI language/component semantics are kept above renderer choice.
- Window backend and renderer are selected independently.
- A software renderer remains available for constrained platforms and tests.
- Skia can provide a sophisticated Vulkan route at the cost of footprint and build complexity.
- WGPU provides a Vulkan route without exposing Vulkan directly to the UI runtime.
- Renderer capabilities and visual fidelity are not automatically identical.

The official renderer documentation explicitly notes that FemtoVG rendering quality can be suboptimal, while Skia is more sophisticated but heavier. This is an important warning: using Vulkan does not itself solve font quality.

### 7.3 Relevance to ZEngine

The most useful Slint lesson is package separation, not a renderer algorithm:

```text
declarative UI/runtime
  -> backend selector
      -> window adapter
      -> renderer adapter
```

ZEngine already follows a similar split with Zui.Core, Zui.Rendering, Zui.Backend.Blazor, Zui.Backend.Vulkan and Zui.Platform.Windows.

Direct adoption is unattractive because it would add another UI language/runtime and its licensing model is GPLv3, royalty-free with attribution conditions, or commercial. See [Slint LICENSE.md](https://github.com/slint-ui/slint/blob/master/LICENSE.md).

## 8. Qt Quick

### 8.1 Retained scene graph

Qt Quick uses a retained scene graph traversed by a renderer backed by Vulkan, Metal, Direct3D or OpenGL. Retention gives the renderer a complete primitive set before submission, enabling material batching, state sorting and obscured-primitive removal. See [Qt Quick Scene Graph](https://doc.qt.io/qt-6/qtquick-visualcanvas-scenegraph.html).

Qt's own example explains the target effect:

```text
10 list cells x (background + icon + text)
naive: 30 draws
scene graph: backgrounds batch + icons batch + text batch = 3 draws
```

The detailed renderer documentation describes its two main strategies as draw-call batching and retaining geometry on the GPU: [Qt Quick default renderer](https://doc.qt.io/qt-6.8/qtquick-visualcanvas-scenegraph-renderer.html).

### 8.2 Vulkan portability layer

Qt uses QRhi as an API abstraction. QRhi supports Vulkan, OpenGL ES, D3D11, D3D12, Metal and Null. Shaders are authored once, compiled through SPIR-V and packaged with translated forms/reflection data. See [QRhi documentation](https://doc.qt.io/qt-6/qrhi.html).

This resembles Impeller's offline shader pipeline, but conflicts with ZEngine's accepted decision to stay Vulkan-native rather than build a general RHI.

### 8.3 Text modes

`QSGTextNode` exposes three text strategies: [QSGTextNode documentation](https://doc.qt.io/qt-6/qsgtextnode.html).

| Mode | Method | Tradeoff |
|---|---|---|
| QtRendering | scalable distance field per glyph | fast and transformable, but more memory and large-size artifacts |
| NativeRendering | platform-specific glyph rendering | native appearance, poor under transforms |
| CurveRendering | GPU curve rasterization | lower texture-memory pressure, different performance tradeoff |

Qt also supports pre-generated distance-field glyph caches and exposes glyph preparation/upload timing: [Qt Distance Field Generator](https://doc.qt.io/qt-6/qtdistancefieldgenerator-index.html).

### 8.4 Lessons and cautions

Useful:

- Retained geometry and material-based batching.
- Multiple explicit text modes instead of pretending one rasterizer fits every use.
- Instrument glyph generation and upload separately.
- Keep the ability to prewarm common glyphs.

Not recommended:

- Pulling Qt Quick into the engine.
- Replacing Vulkan-native contracts with QRhi.
- Adopting QML beside the C# UI DSL.

Qt is dual licensed under open-source and commercial terms; LGPL/GPL obligations must be evaluated carefully: [Qt licensing](https://doc.qt.io/qt-6/licensing.html).

## 9. Flutter Impeller

### 9.1 Architecture

Impeller is Flutter's portable rendering runtime with Vulkan, Metal and OpenGL backends. Its explicit objectives include predictable performance, build-time shader compilation/reflection, resource labeling, explicit caching and concurrency. See [Impeller README](https://github.com/flutter/engine/blob/main/impeller/README.md) and [Flutter Impeller documentation](https://docs.flutter.dev/perf/impeller).

```text
Flutter retained UI/render objects
  -> DisplayList
  -> Impeller display-list dispatcher
  -> backend-agnostic renderer
  -> Vulkan / Metal / GLES backend
```

### 9.2 Text boundary

Impeller deliberately does not shape text. Flutter uses SkParagraph for text layout and shaping. Impeller's Typographer receives already shaped glyph runs and uses a platform/backend implementation to rasterize glyphs into texture atlases.

The source-tree responsibilities are explicit:

```text
SkParagraph
  -> shaped glyph runs
  -> Impeller Typographer interface
  -> platform-specific glyph raster backend
  -> texture atlas
  -> glyph-run rendering
```

This is the strongest architectural confirmation of the planned ZEngine split:

```text
P8B ITextShaper/GlyphRun
  -> P8C IGlyphRasterizer/GlyphAtlas
  -> Vulkan glyph instances
```

### 9.3 Other useful ideas

- All shaders are authored and compiled offline.
- Generated bindings make shader-interface changes compile-time failures.
- Pipeline state is prepared before frame workloads.
- Graphics resources are labeled for capture and diagnostics.
- Backends stay private below the portable renderer.

Impeller is too large and Flutter-specific to embed, but its project boundaries are highly relevant. Flutter is BSD-3-Clause licensed: [Flutter license](https://github.com/flutter/flutter/blob/master/LICENSE).

## 10. NoesisGUI

### 10.1 Architecture

NoesisGUI is a retained XAML UI runtime aimed at games and real-time applications, with C++ and .NET APIs. Its rendering architecture allows UI and render threads to work in parallel: [Noesis rendering architecture](https://www.noesisengine.com/docs/Gui.Core.RenderingTutorial.html).

```text
UI thread Update
  -> pending retained-tree changes
render thread UpdateRenderTree
  -> RenderOffscreen for effects/layers
  -> host renders 3D scene
  -> Render UI onscreen
```

This is a strong reference for eventually moving ZUI paint compilation away from the submission thread while retaining a deterministic frame boundary.

### 10.2 RenderDevice and texture lifecycle

Noesis's RenderDevice API includes:

- dynamic vertex/index mapping;
- texture creation and partial updates;
- explicit `BeginUpdatingTextures` / `EndUpdatingTextures` outside render passes;
- render-target and layer handling;
- batch shader/vertex-format selection;
- tile/scissor hints;
- premultiplied blending;
- constant-buffer hashes to avoid redundant uploads.

See [RenderDevice API notes](https://www.noesisengine.com/docs/Gui.Core.RenderDeviceNotes.html).

The renderer exposes configurable glyph-cache dimensions, and its integration guide supports PPAA, LCD/subpixel rendering and linear/gamma modes. See [Noesis rendering tutorial](https://www.noesisengine.com/docs/Gui.Core.RenderingTutorial.html) and [C++ SDK switches](https://www.noesisengine.com/docs/Gui.Core.GettingStartedNative.html).

### 10.3 Relevance and boundary

Useful:

- UI/render thread snapshot boundary.
- Offscreen effects before onscreen composition.
- Dynamic texture update phase outside render passes.
- Hashed uniform data and batch-oriented RenderDevice.
- Explicit glyph-cache sizing and text rendering modes.

Not recommended for immediate adoption:

- It would replace ZUI semantics with XAML.
- Core implementation is not fully open for unrestricted source study.
- Production use is governed by commercial/evaluation terms: [Noesis licensing](https://www.noesisengine.com/licensing.php).

## 11. Recommendations for ZEngine P8

### 11.1 P8B shaped text

Use these references in order:

1. Impeller Typographer boundary: shaping is outside the renderer; renderer accepts GlyphRun.
2. RmlUi FontEngineInterface: replaceable metrics/shaping/fallback boundary tied to an active render manager.
3. egui `FontsImpl`: reuse shaping buffers and own font fallback caches.
4. Qt text modes: distinguish fixed-size native quality from scalable/transformed text.

Recommended ZEngine contracts remain:

```text
FontFaceId
GlyphId
FontMetrics
GlyphPlacement
GlyphRun
ITextShaper
IFontFallbackResolver
IGlyphRasterizer
```

Do not key the future atlas by Unicode Rune alone.

### 11.2 P8C glyph atlas and Vulkan batching

Use these sources in order:

1. Dear ImGui Vulkan backend for the minimum complete Vulkan image/staging/descriptor/buffer/scissor path.
2. egui `TextureAtlas` for growing storage, one-pixel padding, dirty rectangles and partial upload deltas.
3. egui-wgpu renderer for update-texture/update-buffer/render phase separation.
4. Qt Quick renderer for material batching and retained GPU geometry.
5. Noesis RenderDevice for explicit texture-update phases and upload hashing.

Recommended ZEngine path:

```text
GlyphRun
  -> allocate missing glyph in R8 atlas
  -> dirty rectangle upload before Frame.Ui render pass
  -> one GlyphInstance per placed glyph
  -> group by atlas page + clip + material
  -> <= 8 Editor UI draws
```

### 11.3 P8D shapes and effects

- Follow Qt Quick's retained material batching, not per-node submission.
- Follow Noesis's offscreen/onscreen split for effects requiring intermediate surfaces.
- Keep simple rounded rectangles and borders analytic in one shader.
- Introduce layers only for effects that cannot be expressed analytically.
- Keep premultiplied-alpha texture and blend contracts, consistent with RmlUi and Noesis.

## 12. Adoption decision matrix

| Option | Decision | Reason |
|---|---|---|
| Replace ZUI with Dear ImGui | Reject | Loses retained portable Component/DOM/Blazor/Agent semantics |
| Add Dear ImGui as optional debug overlay | Future spike | Useful mature tooling overlay; keep isolated from game UI contracts |
| Replace ZUI with egui | Reject | Rust/wgpu runtime and immediate model duplicate existing contracts |
| Embed RmlUi core | Defer | Closest retained model, but duplicates ZUI; only reconsider if CSS/layout scope becomes unmanageable |
| Import RmlUi Vulkan backend | Reject | Currently owns its own Vulkan instance/device and is not an engine overlay without modification |
| Replace ZUI with Slint | Reject | Adds another language/runtime/licensing boundary; renderer quality still varies by backend |
| Embed Qt Quick | Reject | Footprint, licensing and QRhi conflict with current architecture |
| Embed Flutter/Impeller | Reject | Too large and Flutter-specific; use as architecture reference |
| License NoesisGUI | Business option only | Strong product, but changes UI model and creates commercial dependency |
| Continue ZUI P8 using these references | Recommend | Preserves accepted architecture while borrowing proven implementation patterns |

## 13. Concrete source-reading order

For the engineer implementing the next stages:

1. [Dear ImGui Vulkan backend implementation](https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp)
   - Find `ImGui_ImplVulkan_UpdateTexture`.
   - Find `ImGui_ImplVulkan_RenderDrawData`.
   - Study per-frame buffers, `FramebufferScale`, scissors and descriptor changes.
2. [egui texture atlas](https://github.com/emilk/egui/blob/main/crates/epaint/src/texture_atlas.rs)
   - Study `new`, `allocate`, `take_delta`, padding and overflow.
3. [egui font collection](https://github.com/emilk/egui/blob/main/crates/epaint/src/text/fonts.rs)
   - Study `FontsImpl`, `TextureAtlas` ownership and recycled `harfrust` buffer.
4. [egui-wgpu renderer](https://github.com/emilk/egui/blob/main/crates/egui-wgpu/src/renderer.rs)
   - Study `update_texture`, `update_buffers`, `render` and scissor conversion.
5. [RmlUi RenderInterface](https://github.com/mikke89/RmlUi/blob/master/Include/RmlUi/Core/RenderInterface.h)
   - Study geometry, texture, scissor, transform, layer and filter boundaries.
6. [Impeller architecture](https://github.com/flutter/engine/blob/main/impeller/README.md)
   - Study `typographer`, `renderer`, backend privacy and offline shaders.
7. [Qt Quick default renderer](https://doc.qt.io/qt-6.8/qtquick-visualcanvas-scenegraph-renderer.html)
   - Study batching, retained geometry and glyph-cache instrumentation.
8. [Noesis rendering architecture](https://www.noesisengine.com/docs/Gui.Core.RenderingTutorial.html)
   - Study UI/render thread handoff and offscreen/onscreen phases.

## 14. Pause boundary

Per the current instruction, work stops at this research document.

No P8B font contracts, DirectWrite bindings, Vulkan image/descriptor code, glyph atlas, third-party package, submodule or commercial SDK should be added until this survey is reviewed and a direction is explicitly selected.
