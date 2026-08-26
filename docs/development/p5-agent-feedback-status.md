# P5 Agent Feedback Status

Status: P5 Agent Feedback Lab accepted on local MCP, browser semantics and Win11 Vulkan capture.

## Implemented

- Explicit Observe, EngineInput, EngineMutation, Capture and NativeInput permissions.
- Typed action catalog with owner plugin/generation and reversible registration.
- Typed observation catalog with reversible registration.
- Thread-safe enqueue and main-thread frame `Drain` boundary.
- ActionReceipt with requested/started/completed frame, duration, deltas, logs and optional PNG.
- Permission failures and action exceptions become structured failed receipts.
- UI semantic tree with shared NodeId, role, label, bounds and action names.
- `ui.input` and `ui.click` invoking the same delegates used by Blazor and Native UI.
- Strongly typed C# scenario DSL.
- Vulkan swapchain `TRANSFER_SRC` negotiation and a live `Frame.Capture` RenderGraph pass.
- Timeline-synchronized BGRA readback without `vkDeviceWaitIdle`.
- Pure C# zlib/CRC PNG encoding.
- `frame.capture` action returns PNG in the structured receipt.

## Validation evidence

- A queued mutation stays incomplete and leaves engine state unchanged until `Drain(42)`, then returns started/completed frame 42.
- `ui.input` and `ui.click` use semantic NodeIds, update the shared LoginCard to `Agent用户 已点击登录 1 次`, and preserve the textbox NodeId.
- Scenario DSL and action/observation registration disposal are deterministic.
- EngineInput permission cannot authorize NativeInput.
- Hidden Vulkan capture test inserts TransferSource copy, returns a valid IHDR with exact swapchain dimensions and has zero validation errors.
- Native UiLab readback produced a 144,841-byte PNG; direct image inspection shows the complete Chinese UI and scene triangle rather than a black frame.
- Official MCP C# SDK 2.2.0 Streamable HTTP server completes initialize, tools/list, engine_ui_tree, engine_input, engine_click, engine_wait and engine_capture.
- MCP semantic smoke used NodeId 16553343936875172595 for input and 856286638151737023 for click; receipts completed on the next main-loop frame and the button became `Click 1` at UI generation 3.
- MCP `engine_capture` returns protocol-native `ImageContentBlock` (`image/png`) plus structured completed-frame metadata.
- AgentHost is a separate executable; no listener exists unless it is explicitly launched.

## Deferred after P5

- Production Native UiLab can wire its real `FrameCaptureAgentAdapter` into AgentHost through the same registry; the standalone protocol smoke uses a 2x2 capture provider.
- Web DOM screenshot uses browser-host capture while semantic actions remain identical; no desktop-wide capture permission is implied.
