# ZEngine：统一 C# UI、Blazor DOM/CSS 与 Vulkan 游戏引擎蓝图

> 状态：Draft，等待审阅  
> 方向确立日期：2026-08-26  
> 语言与运行时：.NET 11 Preview 7、C# 15 Preview、CoreCLR/JIT  
> UI 目标：同一套强类型 C# Component、DOM-like Node、State、Event、Theme 和 CSS Style 模型  
> Web 后端：Blazor WebAssembly + 真实 DOM/CSS  
> 原生后端：Vulkan 1.4；当前唯一图形验收环境为 Windows 11 x64、AMD Radeon RX 9070 GRE  
> 未来适配目录：Browser、Windows Arm64、Linux x64/Arm64、Android Arm64、macOS x64/Arm64、iOS Arm64  
> 工作名称：ZEngine  
> 目标仓库：C:\Users\lionheart\RiderProjects\zengine
> 兼容原则：基础包保证 Portable UI；Web/Windows/Linux/Android/Apple 增强包按项目依赖提供专属能力

## 0. 文档目的与方向重置

本蓝图定义一个充分依赖现代 C# 的游戏引擎与统一 UI 平台。UI 组件、状态、事件、主题、DOM-like 节点和 typed CSS 模型在各端共享；Web 通过 Blazor WebAssembly 映射到真实 DOM/CSS，原生平台通过 Vulkan 映射到计算样式、布局、文字和绘制图元。

新的首要问题是：

1. 如何让同一份纯 C# UI 组件在 Web DOM/CSS 和原生 Vulkan 上保持基础语义一致。
2. 如何用基础包、Portable 增强包和平台专属增强包代替组件级平台注解。
3. 如何完整表达 DOM/CSS API，同时明确 Web Full、Portable 和 Native Extended 能力边界。
4. 如何生成并封装自己的 Vulkan C# 原始绑定，而不是把第三方托管绑定变成公共 API。
5. 如何建立适合现代 GPU 的渲染图、资源生命周期、着色器和多线程提交系统。
6. 如何建立强类型、高吞吐、可热重载的 ECS、场景和资源系统。
7. 如何让游戏模块、UI 增强包、编辑器扩展和插件都支持开发期热重载。
8. 如何支持插件依赖插件，并在依赖更新时保持确定的卸载、重载和回滚顺序。
9. 如何把强类型、清晰 DSL、CSS 可识别性与低分配、高帧率结合。

旧的 WebGPU 全栈蓝图已归档为：

[2026-08-26-zui-webgpu-fullstack-blueprint-rejected.md](../archive/rejected/2026-08-26-zui-webgpu-fullstack-blueprint-rejected.md)

旧文档只作为决策历史，不再是实现依据。

## 1. 总体结论

### 1.1 可行性

该方向可行，前提是把兼容目标分成包级能力，而不是要求每个后端实现完全相同的平台细节：

- 共享 UI 包使用普通 net11.0 C#，包含 Component、State、Event、DOM-like Node、Theme、typed CSS 和 Agent semantics。
- Web Host 使用 Blazor WebAssembly，通过 RenderTreeBuilder 交给 Blazor 批量更新真实 DOM，并让浏览器执行完整 CSS。
- 原生 Host 使用 CoreCLR/JIT、自己的 CSS computed-style/layout/text 系统和 Vulkan renderer。
- Portable 增强包必须在 Web 与原生后端都有实现。
- Web、Windows、Linux、Android 和 Apple 专属增强包由项目引用决定可见 API，不在组件上写平台 attribute。

原生路径仍可充分使用：

- CoreCLR JIT、Tiered Compilation 和 Dynamic PGO。
- collectible AssemblyLoadContext 和动态插件。
- unsafe、delegate* unmanaged、Span、ref struct 和硬件 intrinsics。
- Roslyn Incremental Generator、Analyzer、PDB、EventPipe 和 Rider 调试器。

代价也很明确：我们同时建设 UI 平台和游戏引擎内核。最困难的部分会是：

- Vulkan 同步、资源生命周期和设备差异。
- 可卸载插件的类型身份与依赖图。
- 热重载期间世界状态、任务和 GPU 资源的迁移。
- Portable CSS 的 cascade、computed value、layout、text、input 和两后端一致性。
- Web Blazor RenderTree 与原生 retained tree 的稳定 NodeId 和状态对应。
- 保持每帧低分配而又不把公共 API 变成难读的 unsafe 代码。

### 1.2 推荐架构

~~~text
Shared C# Packages
  ├─ Engine Core / ECS / Scene
  ├─ Zui Core / Basic / Portable Enhancements
  ├─ Component / State / Event
  ├─ DOM-like Node / typed CSS / Theme
  └─ Agent Semantics
           │
     ┌─────┴─────────────────────┐
     ▼                           ▼
Web Host                     Native Host
Blazor WebAssembly           CoreCLR/JIT
  ├─ RenderTreeBuilder         ├─ Plugin Runtime
  ├─ Real DOM                  ├─ CSS Computed Style
  ├─ Browser CSS/Layout        ├─ Native Layout/Text/Input
  ├─ Web Enhancement Packages ├─ Vulkan UI Renderer
  └─ Web Agent Adapter         └─ Native Agent Adapter
                                  │
                                  ▼
                            Vulkan Game Renderer
~~~

### 1.3 核心选择

| 领域 | 推荐 |
|---|---|
| 共享 UI | Component、State、Event、DOM-like Node、typed CSS 和 Theme |
| 能力边界 | Basic 包 + Portable 增强包 + 平台专属增强包 |
| Web | Blazor WebAssembly、RenderTreeBuilder、真实 DOM/CSS |
| 原生运行时 | CoreCLR/JIT 自包含发布 |
| 移动 AOT | 作为 Runtime Capability；不承诺动态插件 |
| Vulkan 绑定 | 从 Khronos vk.xml 生成自有 C# Raw Binding |
| 图形抽象 | Vulkan-native façade，不建立假通用 RHI |
| Vulkan 基线 | Raw Binding 跟踪最新 Registry；当前 Win11 路径直接以 Vulkan 1.4 为基线 |
| 窗口平台 | 当前只验证 Win32；其他平台只规划 adapter 和 RID 目录 |
| ECS | archetype + chunk SoA，生成强类型 Query |
| 调度 | 数据访问声明驱动的 Job Graph |
| 渲染 | Render Graph + Render Extraction + 多线程命令录制 |
| 着色器 | HLSL 通过 DXC 编译 SPIR-V，生成强类型绑定 |
| 插件 | Contract Assembly + collectible Runtime ALC |
| 插件依赖 | 有向无环图、SemVer 范围、锁文件、反向闭包重载 |
| 热重载 | immutable generation + shadow copy + 帧安全点原子切换 |
| Portable UI | change-driven retained core + typed DOM/CSS C# DSL |
| Web UI | 浏览器 DOM/CSS；允许 Web 专属完整 CSS/DOM 增强包 |
| Native UI | computed style + layout/text + Vulkan primitive renderer |
| 编辑器 UI | Tool UI 可复用 Native renderer，也可由专属增强包扩展 |
| Agent | 引擎内强类型观察/操作 API + 本地 MCP 适配，返回截图和结构化反馈 |
| 发布 | 主机、引擎程序集、插件包、内容包和原生运行库 |

### 1.4 Runtime 与 AOT 边界

NativeAOT 与以下核心目标冲突：

- 在运行时加载新的插件程序集。
- 卸载并替换 AssemblyLoadContext。
- 开发期生成并加载新代码。
- 使用完整反射和 PDB 诊断插件泄漏。
- 允许插件携带独立托管依赖。

因此原生桌面：

- Debug 和 Release 都运行 CoreCLR。
- Release 使用 self-contained publish。
- 可评估 ReadyToRun 改善启动，但不能破坏插件 JIT 和热重载。
- 引擎核心代码仍保持低反射、低分配和可分析，不因为有 JIT 就忽略性能纪律。

Web 使用 .NET WebAssembly/Blazor，不具备与桌面 collectible ALC 完全相同的插件模型；Web 增强包在构建期进入应用 bundle。iOS 等 AOT 环境也采用静态插件或重新部署。Component、State、CSS 和 Event 契约共享，但动态加载属于 Runtime Capability。

## 2. 明确的非目标

首版不做：

- 在原生端完整复刻全部 W3C DOM/CSS 或 Chrome 历史兼容行为。
- 承诺 Web 与 Vulkan 后端像素级完全一致。
- 把 Web Full CSS/DOM 组件伪装成 Portable 组件。
- 让业务代码通过逐属性 JS interop 绕过 Blazor直接修改其管理的 DOM。
- 因为 DOM UI 可用就声称 Web 已拥有 Vulkan 3D renderer；Web 3D 需要未来独立 WebGPU backend。
- 在 WebAssembly 或 iOS AOT 上承诺桌面同等级动态插件热重载。
- Direct3D、Metal、OpenGL 等多后端通用 RHI。
- 主机内不可信插件安全沙箱。
- 运行时 C# 源码解释器。
- 自研物理、音频、导航、动画、网络等所有子系统的完整产品版。
- 首版即实现 AAA 全功能编辑器。
- 首版即实现 GPU-driven 全场景、虚拟几何和实时全局光照。
- 用字符串名称作为组件、资源、服务和事件的主要公共契约。
- 允许插件任意启动无法追踪的线程、静态事件或裸 GPU 资源。
- 承诺所有插件 API 变化都能无状态损失地原位热重载。
- 为了语法炫技而在每帧热路径构建大量委托、反射对象或临时集合。

## 3. 平台与运行时基线

### 3.1 首发矩阵

| 项目 | 当前可验证 | 未来只规划 adapter |
|---|---|---|
| UI 编译模型 | 共享 net11.0 C# 契约 | Blazor WebAssembly 与各原生 Host |
| Web UI | 尚未建立样例 | Browser Wasm + Blazor DOM/CSS |
| 原生操作系统 | Windows 11 x64 | Windows Arm64、Linux、Android、macOS、iOS |
| GPU | AMD Radeon RX 9070 GRE | 不预设厂商 |
| Vulkan | Loader 1.4.341；设备 1.4.349 | 运行时协商平台可用版本和 portability 能力 |
| 驱动 | AMD 26.8.1，Conformance 1.4.3.3 | 以目标设备实测为准 |
| 显存 | Vulkan heap 显示约 11.94 GiB device-local | 不硬编码 |
| CPU | x64；AVX2/更高 SIMD 按运行时探测 | Arm64 |
| .NET | 11.0.100-preview.7.26381.103 | 平台实际支持的 .NET 11 runtime/AOT 形态 |
| C# | C# 15 Preview | 主体源码保持一致 |
| 开发 IDE | Rider | VS Code / Visual Studio |
| 发布 | win-x64 self-contained CoreCLR | 仅建立 RID 构建入口，不声称通过 |

Windows 优先不是把 Win32 类型泄漏到引擎公共层。平台抽象从第一天存在，但只实现真正需要的边界：

- Window。
- Display 和 DPI。
- Raw keyboard、mouse、gamepad。
- IME、clipboard、cursor。
- File dialog。
- High-resolution timer。
- Dynamic library。
- Vulkan surface。

当前阶段只有 Win11 x64 + RX 9070 GRE 可以写入验收证据。未来平台目录的存在仅说明依赖方向和扩展位置，不代表编译、运行、图形正确性、热重载或发布已经验证。

### 3.2 运行时能力等级

平台不强求完全相同的运行时能力：

| Runtime Tier | 目标 | UI Backend | 动态插件 / 热重载 |
|---|---|---|---|
| BrowserWasm | Web Browser | Blazor DOM/CSS | 构建期增强包；Blazor/Wasm Hot Reload |
| DesktopDynamic | Windows、Linux、macOS CoreCLR/JIT | Vulkan | 动态插件与进程内热重载 |
| MobileDynamic | Android CoreCLR，需目标环境实测 | Vulkan | 受平台政策限制 |
| MobileAot | iOS 和其他只允许 AOT 的目标 | MoltenVK/Vulkan portability | 静态插件；重新部署 |

引擎主体的 ECS、Render Graph、UI、资产和插件契约源码保持一致；动态加载和热重载属于 Runtime Capability。iOS 不能因为源码是 C# 就假设具备 AssemblyLoadContext 动态插件能力。

### 3.3 CoreCLR 配置

初始策略：

- Tiered Compilation 开启。
- Dynamic PGO 开启。
- Workstation GC 与 Server GC 在真实场景中比较，不先武断固定。
- 游戏运行阶段评估 SustainedLowLatency。
- 不默认使用 NoGCRegion。
- ReadyToRun 只通过启动与稳态基准决定。
- Debug 启用完整符号、验证和追踪。
- Release 关闭 Vulkan Validation，但保留可按启动参数开启的诊断包。

### 3.4 允许充分使用的 C# 能力

引擎底层允许：

- unsafe。
- pointer 和 fixed。
- delegate* unmanaged。
- UnmanagedCallersOnly。
- NativeMemory。
- MemoryMarshal 和 Unsafe。
- stackalloc。
- InlineArray。
- Span 和 ref struct。
- ref field、ref return 和 scoped。
- 泛型静态抽象接口。
- System.Runtime.Intrinsics。

但 unsafe 只能集中在边界清楚的项目：

- Vulkan.Raw。
- NativeInterop。
- Platform.Win32。
- SIMD 优化实现。

游戏逻辑、UI 和大多数插件默认使用安全强类型 API。

## 4. 解决方案总目录

推荐结构：

~~~text
zadmin/
  global.json
  Directory.Build.props
  Directory.Build.targets
  Directory.Packages.props
  zengine.slnx

  eng/
    build.ps1
    run-editor.ps1
    run-sandbox.ps1
    test.ps1
    publish.ps1
    capture-frame.ps1

  docs/
    architecture/
      zengine-unified-csharp-ui-blazor-vulkan-blueprint.md
      decisions/
    archive/
      rejected/
    protocols/
    performance/

  src/
    Foundation/
      ZEngine.Core/
      ZEngine.Collections/
      ZEngine.Diagnostics/
      ZEngine.Jobs/
      ZEngine.Mathematics/
      ZEngine.Serialization/

    Native/
      ZEngine.NativeInterop/
      ZEngine.Vulkan.Registry/
      ZEngine.Vulkan.Generator/
      ZEngine.Vulkan.Raw/
      ZEngine.Platform.Abstractions/
      ZEngine.Platform.Win32/
      ZEngine.Platform.WindowsArm64/
      ZEngine.Platform.Linux/
      ZEngine.Platform.Android/
      ZEngine.Platform.Apple.Common/
      ZEngine.Platform.MacOS/
      ZEngine.Platform.iOS/

    Graphics/
      ZEngine.Graphics/
      ZEngine.Graphics.Vulkan/
      ZEngine.RenderGraph/
      ZEngine.Shaders/
      ZEngine.Rendering/
      ZEngine.Rendering.UI/
      ZEngine.Graphics.Vulkan.Portability/

    World/
      ZEngine.Ecs/
      ZEngine.Scene/
      ZEngine.Assets/
      ZEngine.Input/

    UI/
      Core/
        Zui.Core/
        Zui.Dom/
        Zui.Css/
        Zui.Events/
        Zui.Semantics/
        Zui.Tooling/

      Basic/
        Zui.Basic/
        Zui.Basic.Controls/
        Zui.Basic.Layout/
        Zui.Basic.Text/

      Enhancements/
        Portable/
          Zui.Enhancements.Forms/
          Zui.Enhancements.Animation/
          Zui.Enhancements.Virtualization/
          Zui.Enhancements.DataGrid/
        Web/
          Zui.Enhancements.Web.Dom/
          Zui.Enhancements.Web.AdvancedCss/
          Zui.Enhancements.Web.Forms/
          Zui.Enhancements.Web.Media/
        Native/
          Zui.Enhancements.Native.Gamepad/
          Zui.Enhancements.Native.WorldSpace/
        Windows/
          Zui.Enhancements.Windows.Shell/
          Zui.Enhancements.Windows.Fluent/
        Linux/
          Zui.Enhancements.Linux.Desktop/
        Android/
          Zui.Enhancements.Android.Material/
        Apple/
          Zui.Enhancements.Apple.Cupertino/

      Backends/
        Zui.Backend.Blazor/
        Zui.Backend.Vulkan/
        Zui.Backend.Vulkan.Portability/

      Sdk/
        Zui.Package.Abstractions/
        Zui.Package.Runtime/
        Zui.Package.Tooling/
        Zui.Sdk/

    Plugins/
      ZEngine.Plugin.Abstractions/
      ZEngine.Plugin.Runtime/
      ZEngine.Plugin.Tooling/
      ZEngine.Plugin.Sdk/

    Agent/
      ZEngine.Agent.Abstractions/
      ZEngine.Agent.Protocol/
      ZEngine.Agent.Runtime/
      ZEngine.Agent.Transport.Ipc/
      ZEngine.Agent.Transport.Mcp/
      ZEngine.Agent.Testing/

    Host/
      ZEngine.Runtime/
      ZEngine.Host/
      ZEngine.DevHost/
      ZEngine.Editor/
      ZEngine.AgentHost/
      ZEngine.Web/
      ZEngine.Windows/
      ZEngine.Linux/
      ZEngine.Android/
      ZEngine.MacOS/
      ZEngine.iOS/

  plugins/
    Engine/
      RenderFeatures/
      AssetImporters/
    Samples/
      PhysicsSample/
      GameplaySample/
      DependentPluginSample/

  samples/
    Triangle/
    RenderGraphLab/
    EcsLab/
    UiLab/
    UiPortableLab/
    UiWebBlazorLab/
    UiVulkanLab/
    UiEnhancementPackageLab/
    PluginReloadLab/
    DependentPluginReloadLab/
    EditorLab/

  tests/
    Unit/
    Integration/
    Performance/
    Gpu/
    Reload/
    Agent/

  tools/
    ZEngine.Content/
    ZEngine.ShaderCompiler/
    ZEngine.PluginPack/
    ZEngine.FrameReplay/
~~~

### 4.1 不创建空泛层

不预建 Common、Utils、Managers 或 Abstractions 大杂烩。每个项目必须隔离真实边界：

- 原生 ABI。
- 线程与内存模型。
- Vulkan 设备。
- 插件 ALC。
- 编辑器和运行时进程。
- 构建期与运行期。

如果两个项目总是同步修改且没有独立发布、测试或依赖意义，应合并。

## 5. 引擎内核与生命周期

### 5.1 EngineHost

主机建立少量长期存在的稳定服务：

~~~text
EngineHost
  ├─ PlatformHost
  ├─ DiagnosticsHub
  ├─ JobScheduler
  ├─ PluginRuntime
  ├─ AssetRuntime
  ├─ WorldManager
  ├─ RenderRuntime
  ├─ UiRuntime
  └─ ReloadCoordinator
~~~

这些类型位于 Default AssemblyLoadContext，不随插件重载。

### 5.2 生命周期阶段

~~~text
Created
  → Configured
  → NativeInitialized
  → PluginsResolved
  → PluginsLoaded
  → ContentMounted
  → Running
  → Quiescing
  → Stopped
  → Disposed
~~~

所有注册都返回或自动进入 OwnerScope。OwnerScope 负责：

- Job。
- Event subscription。
- Service export。
- ECS system。
- Render pass factory。
- Asset mount。
- UI panel。
- Command。
- File watcher。
- GPU logical resource。
- Native handle。

Scope 释放顺序可预测，并记录所有未释放项的来源位置。

### 5.3 主循环

~~~text
Poll platform events
  → Apply pending generation at safe point
  → Sample input
  → Fixed simulation ticks
  → Variable gameplay update
  → Flush structural ECS commands
  → Extract RenderWorld
  → Update UI
  → Compile Render Graph
  → Record command buffers in parallel
  → Submit and present
  → Retire completed CPU/GPU resources
  → Publish frame diagnostics
~~~

### 5.4 固定与可变时间

- Physics 和确定性逻辑使用固定 tick。
- 渲染使用当前帧 delta 和固定 tick 间的 interpolation alpha。
- 最大 catch-up tick 有上限，避免 spiral of death。
- 暂停、单帧、慢动作和录制重放由 TimeDomain 控制。
- 插件不能直接读取全局 Stopwatch 作为游戏时间；使用注入的 TimeDomain。

## 6. 自有 Vulkan Raw Binding

### 6.1 为什么从 vk.xml 生成

Khronos 明确维护 vk.xml 作为机器可读 Vulkan API Registry，并支持为其他语言生成绑定。ZEngine 使用固定版本的 vk.xml 生成自己的 C# 原始层：

~~~text
Khronos vk.xml
  → Registry parser
  → Normalized Vulkan model
  → C# binding generator
  → ZEngine.Vulkan.Raw.g.cs
  → ABI layout tests
~~~

不手写数千个 struct、enum 和函数签名，也不直接 fork Silk.NET 的公共 API。

### 6.2 Raw 层原则

ZEngine.Vulkan.Raw：

- 名称尽量忠实 Vulkan。
- struct 使用显式或顺序布局并做 sizeof、offset 验证。
- handle 是不同的强类型 readonly struct，不统一成 nint。
- Bool32、DeviceSize、Flags 等保持 Vulkan 语义。
- 命令通过 instance/device dispatch table 中的函数指针调用。
- 不在每次调用做字符串、数组或 delegate marshalling。
- 不拥有资源生命周期。
- 不隐藏 VkResult。
- 不抛出业务异常。
- 生成 Core、KHR、EXT 和厂商扩展能力表。

示意：

~~~csharp
public readonly struct VkDevice(ulong value)
{
    public ulong Value { get; } = value;
    public bool IsNull => Value == 0;
}

public unsafe readonly struct VkDeviceDispatch
{
    public readonly delegate* unmanaged<VkDevice, VkBufferCreateInfo*, VkAllocationCallbacks*, VkBuffer*, VkResult>
        vkCreateBuffer;
}
~~~

### 6.3 函数加载

只静态导入 Vulkan loader 的最小入口：

- vkGetInstanceProcAddr。

其余函数：

- GlobalDispatch。
- InstanceDispatch。
- DeviceDispatch。

都由 vkGetInstanceProcAddr 或 vkGetDeviceProcAddr 获得 delegate* unmanaged。这样：

- 调用路径接近原生函数指针。
- 不为每个命令生成 P/Invoke stub。
- dispatch table 与具体 instance/device 明确绑定。
- 多设备测试不会依赖全局 current device。

### 6.4 Registry 版本锁定

仓库记录：

- vk.xml 来源 commit。
- Vulkan-Headers 版本。
- 生成器版本。
- 已启用平台与扩展。
- 生成代码 hash。

升级 Registry 时运行：

- XML schema validation。
- 生成 diff。
- ABI layout tests。
- Windows Vulkan SDK smoke test。
- Validation Layer 全套样例。

### 6.5 第三方绑定的地位

Silk.NET 和 Vortice.Vulkan 仅作为：

- API 覆盖率对照。
- ABI 测试对照。
- 原型阶段排错参考。

它们不出现在 ZEngine 公共 API，也不成为正式渲染层的运行时依赖。

## 7. Vulkan 设备与资源层

### 7.1 不建立虚假的通用 RHI

项目首版只支持 Vulkan，因此内部可以直接使用：

- Queue family。
- Pipeline stage 和 access mask。
- Image layout。
- Descriptor。
- Timeline semaphore。
- Dynamic rendering。
- Synchronization 2。

但游戏插件不应长期保存裸 VkDevice、VkImage 或 VkPipeline。引擎提供 Vulkan-native 的资源 façade，用强类型 generational handle 表达所有权。

### 7.2 强类型句柄

~~~csharp
public readonly record struct GpuHandle<TResource>(
    uint Index,
    uint Generation)
    where TResource : class, IGpuResource;

public sealed class GpuBuffer : IGpuResource { }
public sealed class GpuImage : IGpuResource { }
public sealed class GpuSampler : IGpuResource { }
public sealed class GpuPipeline : IGpuResource { }
~~~

句柄：

- 复制便宜。
- 检查 generation，避免 use-after-free。
- 不直接 root 插件对象。
- 能在热重载和资源替换时保持逻辑身份。
- Debug 显示 owner、创建位置、内存、最后使用 timeline。

### 7.3 资源描述

~~~csharp
var vertexBuffer = device.CreateBuffer<Vertex>(
    new BufferDesc
    {
        Count = vertices.Length,
        Usage = BufferUsage.Vertex | BufferUsage.TransferDestination,
        Memory = MemoryIntent.DeviceLocal
    });
~~~

泛型 T 表示元素布局并用于字节大小、调试和 shader 验证。运行时 usage 仍是 flags，因为资源往往拥有多个用途；不使用组合爆炸的 marker generic。

### 7.4 内存分配

Vulkan 原生分配不允许每个 buffer 调一次 vkAllocateMemory。首阶段比较两个实现：

1. 自有最小 buddy 或 TLSF block suballocator。
2. VMA 通过内部 C ABI 包装。

推荐 P1 先接 VMA 作为正确性和性能基线，同时保持 IGpuAllocator 内部边界；P2 根据基准决定是否保留或替换。使用 VMA 不会污染公共 API，也不妨碍我们拥有 Vulkan binding 和渲染架构。

必须支持：

- Device-local block suballocation。
- Host-visible persistent mapping。
- Staging ring。
- Dedicated allocation 条件。
- VK_EXT_memory_budget。
- 资源命名和预算诊断。
- 跨帧延迟释放。
- transient render graph aliasing。

### 7.5 帧资源

默认 2 到 3 frames in flight。每帧拥有：

- Command pools per worker。
- Descriptor arena。
- Dynamic upload arena。
- Query pool。
- Deferred destruction queue。
- Scratch allocator。
- Completion timeline value。

CPU 只有在对应 timeline 完成后才复用帧资源。

## 8. Vulkan 版本与能力策略

### 8.1 当前 Win11 实验室直接使用 Vulkan 1.4

本机实测：

- Vulkan Instance Loader：1.4.341。
- AMD Radeon RX 9070 GRE device apiVersion：1.4.349。
- AMD driver：26.8.1，LLPC。
- Conformance：1.4.3.3。
- Vulkan 1.4 dynamicRenderingLocalRead：true。
- maintenance5、maintenance6、pushDescriptor：true。
- VK_EXT_descriptor_buffer：true。
- Mesh Shader：true。
- Ray Query：true。
- Ray Tracing Pipeline：true。

因此当前主实现不再以 Vulkan 1.3 为设计基线。P0 直接创建 Vulkan 1.4 instance/device，并优先验证：

- Dynamic Rendering 和 Dynamic Rendering Local Read。
- Synchronization 2。
- Timeline Semaphore。
- Scalar Block Layout。
- Buffer Device Address。
- Descriptor Buffer。
- Maintenance 5/6。

Mesh Shader、Ray Query 和 Ray Tracing Pipeline 虽然当前硬件可用，但作为 Render Feature Plugin，不成为所有 UI、2D 或基础场景的强制依赖。

### 8.2 跟踪最新 Registry，而不是锁死 1.4

Vulkan Raw Generator 的输入是仓库锁定的最新已批准 vk.xml：

- 更新时生成完整 diff 和 ABI test。
- Runtime 请求 generator 已知且 driver 支持的最高 major/minor。
- Core patch 版本不作为 feature 判断依据。
- 新核心版本可用时增加对应 GpuProfile，不重写上层 Render Graph。
- Extension 和 feature 仍逐项查询，不能只看 apiVersion。

### 8.3 Capability Profile

~~~text
Win9070GreDevelopmentProfile
  ├─ Vulkan 1.4
  ├─ Descriptor Buffer
  ├─ Dynamic Rendering Local Read
  └─ optional Mesh/Ray feature plugins

DesktopPortableProfile
  ├─ Vulkan core/extension negotiation
  └─ future Linux and other desktop adapters

ApplePortabilityProfile
  ├─ MoltenVK
  ├─ VK_KHR_portability_subset
  └─ feature-specific fallback

AndroidProfile
  ├─ device Vulkan feature query
  └─ Android Baseline Profile policy
~~~

未来 adapter 可以回退或替换某个 render feature，但不能迫使当前 Windows 主路径停留在最低公共版本。

### 8.3 设备选择

DeviceSelector 评分：

- 必需 feature 和 extension。
- 独立 GPU 偏好。
- VRAM budget。
- Queue family。
- Present support。
- Timestamp 和 calibrated timestamp。
- 可选 feature tier。
- 用户配置的 adapter override。

启动输出完整 capability report，并允许保存到诊断包。

### 8.4 Validation

Debug 默认：

- VK_LAYER_KHRONOS_validation。
- Synchronization Validation。
- Best Practices。
- GPU-assisted validation 按需启用。
- Debug Utils object naming。

Release 默认关闭，但通过命令行可启用诊断模式。所有 Vulkan 错误包含：

- 对象逻辑名称。
- owner plugin。
- frame 和 pass。
- callsite。
- validation message ID。

## 9. Render Graph

### 9.1 目标

Render Graph 负责：

- Pass 依赖。
- 逻辑资源读写。
- 执行顺序。
- Image layout。
- Stage/access barrier。
- Queue ownership。
- Transient resource lifetime 和 aliasing。
- Pass culling。
- 并行 command recording 分组。
- GPU timestamp。
- Plugin pass 的插入与移除。

它不负责：

- 游戏世界查询。
- 材质业务规则。
- 自动猜测所有渲染算法。
- 隐藏 Vulkan 的资源访问语义。

### 9.2 强类型资源句柄

~~~csharp
RenderImage<ColorTarget> hdr = graph.CreateImage<ColorTarget>(
    "Scene.Hdr",
    ImageDesc.Color2D(
        format: PixelFormat.Rgba16Float,
        size: Size2D.Swapchain,
        transient: true));

RenderImage<DepthTarget> depth = graph.CreateImage<DepthTarget>(
    "Scene.Depth",
    ImageDesc.Depth2D(
        format: PixelFormat.D32Float,
        size: Size2D.Swapchain,
        transient: true));
~~~

ColorTarget、DepthTarget、SampledTexture 等 marker 防止明显误用；真实格式和 usage 仍由 descriptor 与 graph compiler 验证，避免泛型组合爆炸。

### 9.3 Pass DSL

推荐形态：

~~~csharp
graph.Pass<GBufferPassData>("Scene.GBuffer", pass =>
{
    pass.Depth = pass.Write(
        depth,
        ImageAccess.DepthAttachment(
            load: LoadOp.Clear,
            store: StoreOp.Store));

    pass.Color = pass.Write(
        hdr,
        ImageAccess.ColorAttachment(
            load: LoadOp.Clear,
            store: StoreOp.Store));

    pass.Scene = pass.Read(renderScene);

    pass.Execute(static (ref RenderCommands command, in GBufferPassData data) =>
    {
        command.BindPipeline(Pipelines.GBuffer);
        command.DrawScene(data.Scene);
    });
});
~~~

结构、资源访问和 execute 在一个局部块内；但执行委托必须是静态或生成缓存，避免每帧捕获。

PassData 是由 source generator 支持的 struct。Graph setup 只在图结构变化时执行；稳定帧直接复用编译计划并更新外部资源。

复杂或可复用的 pass 使用浅继承：

~~~csharp
public sealed class GBufferPass : RenderPass<GBufferPassData>
{
    protected override void Setup(RenderPassBuilder<GBufferPassData> pass)
    {
        pass.Data.Depth = pass.Write(
            Resources.SceneDepth,
            DepthAttachment.Clear());

        pass.Data.Scene = pass.Read(Resources.RenderScene);
    }

    protected override void Execute(
        ref RenderCommands command,
        in GBufferPassData data)
    {
        command.BindPipeline(Pipelines.GBuffer);
        command.DrawScene(data.Scene);
    }
}
~~~

小型一次性 pass 用 inline DSL；稳定渲染特性继承 RenderPass<TData>，再通过插件的 Rendering.Add<TPass>() 注册。两种形态共享同一 graph compiler。

### 9.4 编译阶段

~~~text
Collect passes
  → Validate handles and ownership
  → Build read/write dependency graph
  → Cull unreachable passes
  → Topological schedule
  → Determine queue assignment
  → Calculate resource lifetimes
  → Alias transient allocations
  → Synthesize Synchronization 2 barriers
  → Form parallel recording batches
  → Cache compiled plan
~~~

### 9.5 插件扩展点

插件通过 namespaced slot 插入 pass：

~~~text
Scene.Depth
Scene.Opaque
Scene.Transparent
Post.BeforeToneMap
Post.AfterToneMap
Overlay.WorldUi
Overlay.ScreenUi
Editor.Gizmos
Present
~~~

插件注册声明：

- Before。
- After。
- Required slot。
- Optional slot。
- Read/write resource contract。

冲突、环和缺少必需 slot 在激活新插件 generation 前报错，不在帧中临时失败。

## 10. 渲染器架构

### 10.1 Render Extraction

渲染线程不直接遍历正在被游戏系统修改的 World。主循环在明确 barrier 生成 RenderWorld：

~~~text
Game World
  → Extract systems
  → Render World snapshot
  → Visibility
  → Draw packet generation
  → Render Graph
~~~

RenderWorld 使用面向渲染的 SoA 数据：

- Transform。
- Bounds。
- Mesh handle。
- Material handle。
- Layer。
- Visibility flags。
- Skinning data。
- Light data。

它可以双缓冲或使用 snapshot generation，避免游戏更新与命令录制互锁。

### 10.2 Draw Packet

可见对象编译为紧凑 DrawPacket：

- Pipeline key。
- Material binding key。
- Mesh/index range。
- Instance range。
- Sort key。
- Pass mask。
- Owner generation。

排序和批处理发生在 CPU；后续可增加 GPU culling 和 indirect draw，但不在 P1 一开始引入。

### 10.3 多线程命令录制

Vulkan 不替应用自动完成 CPU 多线程。设计：

- 每个 worker、每个 frame 独立 command pool。
- Graph compiler 形成足够大的 recording batch。
- 主 command buffer 负责 barrier、dynamic rendering scope 和 secondary execute。
- Secondary command buffer 只用于具有足够 draw 数量的批次。
- 不创建大量只有几个 draw 的微型 command buffer。
- 每帧 reset command pool，不反复 free/allocate 每个 command buffer。

### 10.4 描述符

P1 使用稳定、保守路径：

- Global frame set。
- Material set。
- Draw data storage buffer。
- 每帧 descriptor arena。
- 在 frame safe point 更新。

P2 在 capability 允许时增加：

- Bindless sampled image table。
- Partially bound。
- Update-after-bind 的严格子集。
- 或 VK_EXT_descriptor_buffer。

任何 descriptor 策略都必须有：

- 生命周期规则。
- 跨帧更新规则。
- fallback。
- Validation stress test。

### 10.5 Pipeline

- HLSL/SPIR-V hash、render state、formats、specialization 构成 PipelineKey。
- Pipeline 创建在后台线程。
- 使用 VkPipelineCache。
- 首帧可使用兼容 fallback pipeline。
- 新 pipeline 完成后在帧边界替换。
- 错误 shader 或 pipeline 不销毁当前可用版本。
- Pipeline 数量、创建耗时和 bind 次数可视化。

### 10.6 默认渲染路径

首个真实路径推荐：

1. Depth prepass 可配置。
2. Clustered Forward 或 Forward+。
3. Opaque。
4. Transparent。
5. Post processing。
6. World UI。
7. Screen UI。
8. Editor overlay。
9. Present。

相比立即建设完整 deferred pipeline，Forward+ 更适合先验证材质、光照、多采样和透明对象；后续通过 Render Graph 插件增加 deferred 或其他路径。

## 11. 渲染性能原则与预算

### 11.1 不靠口号，建立分层指标

每帧分别测量：

- Simulation CPU。
- ECS structural work。
- Render extraction。
- Culling 和 sorting。
- Graph compile 或 reuse。
- Command recording。
- Driver submission。
- GPU pass timestamp。
- Present wait。
- Allocated bytes。
- GC pause。

### 11.2 目标帧率

| 模式 | 帧预算 |
|---|---:|
| 60 Hz | 16.67 ms |
| 120 Hz | 8.33 ms |
| 144 Hz | 6.94 ms |

首版以稳定 60 Hz 和可达 120 Hz 为目标，而不是声明所有场景 144 Hz。

### 11.3 CPU 初始预算

典型 60 Hz 场景目标：

| 阶段 | 目标 |
|---|---:|
| 输入与主循环 | 0.2 ms |
| Gameplay + ECS | 2.5 ms |
| Render extraction | 1.0 ms |
| Visibility + draw packets | 1.5 ms |
| Render graph reuse/compile | 0.3 / 1.0 ms |
| Command recording | 2.0 ms |
| UI update + render data | 0.5 ms |
| Submit 和余量 | 1.0 ms |

GPU 预算由目标画质样例定义，不用一个数字覆盖所有显卡。

### 11.4 稳态分配

目标：

- 稳态空场景每帧 0 B managed allocation。
- 常规玩法帧不因 ECS query、Render Graph execute 或 UI 绘制产生托管垃圾。
- 资源加载可以分配，但必须在 profile 中归因。
- 日志在关闭对应级别时不构造字符串。
- 捕获 lambda 不进入 per-frame hot path。

### 11.5 必须建立的基准

- 100 万实体中 Query 10 万匹配组件。
- 10 万 Transform 更新。
- 10 万 renderable 的视锥剔除。
- 1 千、1 万、10 万 draw packet 的排序和批处理。
- 单线程与多线程 command recording 交叉点。
- 1 千种 pipeline key 的 warm/cold 创建。
- 持续纹理流式上传。
- 1 万 UI 节点局部变化。
- 10 万 glyph instance。
- 插件反复重载 1 千次的内存和句柄稳定性。

### 11.6 Profile before cleverness

以下技术仅在实测收益后开启：

- Async compute。
- Mesh shader。
- GPU culling。
- Descriptor buffer。
- Aggressive transient aliasing。
- 手写 AVX2/AVX-512。
- 自研替换 VMA。
- Cached secondary command buffers。

## 12. Job System

### 12.1 目标

- 固定 worker 数。
- Work stealing。
- 低分配 job storage。
- 依赖计数。
- Cancellation。
- Plugin generation ownership。
- Frame phase barrier。
- Profiler flow。

### 12.2 Job 形态

~~~csharp
public readonly struct IntegrateChunkJob : IJob
{
    public required NativeSlice<Transform> Transforms { private get; init; }
    public required NativeReadOnlySlice<Velocity> Velocities { private get; init; }
    public required float DeltaTime { private get; init; }

    public void Execute()
    {
        for (var i = 0; i < Transforms.Length; i++)
        {
            Transforms[i].Position += Velocities[i].Value * DeltaTime;
        }
    }
}
~~~

NativeSlice 是引擎拥有的 pointer、length、world generation 和 access token 组合，只在调度许可的执行窗口内有效；它不是托管数组，也不能被 job 保存到下一帧。生成器负责构造切片和验证读写权限，普通游戏代码无需直接处理裸指针。

### 12.3 插件约束

插件不得自行创建长期 Thread。它通过 PluginScope.Jobs 调度：

- 新 job 自动绑定 PluginGeneration。
- Quiesce 后拒绝新 job。
- Reload 等待所有已提交 job 完成或取消。
- 执行栈仍在旧程序集时绝不调用 ALC.Unload。
- 卡住的 job 触发超时诊断和 Runtime Process restart，而不是强行卸载。

## 13. ECS 与 World

### 13.1 数据模型

- Entity 是 index + generation。
- Component 默认是 unmanaged struct。
- 相同组件集合构成 Archetype。
- Archetype 由固定容量 Chunk 组成。
- 每个组件为 SoA column。
- Structural change 进入 EntityCommandBuffer，在 phase barrier 应用。

### 13.2 组件声明

~~~csharp
[Component]
public partial struct Transform
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

[Component]
public partial struct Velocity
{
    public Vector3 Value;
}
~~~

生成器提供：

- 稳定 ComponentId。
- Size、alignment 和 unmanaged 验证。
- serializer。
- inspector metadata。
- migration schema hash。
- query column accessor。

包含托管引用的组件进入显式 ManagedComponent 路径，不和高吞吐 chunk 混淆。

### 13.3 System 和 Query

推荐日常形态：

~~~csharp
[UpdateIn<SimulationPhase>]
public partial struct MovementSystem
{
    public void Update(
        in FrameTime time,
        Query<Write<Transform>, Read<Velocity>> query)
    {
        foreach (var chunk in query.Chunks)
        {
            var transforms = chunk.Write<Transform>();
            var velocities = chunk.Read<Velocity>();

            for (var i = 0; i < chunk.Count; i++)
            {
                transforms[i].Position += velocities[i].Value * time.FixedDelta;
            }
        }
    }
}
~~~

Source Generator 从 Query 参数推导读写集合和调度冲突，不需要手写字符串依赖。

便利 API 可以提供逐实体 ref 访问，但性能敏感系统优先 chunk Span。

### 13.4 调度

系统依赖来自：

- Read/Write component access。
- Resource read/write。
- 显式 Before/After。
- Phase。
- Plugin dependency。

Scheduler 构建 DAG，并把无冲突系统并行执行。结构修改通过 command buffer 延迟，避免遍历中移动 archetype。

### 13.5 插件组件与热重载

这是插件系统最危险的根引用之一。规则：

- World 不能在插件卸载时继续持有旧 ALC 的 Type、delegate 或 object。
- Reloadable unmanaged component storage 记录稳定 SchemaId，而不仅是 System.Type。
- 插件 reload closure 停止后，相关 storage 可快照为 engine-owned bytes。
- 新 generation 使用 generated migration 重新绑定或迁移。
- Managed plugin component 必须实现显式 HotState serializer；否则该组件在 reload 时丢弃并报告。

P0 可先限制：

- Contract 不变的 implementation reload 保留 component storage。
- Component schema 变化升级为 Runtime Process hot restart。

P3 再实现组件 schema 的进程内迁移。不能在首版假装所有结构变化都能透明完成。

## 14. Scene、Prefab 与序列化

### 14.1 Scene

Scene 是：

- Entity 数据快照。
- 资源引用。
- 插件和 schema 要求。
- 层级或关系组件。
- 可流式加载的 section。

运行时 World 不依赖编辑器对象图。

### 14.2 Prefab

Prefab：

- 由稳定 EntityLocalId 构成。
- 支持嵌套。
- override 记录为 schema path，不记录托管对象地址。
- 实例化时生成真实 Entity。
- 资产热更新时可选择传播，保留实例 override。

### 14.3 序列化

- 构建期生成 serializer。
- 二进制 cooked format 用于运行时。
- JSON 或 YAML 只作为可审阅源格式或调试输出。
- 所有类型有 SchemaId 和 Version。
- migration 是显式纯函数。
- 不使用 BinaryFormatter。
- 不依赖 AssemblyQualifiedName 作为永久格式。

## 15. Asset Pipeline

### 15.1 Source 与 Cooked 分离

~~~text
AssetsSource/
  character.glb
  albedo.png
  ui/main-menu.ui.cs
  shaders/pbr.hlsl
        ↓ import graph
.zengine/cache/{contentHash}/
  mesh.zmesh
  texture.ztex
  material.zmat
  shader.zspv
        ↓ package
Content/
  base.zpak
  game.zpak
~~~

### 15.2 强类型资源 ID

生成：

~~~csharp
public static partial class GameAssets
{
    public static AssetId<MeshAsset> HeroMesh { get; }
    public static AssetId<TextureAsset> HeroAlbedo { get; }
    public static AssetId<UiPrefab> MainMenu { get; }
}
~~~

业务代码不以字符串路径作为主要 API。

### 15.3 Importer 插件

Importer 也是插件，但运行在 DevHost 或独立 Content Worker：

- 声明输入类型。
- 声明 importer version。
- 输出 typed cooked asset。
- 记录依赖。
- 内容寻址。
- 可热重载 importer。
- importer 更新只重建受影响资产。

复杂、可能崩溃的第三方 importer 可放独立进程，不污染 Runtime。

### 15.4 Asset 热重载

~~~text
File changed
  → debounce
  → import to new immutable content hash
  → validate
  → create CPU resource
  → async GPU upload
  → frame boundary handle swap
  → retire old GPU resource after timeline
~~~

失败继续使用旧资产，并显示精确 importer、源文件和依赖链错误。

## 16. Shader 与材质

### 16.1 为什么首版选择 HLSL

不在 P0 自研 C# shader compiler。首版：

- HLSL 2021。
- DXC 输出 SPIR-V。
- SPIR-V validation。
- SPIR-V reflection。
- Source Generator 生成 C# 绑定。

原因：

- DXC 官方支持 Vulkan SPIR-V。
- 工具链成熟。
- 可以调试、反编译和使用厂商分析器。
- 先把引擎渲染架构验证好，再决定是否开发 C# shader subset。

### 16.2 强类型 Shader Binding

HLSL：

~~~hlsl
struct CameraData
{
    float4x4 ViewProjection;
    float3 CameraPosition;
};

[[vk::binding(0, 0)]]
ConstantBuffer<CameraData> Camera;

[[vk::binding(0, 1)]]
Texture2D<float4> Albedo;
~~~

生成 C#：

~~~csharp
[StructLayout(LayoutKind.Sequential)]
public partial struct CameraData
{
    public Matrix4x4 ViewProjection;
    public Vector3 CameraPosition;
    private float _padding;
}

public readonly partial struct PbrBindings
{
    public required UniformBinding<CameraData> Camera { get; init; }
    public required TextureBinding2D<Rgba> Albedo { get; init; }
}
~~~

生成器验证：

- size 和 offset。
- matrix major order。
- descriptor set/binding。
- push constant size。
- vertex input。
- specialization constant。

### 16.3 Shader 热重载

1. DXC 编译新 SPIR-V。
2. 验证。
3. 反射并计算 LayoutHash。
4. LayoutHash 不变时后台建新 pipeline。
5. 帧边界替换。
6. 旧 pipeline 按 timeline 退休。
7. LayoutHash 变化时重建受影响 material binding；失败则保持旧 generation。

### 16.4 材质

Material 是：

- Shader family。
- permutation。
- typed parameter block。
- texture/sampler handles。
- render state。

公共 API 不允许按字符串设置任意参数。编辑器通过 generated metadata 呈现 inspector。

## 17. 插件包与契约

### 17.1 插件包结构

每个插件是一个可版本化目录或 zplugin 包：

~~~text
Physics/
  Physics.Contracts/
    IPhysicsWorld.cs
    PhysicsComponents.cs
    Physics.Contracts.csproj
  Physics.Runtime/
    PhysicsPlugin.cs
    Physics.Runtime.csproj
  Physics.Editor/
    PhysicsEditorPlugin.cs
    Physics.Editor.csproj
  Physics.Tests/
  plugin.manifest.json
~~~

Contract 与 Runtime 分离是有实际价值的：

- Contract 类型可被依赖插件共享。
- Runtime 实现可以卸载。
- 类型身份可控。
- 依赖方不引用实现程序集。
- 契约变化与实现变化可以使用不同热重载等级。

### 17.2 插件声明

~~~csharp
[Plugin<PhysicsContract>(
    id: "zengine.physics",
    version: "1.2.0")]
[Requires<InputContract>("[1.0.0,2.0.0)")]
public sealed partial class PhysicsPlugin(
    IInput input,
    ILogger<PhysicsPlugin> logger)
    : EnginePlugin
{
    protected override void Configure(PluginBuilder plugin)
    {
        plugin.Provide<IPhysicsWorld>(new PhysicsWorld());
        plugin.Systems.Add<PhysicsStepSystem>();
        plugin.Rendering.Add<PhysicsDebugPass>();
        plugin.Editor.Add<PhysicsPanel>();
    }
}
~~~

PluginBuilder 的所有操作自动登记到内部 PluginScope，开发者不手工管理 disposer。主构造函数注入共享 Contract 服务。生成器：

- 生成 manifest。
- 验证依赖 attribute 与构造函数需求一致。
- 生成 activator，避免热路径反射。
- 注册所有 contribution 到 PluginScope。
- 生成卸载诊断表。

### 17.3 Contract Assembly

Engine stable contracts 位于 Default ALC：

- Plugin API。
- Logging。
- Job、ECS、Render Graph 和 UI extension contracts。
- 基础 value types。

插件自己的 Contracts Assembly 位于 generation 级 PluginContractLoadContext：

- 同一 generation 的所有 runtime ALC 共享相同 Assembly 实例。
- 依赖插件引用 provider 的 Contract Assembly。
- Runtime ALC 不加载自己的重复 Contracts 副本。
- 类型是否相同由 Assembly 实例保证，不依赖名称侥幸匹配。

### 17.4 Runtime AssemblyLoadContext

每个插件 runtime 使用 collectible ALC：

- AssemblyDependencyResolver 解析私有托管和原生依赖。
- Engine stable assemblies 从 Default ALC 共享。
- Plugin contracts 从当前 PluginContractLoadContext 共享。
- 其他私有依赖默认隔离。
- 加载路径来自 immutable generation，不直接加载 build 输出。

### 17.5 信任边界

进程内插件默认为自家或明确受信任插件。AssemblyLoadContext：

- 不是安全沙箱。
- 不能阻止 File、Network、P/Invoke。
- 不能隔离崩溃和无限循环。

不可信插件必须未来使用独立进程、OS 权限和消息协议，不在 v1 假装解决。

## 18. 插件依赖图

### 18.1 依赖语义

插件依赖不是普通 NuGet 依赖的运行时别名。它表达：

- 激活顺序。
- 可用 Contract Assembly。
- 必需服务。
- 生命周期联动。
- 热重载影响范围。
- 版本兼容区间。

### 18.2 强类型依赖

Provider contract：

~~~csharp
public sealed class PhysicsContract : PluginContract { }

public interface IPhysicsWorld
{
    RayHit CastRay(in Ray ray, in QueryFilter filter);
}
~~~

Consumer：

~~~csharp
[Requires<PhysicsContract>("[1.2.0,2.0.0)")]
public sealed partial class GameplayPlugin(IPhysicsWorld physics)
    : EnginePlugin
{
}
~~~

泛型 attribute 让 IDE、编译器和生成器都能找到依赖契约，不使用字符串服务名。

### 18.3 必需、可选和开发依赖

- Required：缺失则插件不可激活。
- Optional：存在则注入 Optional<T> 或 capability handle。
- Development：只在 Editor/DevHost generation 中需要。
- Build：仅 importer、codegen 或 packaging 使用，不进入 runtime graph。

### 18.4 版本

- 使用 SemVer 2.0。
- manifest 保存 exact package version。
- dependency 使用 version range。
- plugin.lock.json 保存当前解析结果和内容 hash。
- 运行时只加载 lock 中的精确 generation。
- 不在启动时在线解析或下载。
- Debug 可以接受 prerelease；Release 默认拒绝未显式允许的 prerelease。

### 18.5 DAG 与循环

v1 插件依赖图必须是 DAG。循环依赖在构建和加载时拒绝，并输出完整路径：

~~~text
Plugin A → Plugin B → Plugin C → Plugin A
~~~

需要双向协作时使用：

- Engine-owned Event。
- Service interface。
- Extension point。
- Optional dependency。
- 把共享 contract 下沉到独立基础插件。

不以加载顺序掩盖真实循环。

### 18.6 激活顺序

~~~text
Discover manifests
  → validate signatures and hashes
  → solve versions
  → detect cycles
  → load contract assemblies
  → load runtimes in topological order
  → construct into staging registry
  → validate required exports
  → atomically activate
~~~

### 18.7 Provider 消失

若必需 provider 被禁用或重载：

- 所有 transitive consumer 进入 Quiescing。
- consumer 先于 provider 停止。
- provider 恢复后按 provider 到 consumer 顺序启动。
- Optional consumer 可以由 capability change event 局部降级，不必强制卸载。

## 19. 插件作用域与可卸载性

### 19.1 PluginScope

所有扩展点都通过 scope：

~~~csharp
public override void Configure(PluginScope scope)
{
    scope.Services.Export<IPhysicsWorld>(world);
    scope.Events.Subscribe<DebugDrawEvent>(OnDebugDraw);
    scope.Systems.Add<PhysicsStepSystem>();
    scope.RenderGraph.Add<PhysicsDebugPass>();
    scope.Assets.Mount(physicsContent);
    scope.UI.AddPanel<PhysicsPanel>();
}
~~~

scope 记录：

- contribution ID。
- owner plugin 和 generation。
- 注册源码位置。
- dispose delegate。
- 当前 active work。
- GPU retirement requirement。

### 19.2 卸载顺序

~~~text
Reject new work
  → cancel cancellable work
  → wait running jobs
  → remove UI and editor entry points
  → remove render graph factories
  → flush ECS systems and commands
  → revoke services and events
  → snapshot hot state
  → dispose plugin objects
  → retire GPU resources
  → release native libraries if supported
  → unload ALC
  → verify WeakReference collection
~~~

### 19.3 禁止的根引用

Analyzer 和运行时诊断重点检查：

- Default ALC static event 持有 plugin delegate。
- Timer callback。
- Thread 和 ThreadStatic。
- Task continuation。
- GCHandle。
- pinned delegate。
- Logger scope。
- event source listener。
- ECS managed component。
- Render Graph cached delegate。
- UI event handler。
- Native callback。
- 未完成 async state machine。

### 19.4 Native library

插件携带的 native library 可能无法可靠热卸载。manifest 必须声明：

~~~text
NativeReloadPolicy:
  Reloadable
  KeepLoaded
  RestartRuntime
~~~

默认 RestartRuntime。只有通过专门压力测试证明 NativeLibrary.Free、安全 callback 清理和线程退出后，才能标为 Reloadable。

## 20. 热重载架构

### 20.1 DevHost 与 Runtime 分进程

~~~text
Rider / File Watcher
       ↓
ZEngine.DevHost
  ├─ dotnet build coordinator
  ├─ shader compiler
  ├─ asset importer
  ├─ generation store
  └─ runtime process supervisor
       ↓ local control channel
ZEngine.Host
~~~

DevHost 不渲染游戏。Runtime Process 崩溃或核心程序集变化时，DevHost 仍然存活并可重启、恢复会话。

### 20.2 Immutable Generation

~~~text
.zengine/dev/generations/{id}/
  engine/
  contracts/
  plugins/
    zengine.physics/
      runtime/
      editor/
      deps.json
  shaders/
  content/
  manifest.json
  plugin.lock.json
~~~

永远不从 bin/Debug 原地加载 DLL，避免文件锁、部分写入和依赖错配。

### 20.3 Reload 等级

| 等级 | 范围 | 行为 |
|---|---|---|
| R0 | Shader / asset | 后台构建，帧边界替换 |
| R1 | 单插件 implementation | 重载插件和需要重建的 runtime consumers |
| R2 | Plugin contract / dependency graph | 重载受影响 contract generation 和反向依赖闭包 |
| R3 | Game module / ECS schema | 序列化会话，重启 Runtime Process |
| R4 | Engine core / native ABI | 重启 Runtime Process；必要时重启 DevHost |

首版必须把 R1 做可靠，而不是用 Metadata Update 假装所有类型变化都可以原位修改。

### 20.4 R1 事务

以 PhysicsPlugin 更新为例：

~~~text
Build new immutable generation
  → Resolve dependency graph
  → Load new ALCs in suspended mode
  → Construct staging PluginScopes
  → Validate services, systems, passes and UI
  → Wait engine frame safe point
  → Quiesce old reverse dependency closure
  → Snapshot hot state
  → Restore snapshot into staged generation
  → Atomically swap registries and schedules
  → Run probation frames
  → Dispose and unload old generation
~~~

新 generation 在旧 generation 停止前完成大部分验证。

### 20.5 回滚

在原子切换前失败：

- 丢弃 staged generation。
- 旧 generation 完全不受影响。

切换后 probation frame 失败：

- 暂停新 generation。
- 若旧 generation 尚未卸载，则恢复旧 registry 和 schedule。
- 丢弃新 generation。
- 输出 failure bundle。

只有 probation 成功后才真正卸载旧 ALC。

### 20.6 Frame Safe Point

安全点条件：

- 当前 simulation phase 结束。
- EntityCommandBuffer 已 flush。
- 没有旧 generation job 在执行。
- RenderWorld extraction 完成或尚未开始。
- 当前帧 Render Graph contribution 已固定。
- GPU 对旧插件资源的使用有明确 timeline。

CPU contribution 可以原子替换；GPU 对象进入延迟退休，不要求 vkDeviceWaitIdle。

### 20.7 热状态

插件只可以把状态保存到 engine-owned HotState：

~~~csharp
public override void SaveHotState(HotStateWriter state)
{
    state.Write("debug.enabled", DebugEnabled);
    state.Write("solver.iterations", SolverIterations);
}

public override void RestoreHotState(HotStateReader state)
{
    DebugEnabled = state.ReadOr("debug.enabled", false);
    SolverIterations = state.ReadOr("solver.iterations", 8);
}
~~~

生成器可以为标记的 HotState partial property 生成实现，但状态格式必须：

- 不包含旧 ALC object。
- 版本化。
- 有 size limit。
- 可诊断。
- 支持迁移。

### 20.8 ALC 卸载验证

每次开发期 reload 建立 WeakReference。卸载后在后台诊断阶段：

1. GC.Collect。
2. GC.WaitForPendingFinalizers。
3. 再次 GC.Collect。
4. 检查 WeakReference。

这不是日常性能路径，只用于确认 generation 可回收。失败时：

- 标记 LeakDetected。
- 列出 scope 未释放项和已知 root。
- 停止连续堆叠新 generation。
- 建议或自动执行 R3 Runtime Process restart。

## 21. Game Module 热重载

游戏自身视为特殊顶级插件：

~~~text
Game.Contracts
Game.Runtime
Game.Editor
~~~

### 21.1 可进程内变化

- System 方法体。
- UI 结构和行为。
- Render pass 实现。
- 非 schema gameplay service。
- 事件处理。

### 21.2 升级为进程重启的变化

- Engine stable contract。
- 广泛 ECS component layout。
- Scene schema。
- Native ABI。
- Default ALC 中的核心服务。
- 无法卸载的 native plugin。

R3 重启不是冷启动：

1. DevHost 请求 runtime session snapshot。
2. 保存当前 scene、camera、selection、play state 和 editor layout。
3. 启动新 Runtime Process。
4. 加载新 generation。
5. 恢复兼容 snapshot。
6. 旧进程退出。

可进一步做并行新进程 warm-up 和窗口交接，但不是 P0。

## 22. 统一 UI 兼容模型

### 22.1 目标

同一份共享 C# 组件代码表达：

- Component。
- State。
- Event。
- DOM-like Node。
- Theme。
- Typed CSS。
- Semantics。
- Agent metadata。

后端负责投影：

~~~text
Shared Component Tree
  ├─ Web: Blazor RenderTree → real DOM/CSS
  └─ Native: Computed Style → Layout/Text → Vulkan primitives
~~~

共享的是组件和 Portable UI 语义，不承诺浏览器历史兼容行为、平台控件外观或像素完全相同。

### 22.2 不使用组件平台注解

不采用：

~~~csharp
[WebOnly]
[NativeOnly]
[UiProfile(UiProfiles.Web)]
~~~

组件可用范围由包和项目引用决定：

~~~text
Game.UI
  → Zui.Basic
  → Zui.Enhancements.Forms
  → Zui.Enhancements.Animation

Game.Web
  → Game.UI
  → Zui.Backend.Blazor
  → Zui.Enhancements.Web.Dom
  → Zui.Enhancements.Web.AdvancedCss

Game.Windows
  → Game.UI
  → Zui.Backend.Vulkan
  → Zui.Enhancements.Windows.Shell
  → Zui.Enhancements.Windows.Fluent
~~~

Game.UI 看不到 Web/Windows 扩展 API，因此无法误用。平台边界由编译依赖真实保证。

### 22.3 三类包能力

#### Basic

所有后端必须实现：

- Div、Span、Text、Heading、Paragraph。
- Image。
- Button、Input、TextArea、Checkbox、Radio、Slider。
- List、Scroll、Canvas、Dialog、Overlay。
- Portable CSS。
- 基础 focus、event、semantics 和 Agent action。

#### Portable Enhancement

增加组件或行为，但必须提供 Web 与 Native 实现：

- Forms。
- Animation。
- Virtualization。
- DataGrid。
- Validation。
- DatePicker。
- ComboBox。

#### Platform Enhancement

只绑定专属 backend/platform：

- Web Advanced DOM/CSS/Media。
- Windows Shell/Fluent。
- Linux Desktop。
- Android Material。
- Apple Cupertino。
- Native WorldSpace/Gamepad。

专属组件由包注入平台 facet：

~~~csharp
ui.Web.Video(...);
ui.Web.IFrame(...);

ui.Windows.TitleBar(...);
ui.Windows.SystemMenu(...);

ui.Android.BackGestureRegion(...);
ui.Apple.SafeAreaOverlay(...);
~~~

### 22.4 Portable CSS 范围

首阶段 Portable CSS 包括：

- Box sizing。
- Width、height、min/max、aspect ratio。
- Margin、padding、gap。
- Flex。
- Grid 的明确子集。
- Relative、absolute、fixed-like overlay。
- Overflow 和 scroll。
- Color、background、image。
- Border、radius、outline、shadow。
- Opacity。
- 2D transform。
- Font、line height、letter spacing、text align、white space。
- Visibility、pointer events、cursor。
- Hover、active/pressed、focus、focus-visible、disabled、checked、selected。
- Transition。
- Keyframe animation 的目标子集。
- Safe area、reference resolution、world space 等原生增强能力。

完整 selector、table layout、float、多栏、fragmentation、Shadow DOM 和浏览器历史 quirks 不属于 Portable 基线。

### 22.5 UI IR

共享运行时使用自己的 UI IR，而不是把 Browser HTMLElement 泄漏到共享代码：

~~~text
UiRoot
  └─ ComponentNode
      └─ ElementNode
          ├─ TextNode
          ├─ ElementNode
          └─ ComponentNode
~~~

每个节点包含：

- Stable NodeId。
- ElementKind。
- Typed attributes。
- Style references。
- Inline style overrides。
- Event table。
- Semantics。
- Children。
- Package owner。
- Capability requirements。
- Hot reload generation。

共享层允许：

- Parent/Children。
- Focus。
- ScrollIntoView。
- Bounds。
- Semantics。
- Typed query。

Browser 专属 Document、HTMLElement、MutationObserver、Range、Selection 和 ShadowRoot 只存在于 Web 增强包。

### 22.6 组件与状态

~~~csharp
public sealed partial class LoginCard
    : UiComponent<AppTheme>
{
    [State]
    private partial string UserName { get; set; }

    [State]
    private partial bool Submitting { get; set; }

    protected override void Compose(Ui<AppTheme> ui)
    {
        // typed DOM/CSS DSL
    }
}
~~~

State、keyed reconciliation、event、resource 和 Agent NodeId 在两后端保持同一逻辑身份。

## 23. Typed DOM/CSS C# DSL

### 23.1 基本形态

~~~csharp
protected override void Compose(Ui<AppTheme> ui)
{
    ui.Div(card =>
    {
        card.Display.Flex();
        card.FlexDirection.Column();

        card.Width.Rem(28);
        card.MaxWidth.Percent(90);

        card.Padding.All.Rem(1.5f);
        card.Gap.Rem(0.75f);

        card.BackgroundColor.Surface();

        card.Border.All.Width.Px(1);
        card.Border.All.Style.Solid();
        card.Border.All.Color.Subtle();

        card.BorderRadius.All.Rem(0.75f);
        card.BoxShadow.Medium();

        card.H1("登录", title =>
        {
            title.Color.TextPrimary();
            title.FontSize.Rem(1.5f);
            title.FontWeight.Bold();
        });

        card.Input(
            value: UserName,
            onChange: value => UserName = value,
            input =>
            {
                input.Placeholder = "用户名";
                input.AutoComplete.UserName();
            });

        card.Button(
            Submitting ? "登录中…" : "登录",
            onClick: SubmitAsync,
            button =>
            {
                button.Variant.Primary();
                button.Disabled = Submitting;
            });
    });
}
~~~

结构、样式、属性和事件仍位于同一节点 scope。普通 CSS 开发者可以识别属性，Rider 又能提供强类型补全。

### 23.2 节点命名

Portable DOM 节点使用 PascalCase：

- Div。
- Span。
- Text。
- H1–H6。
- P。
- Img。
- Button。
- Input。
- TextArea。
- Label。
- List。
- Scroll。
- Canvas。
- Dialog。
- Overlay。
- Viewport。

游戏语义组件可以作为 Portable Enhancement：

- Panel。
- Stack。
- VirtualList。
- InventoryGrid。
- HudLayer。

它们最终仍投影到基础 UI IR。

### 23.3 CSS 属性载体

每个 CSS property 是独立强类型 carrier：

~~~csharp
node.Display.Flex();
node.Position.Absolute();

node.Width.Px(320);
node.MinWidth.Rem(12);
node.MaxWidth.Percent(90);

node.Margin.Top.Px(8);
node.Padding.All.Rem(1);
node.Gap.Px(12);

node.BackgroundColor.Surface();

node.Border.Top.Width.Px(1);
node.Border.Top.Style.Solid();
node.Border.Top.Color.Subtle();

node.BorderRadius.All.Px(12);
node.Opacity.Set(0.9f);
~~~

setter 返回 void，不允许跨 property 无限 fluent chain。

### 23.4 CSS Value

内部使用 typed value：

~~~csharp
CssLength
CssColor
CssAngle
CssTime
CssPercentage
CssTransform
CssShadow
CssGridTrack
CssDisplay
CssPosition
~~~

公共属性提供方便方法：

~~~csharp
node.Width.Auto();
node.Width.Fill();
node.Width.Px(320);
node.Width.Rem(20);
node.Width.Percent(100);
node.Width.Set(CssLength.Clamp(...));
~~~

复杂值：

~~~csharp
node.GridTemplateColumns.Set(
    GridTrack.Fr(1),
    GridTrack.MinMax(
        CssLength.Px(240),
        GridTrack.Fr(2)));

node.BoxShadow.Add(
    x: 0,
    y: 8,
    blur: 24,
    spread: -8,
    color: Colors.Black.WithAlpha(0.3f));
~~~

### 23.5 Theme 泛型

TTheme 沿 Ui<TTheme> 和节点 builder 传播：

~~~csharp
node.BackgroundColor.Surface();
node.Color.TextPrimary();
node.Border.All.Color.Subtle();
node.Padding.All.Large();
node.BoxShadow.Medium();
~~~

这些方法由 Theme token generator 生成。任意值仍可使用 Set、Px、Rem 等明确入口。

### 23.6 Reusable Style

~~~csharp
public static class AppStyles
{
    public static readonly UiStyle<Div, AppTheme> Card =
        UiStyle.Define<Div, AppTheme>(card =>
        {
            card.Display.Flex();
            card.FlexDirection.Column();
            card.Padding.All.Rem(1.5f);
            card.Gap.Rem(0.75f);
            card.BackgroundColor.Surface();
            card.Border.All.Width.Px(1);
            card.Border.All.Style.Solid();
            card.Border.All.Color.Subtle();
            card.BorderRadius.All.Rem(0.75f);
            card.BoxShadow.Medium();
        });
}
~~~

使用：

~~~csharp
ui.Div(card =>
{
    card.Use(AppStyles.Card);
    card.Width.Rem(32);

    card.H1("标题");
});
~~~

Style 预编译、intern 和 hash，不在每帧执行声明委托。

### 23.7 明确 Style Stack

不用浏览器完整 specificity 规则决定 C# typed style 的覆盖顺序：

~~~text
1. Element default
2. Theme default
3. Variant
4. Use styles，按声明顺序
5. Inline typed property
6. State overlay
7. Animation value
~~~

Analyzer 要求 Variant/Use 出现在第一个 inline property 之前，避免代码阅读顺序与实际覆盖顺序冲突。

### 23.8 State Style

~~~csharp
button.Hover(style =>
{
    style.BackgroundColor.PrimaryHover();
    style.Transform.Scale(1.02f);
});

button.Pressed(style =>
{
    style.Transform.Scale(0.98f);
});

button.FocusVisible(style =>
{
    style.Outline.Width.Px(2);
    style.Outline.Color.Focus();
});

button.DisabledStyle(style =>
{
    style.Opacity.Set(0.5f);
});
~~~

Web 生成 class/pseudo-class rule；Native 编译为状态 overlay。状态变化不重新解析 style。

### 23.9 CSS 文件

#### Portable CSS

Portable 增强包可以携带 CSS 文件。构建期执行：

~~~text
CSS tokenizer/parser
  → Selector AST
  → Declaration AST
  → Capability validation
  → Typed style program
  ├─ Web CSS asset
  └─ Native style program
~~~

不支持的 selector/property 在构建期报诊断，不在原生端悄悄近似。

#### Web CSS

Web 增强包允许完整浏览器 CSS、media query、container query、custom property 和第三方样式。它只进入 Web Host，不需要组件 attribute。

### 23.10 Event

Portable event：

- Click。
- Input/Change。
- Pointer。
- Key。
- Focus/Blur。
- Submit。
- Scroll。
- Composition/IME。
- Drag/drop 的基础子集。

Event route 支持 capture、target、bubble，并使用 NodeId 而不是 Browser object identity。

Web backend 投影为 Blazor event；Native backend 由 hit test、focus 和 input queue 产生。

### 23.11 DOM Query

共享代码允许 typed query：

~~~csharp
UiRef<Button> submit =
    ui.Query.ById<Button>("login.submit");

IReadOnlyList<UiRef<Input>> inputs =
    ui.Query.Descendants<Input>();
~~~

不在共享代码提供任意字符串 querySelector。Web 增强包可以提供真实 selector API，但返回 Web 专属 handle。

## 24. 基础包与增强包

### 24.1 包图

~~~text
Zui.Core
  ├─ Zui.Dom
  ├─ Zui.Css
  ├─ Zui.Events
  └─ Zui.Semantics
       ↓
Zui.Basic
       ↓
Zui.Enhancements.Portable.*
       ↓
┌─────────────────────────┬──────────────────────────┐
│ Zui.Backend.Blazor      │ Zui.Backend.Vulkan      │
│ Zui.Enhancements.Web.*  │ Zui.Enhancements.*      │
└─────────────────────────┴──────────────────────────┘
~~~

### 24.2 Package Manifest

每个包携带 build-time manifest：

~~~json
{
  "id": "zui.windows.fluent",
  "version": "1.0.0",
  "platform": "windows",
  "requires": [
    "zui.basic",
    "zui.backend.vulkan",
    "zui.platform.windows"
  ],
  "provides": [
    "style.fluent",
    "component.windows.titlebar",
    "enhancer.button.fluent",
    "enhancer.panel.mica"
  ]
}
~~~

Zui.Sdk 收集 manifest 并生成静态 registry，不在启动时反射扫描程序集。

### 24.3 增强已有组件

增强点分为：

~~~text
RendererAdapter<TComponent>
Behavior<TComponent>
StyleVariant<TComponent>
Decorator<TComponent>
~~~

规则：

- 一个 backend 对一个 component 只能有一个 primary RendererAdapter。
- Behavior/Decorator 可以多个，但必须声明 Before/After/Requires。
- StyleVariant 只能增加视觉 variant，不能改写基础事件契约。
- 不采用 last package wins。

### 24.4 Extension Block 增加 API

Windows Fluent 包：

~~~csharp
public static class FluentPanelExtensions
{
    extension<TTheme>(Panel<TTheme> panel)
        where TTheme : UiTheme
    {
        public void Mica()
        {
            panel.Use(FluentStyles.MicaPanel);
        }

        public void Acrylic()
        {
            panel.Use(FluentStyles.AcrylicPanel);
        }
    }
}
~~~

Web CSS 包：

~~~csharp
extension<TTheme>(Div<TTheme> div)
    where TTheme : UiTheme
{
    public void BackdropBlur(float radius)
    {
        div.BackdropFilter.Blur(radius);
    }
}
~~~

只有引用对应包的项目才能看到这些 API。

### 24.5 添加新组件

Portable 包：

~~~csharp
ui.DatePicker(...);
ui.ComboBox(...);
ui.DataGrid(...);
~~~

平台包：

~~~csharp
ui.Web.Video(...);
ui.Web.IFrame(...);
ui.Windows.TitleBar(...);
ui.Android.BackGestureRegion(...);
ui.Apple.SafeAreaOverlay(...);
~~~

平台 facet 避免名字冲突并清楚表达不可移植性。

### 24.6 项目约束

~~~xml
<Project Sdk="Zui.Sdk">
  <PropertyGroup>
    <ZuiPlatform>Portable</ZuiPlatform>
  </PropertyGroup>
</Project>
~~~

Web：

~~~xml
<PropertyGroup>
  <ZuiPlatform>Web</ZuiPlatform>
  <ZuiBackend>BlazorDom</ZuiBackend>
</PropertyGroup>
~~~

Windows：

~~~xml
<PropertyGroup>
  <ZuiPlatform>Windows</ZuiPlatform>
  <ZuiBackend>Vulkan</ZuiBackend>
</PropertyGroup>
~~~

Analyzer 阻止 Portable 项目引用平台包、阻止 backend 冲突，并验证每个 Portable 组件都有目标 backend adapter。

### 24.7 包冲突诊断

~~~text
ZUI4101:
Two packages provide the exclusive Web Button renderer.

ZUI4102:
Windows Fluent requires Vulkan + Windows platform,
but the current host is Web.

ZUI4103:
Platform package cannot be referenced by Portable project.

ZUI4104:
Enhancer dependency graph contains a cycle.
~~~

### 24.8 热重载

稳定 Host 包通常不热卸载：

- Zui.Core。
- Zui.Backend.Blazor。
- Zui.Backend.Vulkan。
- Platform Host。

可热重载增强包：

- Portable form/data components。
- Theme/style package。
- App UI enhancement。
- Editor package。

它们的 StyleVariant、Behavior、Decorator、Component、Agent action 和 Editor panel 都进入 UiPackageScope。更新时按包依赖闭包重建 registry，并保留兼容 State/NodeId。

### 24.9 Agent Schema

包可以为组件提供：

- Semantic role。
- State properties。
- Actions。
- Observation schema。
- Screenshot highlight。
- Test fixture。

生成器从 package manifest 和 typed component contract 生成 Agent schema，不要求组件写 Agent attribute。

## 25. Blazor 与 Vulkan 双后端

### 25.1 Web Blazor

~~~text
Component State
  → Portable UI Tree
  → Zui.Backend.Blazor
  → RenderTreeBuilder
  → Blazor diff
  → batched JS DOM update
  → Browser DOM/CSS/Layout
~~~

Web backend 映射：

| Portable | Web |
|---|---|
| Div | div |
| Span | span |
| Text | text node |
| Button | button |
| Input | input |
| Event | Blazor callback |
| UiStyle | CSS class/rule |
| Theme token | CSS custom property |
| NodeId | data-zui-id |
| Semantics | semantic HTML/ARIA |

普通属性更新不逐个调用 JS interop。JS 只用于 ResizeObserver、PointerLock、Clipboard、Fullscreen、File API、WebGPU canvas 等浏览器能力。

### 25.2 Blazor RenderTree

Zui.Backend.Blazor 提供 ComponentBase host：

~~~csharp
public sealed class ZuiWebRoot : ComponentBase
{
    [Parameter]
    public required UiRoot Root { get; init; }

    protected override void BuildRenderTree(
        RenderTreeBuilder builder)
    {
        WebDomRenderer.Render(
            Root,
            builder);
    }
}
~~~

Source Generator 为节点 callsite 提供稳定 sequence/NodeId，避免手工 RenderTreeBuilder 顺序错误。

### 25.3 Web CSS

Typed C# style 生成：

- Scoped class。
- Static CSS asset。
- CSS custom property。
- State pseudo-class。
- Inline style，仅用于真正动态值。

Web enhancement package 的完整 CSS 由浏览器处理，不进入 Native compiler。

### 25.4 Native Vulkan

~~~text
Portable UI Tree
  → Package enhancers
  → Cascade/style stack
  → ComputedStyle
  → Layout Tree
  → Text shaping
  → Paint Tree
  → UiPrimitive Stream
  → Vulkan UI Pass
~~~

Native backend 实现 Portable CSS，不实现完整浏览器 DOM API。

### 25.5 Native Style Performance

- Property 使用 generated PropertyId，不用字符串字典。
- Static UiStyle intern。
- ComputedStyle 缓存。
- Theme token 建立 dependency。
- Dirty bit 按 Layout、Text、Paint、Transform、Input 分类。
- Selector/style program 只在树或状态依赖变化时求值。
- 动画直接更新 motion/paint data。
- Vulkan instance buffer 只上传 dirty range。
- 稳态无变化 UI 不重新 Compose、layout 或 paint compile。

### 25.6 两后端一致性

保证：

- Component state。
- Event 意图。
- Node/key identity。
- Portable property 语义。
- Focus order 的明确规则。
- Agent role/name/action。
- Accessibility 基础信息。

不保证：

- 字体抗锯齿像素一致。
- 浏览器表单外观与 Vulkan 控件一致。
- 所有 CSS edge case 相同。
- 浏览器和原生滚动 physics 完全一致。
- Web Full/Native Extended 功能跨后端可用。

测试使用 semantic parity、layout tolerance 和每后端独立 golden。

### 25.7 Tool UI

Tool UI 可以作为 Native enhancement 继续使用 allocation-free scope API，也可以在 Web Editor Host 中由 DOM enhancement 提供对应面板。它复用 Component State、Agent schema 和 package ownership，不要求两后端使用相同绘制实现。

### 25.8 Agent

Web Agent：

~~~text
NodeId → data-zui-id → DOM element
Screenshot → browser surface
Action → Blazor/DOM event
~~~

Native Agent：

~~~text
NodeId → UiNode/Layout bounds
Screenshot → Vulkan CapturePass
Action → InputQueue/Focus
~~~

Codex 工具名和 ActionReceipt 保持一致。

### 25.9 Web 游戏视口

DOM UI 可在 Web 运行，不代表 3D renderer 已移植。未来 Browser Game Profile 需要单独的 WebGPU scene backend：

~~~text
Gameplay/ECS    .NET WebAssembly
UI              Blazor DOM/CSS
Scene           WebGPU
Agent           Web DOM + WebGPU capture
~~~

在 WebGPU backend 完成前，Web 目标只承诺 UI、工具和非 3D 应用能力。

## 26. 输入、窗口与平台

### 26.1 Web

ZEngine.Platform.Web 适配：

- Pointer、keyboard、wheel 和 touch event。
- Browser text input 和 IME composition。
- Focus 和 tab navigation。
- Clipboard、fullscreen 和 pointer lock。
- ResizeObserver、device pixel ratio 和 viewport。
- History/navigation。

普通 UI event 由 Blazor绑定；只有浏览器专属 API 通过集中 JS interop module。Web adapter 把事件归一到 InputFrame 和 portable UI event route。

### 26.2 Windows 首版

ZEngine.Platform.Win32 直接封装：

- RegisterClassEx 和窗口生命周期。
- Per-monitor DPI v2。
- Raw Input keyboard/mouse。
- WM_POINTER。
- XInput 或 GameInput adapter。
- IME composition。
- Clipboard。
- Cursor confinement。
- Fullscreen、borderless 和 windowed。
- VK_KHR_win32_surface。
- High-resolution waitable timer。

公共层不暴露 HWND，但高级插件可通过受控 NativeWindowHandle 访问。

### 26.3 Linux 后续

Linux 平台选择在 Windows P2 稳定后评估：

1. SDL3 作为窗口、输入和 Vulkan surface 后端。
2. 直接 Wayland/X11。

推荐先用 SDL3 验证 Linux，公共 API 保持 ZEngine.Platform；若 SDL 行为限制成为实际问题，再替换内部实现。SDL3 官方提供 Vulkan surface API，不需要改变 Graphics 层。

### 26.4 输入帧

平台事件进入 InputFrame：

- 当前状态。
- pressed/released edge。
- text input。
- IME composition。
- pointer positions。
- gamepad axes。
- timestamp。

Gameplay 和 Portable UI 读取 immutable InputFrame，不直接订阅 Win32 或 Browser 原始 event。

### 26.5 Action Map

~~~csharp
public sealed partial class GameActions : InputActions
{
    public partial ActionId Jump { get; }
    public partial Axis2Id Move { get; }
    public partial ActionId Pause { get; }
}
~~~

绑定资产生成强类型 ID。支持：

- Keyboard。
- Mouse。
- Gamepad。
- Rebind。
- Chord。
- Context。
- UI navigation。

## 27. 编辑器

### 27.1 编辑器就是引擎应用

Editor 使用同一：

- Vulkan renderer。
- Runtime UI primitives。
- Tool UI。
- Asset Runtime。
- Plugin Runtime。
- World 和 Scene。

不另建 WPF、WebView 或 Electron 编辑器。

### 27.2 初始面板

- Scene hierarchy。
- Inspector。
- Asset browser。
- Viewport。
- Console。
- Render Graph。
- Frame profiler。
- ECS archetypes。
- Plugin graph。
- Reload history。
- GPU resources。

### 27.3 Play Mode

首版优先独立 Runtime Process：

- Editor 保持稳定。
- Game 崩溃不带走编辑器。
- Game Module 核心变化可快速重启。
- 更容易测试发布形态。

进程内 Play Mode 可后续作为快速模式，但不能破坏隔离和热重载模型。

### 27.4 插件扩展

插件可以添加：

- Panel。
- Menu command。
- Inspector。
- Gizmo。
- Asset importer。
- Scene processor。
- Render pass。
- Debug overlay。
- Build step。

所有扩展都有 typed extension point 和 PluginScope owner。

## 28. Source Generator 与 Analyzer

### 28.1 工具项目

第一阶段集中为：

- ZEngine.Tooling。
- ZEngine.Vulkan.Generator。
- ZEngine.Plugin.Tooling。

随着构建时间和发布需要再拆。

### 28.2 生成内容

- Vulkan Raw Binding。
- Component metadata 和 serializer。
- Query accessor 和 System schedule metadata。
- Scene/Prefab schema。
- Asset IDs。
- Shader binding。
- UI State 和 callsite ID。
- Plugin manifest 和 activator。
- HotState serializer。
- Editor inspector。
- Agent action/observation ID、JSON Schema 和 typed scenario client。

### 28.3 分析器

必须诊断：

- Plugin 注册未进入 PluginScope。
- Plugin 启动 Thread。
- Plugin 向 Default ALC static event 注册。
- ECS unmanaged component 含托管引用。
- System 的 Query access 与实际写入不一致。
- UI Compose 修改 State。
- Per-frame API 捕获 lambda。
- Render pass 使用未声明资源。
- Shader C# struct layout 不匹配。
- 跨 reload boundary 暴露实现类型。
- 插件依赖环。
- Contract 版本变化未更新 SemVer。
- Resource handle 跨 generation 非法保留。
- Agent action 未通过 PluginBuilder 注册。
- Observation 执行 mutation。
- 相同 generation 中 Agent semantic ID 冲突。
- Agent action 返回 plugin implementation type。

### 28.4 反射策略

CoreCLR 允许完整反射，但使用原则：

- 启动、编辑器和诊断可反射。
- 每帧 hot path 使用生成 delegate、ID 和表。
- 反射扫描结果缓存到 generation。
- 插件卸载时缓存必须由 PluginScope 清除。

## 29. C# API 设计准则

### 29.1 安全层次

~~~text
Game / UI / ordinary plugin
  → Safe strongly typed API
Low-level render plugin
  → Advanced Vulkan-native façade
Engine backend
  → unsafe Vulkan Raw
~~~

### 29.2 泛型

泛型用于：

- GpuHandle<T>。
- AssetId<T>。
- Query<Read<T>, Write<T>>。
- typed Event。
- typed Plugin Contract。
- typed Shader Binding。
- UiComponent<TTheme>。

不用于：

- 把每种 Vulkan flags 组合编码成独立类型。
- 让每个 ECS archetype 成为编译期类型。
- 构建十几层 CRTP。
- 为形式上的零 boxing 制造难以理解的 API。

### 29.3 继承

- EnginePlugin、UiComponent 和 EditorPanel 允许浅生命周期继承。
- ECS Component 使用 struct 组合。
- System 默认 partial struct。
- Renderer feature 通过注册组合。
- 不建立 GameObject 五十层继承树。

### 29.4 委托

- 配置期和 UI change-driven Build 可以使用 lambda。
- 每帧执行委托必须缓存或 static。
- Job 和 render execute 优先 struct + interface devirtualization 或生成函数表。
- Native callback 使用函数指针和 UnmanagedCallersOnly。

### 29.5 Exception

- 初始化、资产导入、插件构建失败可以使用异常。
- 每帧 hot path 不用异常表示正常分支。
- Vulkan Raw 返回 VkResult。
- 高层在创建失败时转换为带上下文的 EngineException。
- Plugin exception 带 plugin ID 和 generation，触发隔离或回滚策略。

## 30. 诊断与性能工具

### 30.1 CPU

- EventSource/EventPipe。
- dotnet-trace。
- dotnet-counters。
- Rider profiler。
- 自定义 frame timeline。
- Job flow ID。
- Allocation attribution。

### 30.2 GPU

- Vulkan timestamp query。
- Debug Utils labels。
- RenderDoc capture。
- NVIDIA Nsight。
- AMD Radeon GPU Profiler。
- GPUView/PresentMon。

### 30.3 Engine Overlay

显示：

- CPU/GPU frame time。
- Simulation tick。
- Draw、dispatch、triangle。
- Pipeline bind。
- Descriptor update。
- VRAM budget。
- Upload queue。
- ECS archetypes/entities。
- Managed allocation 和 GC。
- Plugin generation。
- Reload duration。

### 30.4 Frame Replay

Render Graph 可导出：

- Compiled graph。
- Pass parameters。
- Resource descriptors。
- Draw packets。
- Shader hashes。
- Pipeline keys。

不默认复制所有大纹理。完整 GPU capture 交给 RenderDoc；ZEngine replay 主要重现 CPU 编译和 graph 问题。

## 31. 测试矩阵

### 31.1 Vulkan Raw

- vk.xml parser golden。
- struct sizeof 和 offset。
- enum/flags 值。
- dispatch 加载。
- extension commands。
- callback ABI。
- 与 Vulkan-Headers 小型 native probe 对比。

### 31.2 Graphics

- Instance/device/swapchain。
- Resize/minimize/restore。
- device lost。
- Validation zero-error。
- Resource generation。
- Deferred destruction。
- Memory budget。
- Render Graph barrier。
- Transient alias。
- Multi-thread recording。
- Shader/pipeline hot swap。

### 31.3 ECS

- Entity generation。
- Archetype move。
- Query read/write。
- Structural command。
- Parallel scheduler。
- Serialization。
- Plugin component reload。
- 百万实体基准。

### 31.4 Plugin

- 私有依赖版本冲突。
- Shared contract 类型身份。
- Required/Optional dependency。
- Cycle detection。
- Topological activation。
- Reverse closure unload。
- Build failure rollback。
- Configure failure rollback。
- Probation frame rollback。
- ALC WeakReference collection。
- Static event leak。
- Active job blocker。
- NativeReloadPolicy。

### 31.5 Plugin dependency reload scenarios

至少：

~~~text
A
B → A
C → B
D independent
E optional → A
~~~

验证：

- 修改 C，只重载 C。
- 修改 B implementation，重载 B、C。
- 修改 A implementation，重载 A、B、C；E 根据 optional policy 更新。
- D 不受影响。
- 修改 A contract，执行 R2 或 R3，不静默混用旧类型。
- A 新 generation 失败时，旧 A/B/C 继续运行。

### 31.6 UI

- keyed reconcile。
- Dirty propagation。
- Typed CSS value/parser/property generation。
- Portable package graph 和 platform reference diagnostics。
- Enhancement renderer/behavior ordering 与冲突。
- Blazor RenderTree sequence 和 DOM snapshot。
- Web CSS class、custom property 和 state selector。
- Web/Native semantic parity。
- Flex/Grid/box layout tolerance。
- Safe area。
- Focus/gamepad navigation。
- Virtual list。
- Chinese IME。
- Glyph atlas eviction。
- Enhancement package unload。
- 1 万节点局部变化。
- Tool UI steady allocation。

### 31.7 长时间稳定性

- 8 小时运行。
- 1 千次 shader reload。
- 1 千次 plugin reload。
- 反复 window resize/fullscreen。
- 资源流式加载与卸载。
- GPU capture 后继续运行。
- VRAM 和 managed heap 不单调增长。

### 31.8 Agent

- Screenshot frame 与 ActionReceipt.CompletedAt 对齐。
- UI semantic ID 在局部重建和 plugin reload 后保持规则一致。
- role/name 歧义返回候选，不执行随机动作。
- Engine input record/replay。
- Native input 权限和前台窗口检查。
- MCP input/output schema。
- ImageContent PNG magic bytes、尺寸和颜色空间。
- Plugin action reload 后旧 ALC 可回收。
- 进行中 action 在 plugin quiesce 时正确取消。
- Release build 不启动 Agent transport。
- 未授权 AssetMutation、PluginReload 和 NativeInput 被拒绝并审计。

## 32. 开发与发布形态

### 32.1 Development

~~~text
dotnet run --project src/Host/ZEngine.DevHost
~~~

DevHost：

- 构建 Runtime 和插件。
- 构建 Portable UI package graph。
- 启动 Blazor Web UI Lab 和 Native Vulkan UI Lab。
- 监控 shader 和 asset。
- 监控 typed style、Portable CSS 和 Web CSS。
- 启动 Editor 或 Sandbox。
- 管理 generations。
- 展示错误和 reload 状态。

Runtime 本身不重复 watch 文件。

### 32.2 Native Release

~~~text
Game/
  Game.exe
  Game.dll
  ZEngine.*.dll
  plugins/
  content/
  native/
    vulkan-related optional libraries
    dxcompiler
  game.manifest.json
  licenses/
~~~

Vulkan loader 通常由系统或驱动提供；应用打包自身明确依赖的 native helper。

### 32.3 Web Release

~~~text
Web/
  index.html
  _framework/
  zui.styles.css
  zui.packages.json
  assets/
~~~

Web 发布使用 Blazor WebAssembly/static web assets。Portable 与 Web enhancement 在构建期进入 bundle，不在浏览器运行时扫描插件目录。

### 32.4 Enhancement Package

~~~text
zui-enhancement.nupkg
  lib/
  analyzers/
  buildTransitive/
  contentFiles/
    zui.package.json
  staticwebassets/
  runtimes/
~~~

Package 可以包含 C# contract、Source Generator metadata、Portable CSS、Web CSS、Native resources 和 RID-specific native asset。

### 32.5 Plugin Package

~~~text
plugin.zplugin
  manifest.json
  contracts/
  runtime/win-x64/
  editor/win-x64/
  content/
  licenses/
  symbols/dev-only/
~~~

Release 插件包：

- 签名或 hash。
- exact dependency lock。
- 不包含源码路径和开发 secret。
- 可选 PDB 单独分发。

动态 plugin.zplugin 主要用于 DesktopDynamic Host；Web/iOS 使用构建期 package graph。

## 33. 当前仓库迁移

当前根目录已有：

- Program.cs。
- zadmin.csproj。
- zadmin.sln。
- .idea。
- bin/obj。

本次只修改文档，不迁移这些文件。

新蓝图批准后：

1. 固定 global.json 到实测 11.0.100-preview.7.26381.103。
2. 创建 P0 最小 slnx。
3. 决定现有根 console 项目迁入 samples/Triangle，还是保留为临时 smoke host。
4. 移除 InvariantGlobalization=true；游戏 UI、中文、IME 和本地化不能使用 invariant globalization。
5. 加入 AllowUnsafeBlocks，仅对需要项目启用。
6. 不在根项目默认 PublishAot。
7. 清理 bin/obj 前先确认它们仅是生成物且不含用户内容。

## 34. 分阶段路线图

### P0：Native Core Lab

交付：

- global.json 和解决方案骨架。
- Win11 x64 + RX 9070 GRE environment report。
- 最新兼容 Vulkan SDK、Validation、DXC、SPIR-V Tools 锁文件。
- vk.xml 解析与最小 C# binding generator。
- Win32 window。
- Vulkan 1.4 instance/device/swapchain。
- Validation。
- Triangle。
- CoreCLR self-contained publish。

退出条件：

- 不依赖 Silk.NET/Vortice 运行。
- 实际启用 Vulkan 1.4 和已选 feature chain。
- Resize、minimize、restore 正确。
- Validation 无错误。
- 发布目录可独立运行。

### P1：Render Graph Lab

交付：

- GpuHandle。
- allocator baseline。
- frame resources。
- Dynamic Rendering。
- Dynamic Rendering Local Read。
- Synchronization 2。
- Timeline。
- Descriptor Buffer primary path。
- Render Graph。
- HLSL/DXC/SPIR-V。
- pipeline hot reload。
- RenderDoc labels。

退出条件：

- 多 pass 正确 barrier。
- shader 编译失败保留旧 pipeline。
- 无 vkDeviceWaitIdle 日常帧路径。
- GPU 资源延迟退休通过压力测试。

### P2：ECS + Jobs Lab

交付：

- archetype/chunk。
- typed query generator。
- job scheduler。
- fixed/variable loop。
- render extraction。
- scene minimal serializer。

退出条件：

- 百万实体基准达到目标。
- 无冲突 system 并行。
- steady frame 0 B 基础分配。
- RenderWorld 与 Game World 解耦。

### P3：Plugin Reload Lab

交付：

- Plugin Contract/Runtime。
- ALC。
- Dependency DAG。
- SemVer/lock。
- PluginScope。
- immutable generation。
- R1 transaction 和 rollback。
- reverse dependency closure。

退出条件：

- A、B→A、C→B 场景全部通过。
- 1 千次 reload 无 heap/ALC 增长。
- 编译失败旧 generation 继续运行。
- plugin job 和 GPU resource 正确排空。

### P4：UI Lab

交付：

- Zui.Core、Zui.Basic 和 Zui.Sdk package graph。
- Typed DOM/CSS DSL。
- Portable Forms/Animation enhancement 样例。
- Blazor RenderTree/DOM/CSS backend。
- Native computed-style/layout/text backend。
- Vulkan UI renderer。
- Web 和 Native 两个 UiLab。
- text/glyph atlas。
- Chinese IME。
- Package enhancer/conflict diagnostics。

退出条件：

- 同一 LoginCard/MainMenu C# 组件在 Web DOM 和 Vulkan 渲染。
- Portable property semantic test 通过。
- Web 使用 Blazor批量 DOM 更新，不做逐 property JS mutation。
- 1 万 Native 节点局部更新不全树重建。
- Enhancement package reload 不留下旧 ALC/root。
- Portable 项目引用 Web/Windows 包时构建失败并给出明确诊断。

### P5：Agent Feedback Lab

交付：

- AgentDispatcher。
- Observation/Action registry。
- UI semantic tree。
- Engine input injection。
- Vulkan CapturePass。
- Web DOM/browser capture adapter。
- ActionReceipt。
- MCP AgentHost。
- Plugin Agent actions。
- C# Scenario DSL。

退出条件：

- Codex 可以查询 UI、点击、输入并收到完成帧。
- Screenshot 返回有效 ImageContent。
- Web 与 Native 使用相同 NodeId、role 和 action 名。
- 每个 mutation 返回结构化 delta、日志和性能。
- Plugin reload 后 action catalog 不引用旧 ALC。
- Agent transport 关闭时运行时无额外网络监听。
- Native input 与 Engine input 权限分离。

### P6：Asset + Editor Lab

交付：

- content-addressed asset pipeline。
- importer plugin。
- typed AssetId。
- scene/editor panels。
- viewport、inspector、console。
- play process。

退出条件：

- importer、shader、texture、scene 可独立热更新。
- Game crash 不关闭 Editor。
- Plugin graph 和 reload inspector 可用。

### P7：第一款真实游戏切片

用真实项目验证：

- Gameplay plugin。
- Render feature plugin。
- dependent plugin。
- HUD/menu。
- asset streaming。
- 发布和性能。

此阶段之后才讨论 v0.1 公共 API 冻结。

## 35. 架构停止门

### Gate A：Vulkan Raw ABI

通过前不扩展渲染器：

- vk.xml generator coverage。
- ABI probe。
- dispatch。
- validation。

### Gate B：Render Graph

通过前不做高级渲染：

- barrier 可验证。
- pass 插件化。
- transient lifetime。
- timeline retirement。
- graph inspector。

### Gate C：Plugin Reload

通过前不允许大量插件：

- ALC 真正回收。
- reverse dependency closure。
- transaction rollback。
- job、event、ECS、UI、GPU root 全覆盖。

### Gate D：UI Syntax

至少用：

- 主菜单。
- 游戏 HUD。
- Inventory。
- Render Graph editor。
- ECS inspector。

五个真实界面比较 DSL 可读性和分配，再冻结。

### Gate E：Real Game Slice

只有真实 gameplay、渲染、UI 和依赖插件一起运行，才能决定：

- ECS API。
- Plugin Contract。
- Render pass API。
- Asset format。
- UI 状态模型。

### Gate F：Agent Feedback

通过前不把 Agent 工具配置为 Codex 默认能力：

- semantic target 稳定。
- screenshot 与 action frame 对齐。
- mutation 权限。
- plugin action 卸载。
- MCP schema。
- record/replay。
- 失败时不阻塞 game loop。

## 36. 风险登记

| 风险 | 影响 | 缓解 |
|---|---|---|
| Vulkan binding generator ABI 错误 | 崩溃或静默内存破坏 | native ABI probe、Silk/Vortice 对照、Validation |
| 自有 Vulkan 层范围膨胀 | 长期只写封装不写引擎 | Raw 忠实生成，上层只封装真实模式 |
| Render Graph barrier 错误 | GPU corruption | Synchronization validation、graph replay、明确 access |
| 过早高级 GPU 技术 | 架构复杂且不可测 | Capability + benchmark gate |
| ALC 无法卸载 | 热重载内存增长 | Scope ownership、WeakReference、R3 fallback |
| 插件 contract 类型身份混乱 | 无法 cast 或旧类型泄漏 | generation contract ALC、禁止 runtime duplicate |
| 插件依赖闭包过大 | 一次修改重载过多 | 细化 contracts、service handles、optional capability |
| 插件 native DLL 不可卸载 | 进程内 reload 失败 | NativeReloadPolicy、Runtime Process restart |
| ECS component schema 变化 | 世界状态丢失 | 明确 R1/R2/R3、generated migration |
| UI DSL 仍然产生过多 lambda | CPU/GC 抖动 | change-driven build、static hot delegates、bench |
| Tool UI 每帧扫描大量数据 | 编辑器性能差 | visible range、cache、降低隐藏 panel 频率 |
| 字体和 CJK atlas 复杂 | 中文体验差 | HarfBuzz、动态 atlas、专门 TextLab |
| .NET 11 Preview 变化 | 编译器/运行时漂移 | global.json、版本升级分支、真实基准 |
| Editor 与 Runtime 耦合 | crash 与 reload 互相影响 | 默认独立 Play Runtime Process |
| Agent 截图与状态不在同一帧 | AI 得出错误结论 | FrameId/TickId correlation、CapturePass timeline |
| Agent 坐标操作不稳定 | 分辨率变化后误点 | semantic target 优先、歧义返回候选 |
| Agent mutation 权限过大 | 误改资产或运行状态 | capability permissions、Release 禁用、审计 |
| 未来平台倒逼最低公共特性 | 当前 Windows 性能路径退化 | Capability Profile、feature plugin、adapter fallback |
| iOS AOT 与动态插件冲突 | 无法保持桌面热重载 | 静态插件构建、Runtime Capability 明示 |
| 把 Portable CSS 扩成完整浏览器 | UI 工作吞掉引擎主体 | Portable/Web/Native 包边界、构建期 capability diagnostic |
| Web 与 Vulkan 布局细节不同 | 截图或交互不一致 | semantic parity、layout tolerance、每后端 golden |
| Blazor 管理节点被外部 JS 修改 | RenderTree 与 DOM 不一致 | 普通 DOM mutation 只走 Blazor renderer |
| 增强包修改基础控件契约 | 多包组合行为不可预测 | Renderer/Behavior/Variant/Decorator 限定 extension point |
| 多个增强包争夺独占 renderer | backend 冲突 | Zui.Sdk 构建期错误，不采用 last-wins |

## 37. 待审阅决策清单

括号内是本蓝图推荐。

1. 共享 UI 支持 Web Blazor DOM/CSS 与原生 Vulkan 两类后端。（接受）
2. Component、State、Event、DOM-like Node、Theme、typed CSS 和 Agent semantics 跨端共享。（接受）
3. 当前 Vulkan 只验证 Windows 11 x64 + RX 9070 GRE；Web 与其他平台在各自 Lab 建立证据。（接受）
4. 桌面原生使用 CoreCLR/JIT；Web 使用 Blazor WebAssembly；移动端按 Runtime Capability。（接受）
5. 动态插件是桌面能力；Web Wasm/iOS AOT 使用构建期增强包或重新部署。（接受）
6. 从 Khronos vk.xml 生成自有 Vulkan Raw Binding。（接受）
7. Silk.NET/Vortice 仅作对照，不进入正式公共 API。（接受）
8. Raw Binding 跟踪最新已批准 Registry；当前 Windows 主路径直接使用 Vulkan 1.4。（接受）
9. 不建立 Direct3D/Metal 风格通用 RHI。（接受）
10. Windows P0 直接 Win32；Windows Arm64、Linux、Android、macOS、iOS 只规划 adapter/RID 边界。（接受）
11. P1 以 VMA 作为 allocator baseline，保留替换边界。（接受）
12. 使用 Dynamic Rendering、Synchronization 2 和 Timeline。（接受）
13. Render Graph 管理 barrier、资源 lifetime 和 plugin pass。（接受）
14. 初始渲染路径采用 Forward+，高级路径插件化。（接受）
15. ECS 使用 archetype/chunk SoA。（接受）
16. Query 的 Read/Write 泛型驱动并行调度。（接受）
17. Render Extraction 隔离 Game World 和 Render World。（接受）
18. Shader 首版使用 HLSL + DXC → SPIR-V。（接受）
19. SPIR-V reflection 生成强类型 C# binding。（接受）
20. 不在 P0 自研 C# shader compiler。（接受）
21. 插件拆分 Contract、Runtime 和可选 Editor assembly。（接受）
22. 插件 Runtime 每个使用 collectible ALC。（接受）
23. Plugin Contract 使用 generation 级共享 Contract ALC。（接受）
24. 插件依赖是强类型 DAG，循环在 v1 拒绝。（接受）
25. 插件版本使用 SemVer、version range 和 lock file。（接受）
26. Provider 重载时重载必需 consumer 的反向依赖闭包。（接受）
27. 所有插件 contribution 必须属于 PluginScope。（接受）
28. 插件热重载使用 immutable generation 和 shadow copy。（接受）
29. 新 generation staged 验证后才在 frame safe point 切换。（接受）
30. 旧 generation 保留 probation frames，以支持回滚。（接受）
31. Native plugin 默认变化触发 Runtime Process restart。（接受）
32. Game Module 视为特殊顶级插件。（接受）
33. ECS schema 和核心 ABI 变化允许升级为 Runtime Process restart。（接受）
34. UI 使用显式 Compose(Ui<TTheme>)、DOM-like retained tree 和 typed CSS。（接受）
35. Web backend 通过 RenderTreeBuilder/Blazor diff 更新真实 DOM，不做逐 property JS mutation。（接受）
36. Native backend 实现 Portable computed style/layout/text 并输出 Vulkan primitives。（接受）
37. Portable 基础节点使用 Div、Span、Text、Button、Input 等正常 PascalCase DOM 语义。（接受）
38. UI 结构、CSS 属性、事件和子节点仍放在单个 builder scope。（接受）
39. UI 只在状态、树或 style dependency 变化时 Compose/resolve，不在每 render frame 全量重建。（接受）
40. Editor 使用同一引擎，不使用 WPF/WebView/Electron。（接受）
41. Play Mode 默认独立 Runtime Process。（接受）
42. 中文、IME 和字体 fallback 是 P4 验收，不后补。（接受）
43. 批准后移除当前 InvariantGlobalization=true。（接受）
44. 先完成 P0-P7 Labs 和真实游戏切片，再冻结公共 API。（接受）
45. 本轮只归档和规划，不改现有代码骨架。（接受）
46. Agent Control Plane 是稳定引擎能力，不是临时测试脚本。（接受）
47. Agent 优先按 UiTarget、EntityId 和 typed command 操作，坐标只是后备。（接受）
48. 截图通过 Vulkan CapturePass 回读引擎表面，不默认截取整个桌面。（接受）
49. 每个 Agent mutation 返回完成帧、结构化 delta、日志、性能和可选截图。（接受）
50. Codex 通过本地 MCP AgentHost 使用工具；Release 默认关闭。（接受）
51. 插件以 AgentAction<TRequest,TResult> 和 Observation<T> 扩展 Agent，并由 PluginScope 拥有。（接受）
52. Engine input 与 Native OS input 分为不同权限等级。（接受）
53. macOS 可通过 MoltenVK 研究 Vulkan 1.4 portability；iOS 使用 AOT 时不承诺动态插件。（接受）
54. Arm64 是 architecture dimension，不创建一套独立引擎主体。（接受）
55. 依赖选择最新兼容 stable，锁定版本、commit 和 hash，通过完整验收后升级。（接受）
56. DXC preview、Vulkan 新 extension 和高级 GPU feature 在独立实验 profile 中验证后进入主路径。（接受）
57. 所有公共 DSL 使用 PascalCase、显式 receiver、清晰动词和短重载。（接受）
58. 泛型负责契约和类型安全；浅继承负责生命周期；不使用复杂 CRTP 或泛型组合爆炸。（接受）
59. 不使用 WebOnly/NativeOnly/UiProfile 等组件级平台 attribute。（接受）
60. 平台能力由 Zui.Basic、Portable Enhancement、Backend 和 Platform Enhancement 包图决定。（接受）
61. Game.UI 只引用 Portable 包；Game.Web、Game.Windows 等 Host 引用各自平台包。（接受）
62. 增强包可以增加组件，也可以通过 RendererAdapter、Behavior、StyleVariant 和 Decorator 增强已有组件。（接受）
63. 平台专属新组件通过 ui.Web、ui.Windows、ui.Android、ui.Apple facet 暴露。（接受）
64. Package manifest + Zui.Sdk Source Generator 生成静态 registry，不要求用户手工 AddXxx。（接受）
65. 一个 backend/component 只有一个 primary renderer；冲突是构建错误。（接受）
66. Portable CSS 文件在构建期解析和验证；Web 增强包可以携带完整浏览器 CSS。（接受）
67. 原生端不承诺完整 W3C DOM/CSS 或 Chrome 像素级兼容。（接受）
68. Web DOM UI 不等于 Web 3D renderer；浏览器游戏场景需要未来独立 WebGPU backend。（接受）

## 38. 官方资料与研究依据

核验日期：2026-08-26。

### 38.1 .NET 和 C#

- [.NET plugin tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)：自定义 AssemblyLoadContext、AssemblyDependencyResolver 和共享 plugin contract。
- [Assembly unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)：collectible ALC 是协作卸载；线程栈、实例、Type、Assembly、GCHandle 等强引用都会阻止回收。
- [AssemblyLoadContext shared dependencies](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)：共享类型必须来自同一 Assembly 实例，适合稳定 contract assembly。
- [C# unsafe and function pointers](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code)：delegate* 可通过 calli 调用 native function pointer。
- [.NET native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)：函数指针、UnmanagedCallersOnly、struct 映射和明确 native lifetime。
- [.NET SIMD](https://learn.microsoft.com/en-us/dotnet/standard/simd)：System.Numerics、Vector 和 hardware intrinsics 的分层使用。
- [NuGet dependency resolution](https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution)：版本区间、锁定和确定性解析的参考。

### 38.2 Vulkan

- [Khronos Vulkan API Registry](https://github.com/KhronosGroup/Vulkan-Docs/blob/main/registry.adoc)：vk.xml 是机器可读 Registry，可用于其他语言 binding generator。
- [Vulkan versions guide](https://docs.vulkan.org/guide/latest/versions.html)：Vulkan 1.2/1.3/1.4 核心能力和迁移关系。
- [Vulkan Synchronization 2](https://docs.vulkan.org/guide/latest/extensions/VK_KHR_synchronization2.html)：更清楚的 stage/access、barrier 和 submission。
- [Vulkan threading guide](https://docs.vulkan.org/guide/latest/threading.html)：应用负责 host 多线程；command pool 需要外部同步。
- [Command buffer performance sample](https://docs.vulkan.org/samples/latest/samples/performance/command_buffer_usage/README.html)：per-thread pool、并行录制和 reset pool 的实际取舍。
- [NVIDIA Vulkan recommendations](https://developer.nvidia.com/blog/?p=14696)：并行 command recording、pipeline cache、suballocation、barrier 和 queue submission。
- [AMD Vulkan barrier guide](https://gpuopen.com/learn/vulkan-barriers-explained/)：精确 producer/consumer stage 避免无谓 pipeline bubble。
- [Vulkan Memory Allocator](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator)：Vulkan block suballocation 的成熟基线。

### 38.3 Shader

- [Microsoft DirectX Shader Compiler](https://github.com/microsoft/DirectXShaderCompiler)：HLSL 可生成用于 Vulkan 的 SPIR-V。
- [DXC HLSL to SPIR-V mapping](https://github.com/microsoft/DirectXShaderCompiler/blob/main/docs/SPIR-V.rst)：binding、location、push constant 和 reflection 规则。
- [SPIRV-Reflect](https://github.com/KhronosGroup/SPIRV-Reflect)：descriptor、push constant、IO 和 layout reflection。

### 38.4 UI 与现有引擎

- [Blazor RenderTree construction](https://learn.microsoft.com/en-us/aspnet/core/blazor/advanced-scenarios)：RenderTreeBuilder 可以使用纯 C# 手工构造 element/component tree。
- [Blazor JavaScript interoperability](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/)：Blazor 管理的 DOM 不应被外部 JS 任意修改，否则内部表示与真实 DOM 可能不一致。
- [Blazor CSS isolation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/css-isolation)：CSS scope 和 selector rewriting 在构建期完成，可作为 Web package 样式输出参考。
- [Dear ImGui paradigm](https://github.com/ocornut/imgui/wiki/About-the-IMGUI-paradigm)：Immediate Mode 指 API 的状态所有权，不等于立即绘制，也不排斥内部 retained cache。
- [Unity UI Toolkit](https://docs.unity3d.com/2023.2/Documentation/Manual/UIElements.html)：retained tree、CSS-like style 和 editor/runtime 共用 UI 的参考。
- [Stride](https://www.stride3d.net/features/)：现有 C# 引擎的 Vulkan、多线程、shader/script hot reload 和可扩展编辑器先例。
- [Godot design philosophy](https://docs.godotengine.org/en/latest/getting_started/introduction/godot_design_philosophy.html)：编辑器使用引擎自身 UI、场景和插件能力的先例。
- [SDL3 Vulkan surface](https://wiki.libsdl.org/SDL3/SDL_Vulkan_CreateSurface)：Linux 后续平台适配候选。

### 38.5 Agent 与 MCP

- [MCP Tools specification](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)：工具使用 JSON Schema，可返回 structured content 和 ImageContent。
- [MCP C# SDK image tools](https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html)：C# MCP server 可以直接返回 PNG 等图像内容。
- [OpenAI Docs：Codex MCP](https://developers.openai.com/codex/mcp)：Codex 支持 STDIO 与 Streamable HTTP MCP，并共享 config.toml 配置。

这些资料支持工具与图片传输，不会自动解决 action frame correlation、Vulkan readback、UI semantic tree 或 plugin action unload；这些属于 ZEngine 设计。

### 38.6 平台与当前依赖

- [MoltenVK](https://github.com/KhronosGroup/MoltenVK)：在 macOS、iOS 和 Apple Silicon 上提供 Vulkan 1.4 portability subset，并将 SPIR-V 转换到 Metal。
- [.NET RID catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)：win-arm64、linux-arm64、android-arm64、osx-arm64 和 ios-arm64 等目标标识。
- [.NET NativeAOT limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)：AOT 不支持 Assembly.LoadFile 等动态程序集加载。
- [Android Vulkan overview](https://developer.android.com/games/develop/vulkan/overview)：Android Vulkan 需要按设备和 Baseline Profile 验证。
- [Vulkan Validation releases](https://github.com/KhronosGroup/Vulkan-ValidationLayers/releases)：当前 SDK/Validation 发布系列。
- [VMA releases](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator/releases)：当前 3.4.0 基线。
- [DXC releases](https://github.com/microsoft/DirectXShaderCompiler/releases)：稳定和 preview compiler 应分开锁定。
- [SPIR-V Tools releases](https://github.com/KhronosGroup/SPIRV-Tools/releases)：validator、optimizer 和 disassembler 版本来源。
- [SDL releases](https://github.com/libsdl-org/SDL/releases)：未来 Linux/Android platform adapter 候选。

### 38.7 哪些是我们的设计推导

资料直接支持底层能力，但不会自动提供：

- ZEngine 的 generational GpuHandle。
- Render Graph DSL 和 plugin slot。
- Plugin Contract ALC + per-plugin Runtime ALC。
- Reverse dependency closure reload。
- Staged generation、probation 和 rollback。
- PluginScope 全资源所有权。
- ECS schema hot state。
- Basic/Portable/Platform Enhancement package graph。
- typed CSS 的固定 style stack。
- 同一 NodeId 在 Blazor DOM 与 Vulkan tree 的投影。
- Package enhancer conflict resolution。
- Portable CSS 编译到 Web asset 与 Native style program。

这些必须通过 P0-P7 实证，不能因为底层 API 存在就视为已经完成。

## 39. 建议的审阅顺序

第一轮先审六个根决策：

1. 共享 C# UI 同时面向 Blazor DOM/CSS 与 Vulkan。
2. Basic/Portable/Platform Enhancement 分包，不使用组件平台 attribute。
3. Portable CSS 边界与 Web Full/Native Extended 能力。
4. CoreCLR/JIT、WebAssembly 和移动 AOT 的 Runtime Capability 差异。
5. vk.xml 自生成 Raw Binding、插件依赖与热重载。
6. Agent 在 Web/Native 后端使用同一 NodeId 和 action contract。

第二轮审公共语法：

- Plugin declaration。
- ECS Query。
- Render Graph Pass。
- Typed DOM/CSS UI。
- Package enhancer。
- Tool UI。
- AssetId 和 Shader Binding。

第三轮审工程计划：

- P0-P7。
- 测试矩阵。
- 性能预算。
- 当前 Windows 验收与未来 adapter 范围。

蓝图批准前不应：

- 创建全部占位项目。
- 开始大规模 Vulkan wrapper。
- 引入 ECS、UI 或插件第三方框架作为永久依赖。
- 把 Silk.NET/Vortice 类型写进业务 API。
- 决定所有高级渲染 feature。
- 修改或删除现有根代码。

## 40. 当前 Win11 Vulkan 验证实验室

### 40.1 已实测硬件

核验日期：2026-08-26。

~~~text
OS                   Windows 11 x64
GPU                  AMD Radeon RX 9070 GRE
GPU device ID        0x7550
Driver               AMD proprietary 26.8.1 LLPC
Vulkan loader        1.4.341
Vulkan device API    1.4.349
Conformance          1.4.3.3
Device-local heap    about 11.94 GiB
~~~

设备还报告：

- Dynamic Rendering Local Read。
- Maintenance 5/6。
- Push Descriptor。
- Descriptor Buffer。
- Mesh Shader。
- Ray Query。
- Ray Tracing Pipeline。

这些是当前唯一可以写入验收报告的图形能力。WinArm64、Linux、Android 和 Apple 平台目前没有任何运行证据。

### 40.2 当前缺失的开发工具

系统有 vulkaninfo 和 Vulkan loader，但当前 PATH 中未发现：

- DXC。
- SPIR-V Tools。
- glslc。
- RenderDoc CLI。
- VK_LAYER_KHRONOS_validation。

P0 工具链准备需要安装或仓库自带确定版本，之后再次生成 environment report。没有 Validation Layer 不影响驱动支持 Vulkan 1.4，但不能开始正式 Raw Binding 和 Render Graph 验收。

### 40.3 依赖更新策略

原则不是长期停留在蓝图编写时的版本，也不是每次构建下载 latest：

1. 建立版本更新分支。
2. 选择当时最新兼容 stable。
3. 锁定版本、commit 和下载 hash。
4. 运行 ABI、shader、validation、GPU 和 reload 验收。
5. 通过后更新主锁文件。

2026-08-26 研究快照：

| 依赖 | 当前候选 |
|---|---|
| .NET SDK | 11.0.100-preview.7.26381.103 |
| ASP.NET Core / Blazor | .NET 11 Preview 7 SDK family |
| Vulkan SDK/Validation family | 1.4.357.0 |
| VMA | 3.4.0 |
| DXC stable | 1.9.2607 |
| DXC experimental | 1.10.2605.x，仅独立实验 |
| SPIR-V Tools | 2026.2 |
| SDL3 | 3.4.14，仅未来平台 adapter 候选 |
| MoltenVK | 1.4.x，等待真实 Apple 环境再锁定 |
| MCP C# SDK | Agent Lab 时选择最新兼容 stable 并锁定 |

Raw Binding 始终由锁定的最新 vk.xml 生成，不因为本机 loader patch 较低就降级 header。Feature 使用由 apiVersion、feature struct 和 extension query 决定。

### 40.4 Web UI 尚待验证

当前仓库没有 Blazor WebAssembly Host、RenderTree adapter、Portable CSS compiler 或 Browser Agent，因此 Web UI 仍是架构目标，不是已验证结果。UI Lab 必须提供 clean build、browser render、DOM snapshot、CSS output、input/IME 和 Agent 操作证据后才能标记 Web backend 可用。

## 41. Agent Control Plane

### 41.1 目标

Agent 能力让 Codex、自动化测试和未来其他 AI 能形成闭环：

~~~text
Observe
  → Decide
  → Act at engine safe point
  → Wait for resulting frame
  → Return structured delta + image + diagnostics
~~~

它不是远程桌面替代品，也不是允许任意内存修改的调试后门。

### 41.2 稳定核心

AgentDispatcher 位于 Engine Host 的稳定程序集，不随游戏插件卸载。它持有：

- Observation registry。
- Action registry。
- Request queue。
- Frame correlation。
- Screenshot readback queue。
- Capability manifest。
- Permission policy。
- Action history。

MCP、命令行测试和编辑器面板只是 transport/client，不拥有执行语义。

### 41.3 观察能力

内置观察：

- Engine status。
- Current frame/tick。
- Screenshot。
- UI semantic tree。
- Focused UI element。
- Hit-test result。
- Scene hierarchy。
- ECS entity/component snapshot。
- Selected entity。
- Asset status。
- Plugin graph and generations。
- Render Graph。
- CPU/GPU frame statistics。
- Logs、warnings、exceptions。
- Input state。

Observation 都带 FrameId、TickId、GenerationId 和 Timestamp，防止 AI 把不同帧的数据误当成同一状态。

### 41.4 操作能力

内置动作：

- Click semantic UI target。
- Pointer move/down/up。
- Text input。
- Key press/release。
- Scroll。
- Gamepad button/axis。
- Focus。
- Activate menu command。
- Select entity。
- Set inspector property，需 EditorWrite 权限。
- Load scene。
- Pause、resume、step frame。
- Request asset/plugin reload。

操作只进入 Engine InputQueue 或 Editor CommandQueue，由主线程在明确 phase 执行；MCP 线程不能直接修改 World、UI tree 或 Vulkan resource。

### 41.5 语义目标优先

优先：

~~~csharp
await agent.Click(UiTarget.Id<Button>("main-menu.continue"));
~~~

然后：

~~~csharp
await agent.Click(UiTarget.Role(
    UiRole.Button,
    name: "继续游戏"));
~~~

最后才是坐标：

~~~csharp
await agent.Click(ScreenPoint.Pixels(840, 612));
~~~

若 name/role 匹配多个节点，返回候选列表和边界，不随机点击。

### 41.6 ActionReceipt

每次 mutation 返回：

~~~csharp
public sealed record ActionReceipt(
    ActionId Action,
    FrameId AcceptedAt,
    FrameId CompletedAt,
    ActionStatus Status,
    UiDelta Ui,
    SceneDelta Scene,
    IReadOnlyList<LogEvent> Logs,
    FrameStatistics Performance,
    CaptureRef? Screenshot);
~~~

默认只返回结构化 delta。调用方可要求同时截图、命中标记或前后对比图。

### 41.7 Backend Screenshot

Web：

- Browser/Blazor Agent adapter 捕获指定 DOM root、viewport 或 WebGPU canvas。
- NodeId 映射到 data-zui-id。
- Capture 结果与 Blazor render generation/FrameId 对齐。

Native：

Render Graph 插入受控 CapturePass：

1. 选择 Swapchain、Viewport、UI layer 或 named render resource。
2. 处理 layout transition。
3. Copy image 到 readback buffer。
4. 由 timeline semaphore 标记完成。
5. 后台编码 PNG；HDR 可选 EXR。
6. 返回 MCP image content 或本地 artifact reference。

两端都只捕获引擎授权表面，不默认截取整个桌面，并返回相同 CaptureRef/ActionReceipt。

### 41.8 插件扩展 Agent

插件通过 PluginBuilder 注册强类型 action/observation：

~~~csharp
plugin.Agent.AddAction<CastRayAgentAction>();
plugin.Agent.AddObservation<PhysicsWorldObservation>();
~~~

~~~csharp
public sealed class CastRayAgentAction
    : AgentAction<CastRayRequest, RayHitResult>
{
    public override ValueTask<RayHitResult> Execute(
        CastRayRequest request,
        AgentActionContext context)
    {
        return ValueTask.FromResult(
            context.World.CastRay(request));
    }
}
~~~

Source Generator 产生：

- Stable action ID。
- JSON Schema。
- C# test client。
- MCP metadata。
- Permission annotation。
- Plugin generation owner。

插件重载时，旧 action 先停止接收请求；进行中的请求受 PluginScope cancellation 和 quiesce 约束。

## 42. Codex/MCP 适配

### 42.1 项目

~~~text
ZEngine.Agent.Abstractions
ZEngine.Agent.Protocol
ZEngine.Agent.Runtime
ZEngine.Agent.Transport.Ipc
ZEngine.Agent.Transport.Mcp
ZEngine.Agent.Testing
ZEngine.AgentHost
~~~

### 42.2 稳定 MCP 工具

首批工具：

| Tool | 作用 |
|---|---|
| engine_status | Runtime、scene、plugin generation 和 GPU 状态 |
| engine_observe | 一次请求组合多个 observation |
| engine_capture | 返回 PNG/EXR 或 artifact |
| engine_ui_tree | 查询 UI semantic tree |
| engine_click | semantic target 或坐标点击 |
| engine_type | 输入文字和 IME 测试 |
| engine_key | 键盘按下/释放 |
| engine_scroll | 鼠标滚轮或触控滚动 |
| engine_gamepad | gamepad action |
| engine_wait | 等待 UI、scene、log 或 frame predicate |
| engine_pick | 屏幕点命中 UI/entity/render object |
| engine_scene_query | 查询 entity/component |
| engine_logs | 增量日志 |
| engine_frame_stats | CPU/GPU 时间和分配 |
| engine_run_scenario | 执行受限、可记录场景 |

MCP Tool 返回 output schema；截图使用 ImageContent，结构化状态使用 structuredContent。

### 42.3 Transport

开发期优先：

- Windows named pipe 或 loopback streamable HTTP。
- 可选 STDIO，由 DevHost 启动 AgentHost。
- Linux 使用 Unix domain socket 或 loopback。
- Mobile 只在显式开发构建中启用受认证 transport。

不绑定公网地址，不默认接受局域网连接。

### 42.4 Plugin 动态能力

当 MCP client 支持 tool list change 时，AgentHost 可以在 plugin generation 切换后更新 tool catalog。为兼容静态 client，稳定工具 engine_observe 和 engine_run_action 也可以通过 action ID 加 generated schema 调用插件能力。

### 42.5 权限

~~~text
Observe
Input
EditorRead
EditorWrite
RuntimeControl
AssetMutation
PluginReload
NativeInput
~~~

- Release 默认完全禁用 AgentHost。
- Development 默认只开放 Observe、Input、EditorRead。
- 写资产、重载插件和原生输入需要显式授权。
- 每个 action 记录 caller、参数摘要、目标 frame 和结果。
- 不返回未授权的文件路径、环境变量或任意进程内存。

### 42.6 两种输入模式

Engine 模式：

- 直接进入 InputQueue。
- 确定、可重放、跨平台。
- 默认。

Native 模式：

- Win32 SendInput 或未来平台等价物。
- 验证窗口焦点、IME 和 OS 事件链。
- 只在显式 NativeInput 权限和前台测试窗口中使用。

### 42.7 C# Scenario DSL

~~~csharp
await scenario
    .Click(GameUi.MainMenu.Continue)
    .WaitFor(GameState.Playing)
    .Capture("after-continue")
    .AssertNoErrors()
    .AssertFrameTimeUnder(TimeSpan.FromMilliseconds(16.67));
~~~

这是生成的强类型测试客户端，不是把字符串脚本塞进引擎解释。

## 43. 未来平台适配目录

### 43.1 只分配边界，不写未验证实现

~~~text
src/Native/
  ZEngine.Platform.Abstractions/
  ZEngine.Platform.Win32/
  ZEngine.Platform.WindowsArm64/
  ZEngine.Platform.Linux/
  ZEngine.Platform.Android/
  ZEngine.Platform.Apple.Common/
  ZEngine.Platform.MacOS/
  ZEngine.Platform.iOS/

src/Graphics/
  ZEngine.Graphics.Vulkan/
  ZEngine.Graphics.Vulkan.Portability/

src/Web/
  ZEngine.Platform.Web/
  ZEngine.Web.Interop/
  ZEngine.Web.Agent/

eng/rids/
  browser-wasm/
  win-x64/
  win-arm64/
  linux-x64/
  linux-arm64/
  android-arm64/
  osx-x64/
  osx-arm64/
  ios-arm64/
~~~

蓝图批准后 P0 只创建实际参与 Windows Vulkan build 的项目；Web 项目在 UI Lab 建立。其他目录可以先由文档占位，不创建无法编译的空 csproj。

### 43.2 平台矩阵

| 平台 | UI/Graphics backend | .NET runtime | 动态插件 |
|---|---|---|---|
| Browser | Blazor DOM/CSS；未来 WebGPU scene | .NET WebAssembly | 构建期增强包 |
| Windows x64 | Vulkan 1.4 UI/scene，当前图形验证 | CoreCLR/JIT | 完整目标 |
| Windows Arm64 | 驱动 Vulkan，未验证 | CoreCLR/JIT | 计划 |
| Linux x64/Arm64 | Vulkan loader + Wayland/X11/SDL adapter | CoreCLR/JIT | 计划 |
| Android Arm64 | Android Vulkan loader/surface | .NET 11 CoreCLR/AOT 以设备为准 | 受平台政策限制 |
| macOS x64/Arm64 | MoltenVK Vulkan 1.4 portability subset | CoreCLR/JIT | 可研究 |
| iOS Arm64 | MoltenVK + Metal | AOT 为主 | 不支持运行时程序集插件 |

### 43.3 主体不变的含义

保持不变：

- ECS。
- Scene。
- Asset schema。
- Render Graph 逻辑模型。
- UI composition。
- DOM-like Node、typed CSS 和 package contract。
- Plugin contracts。
- Agent protocol。

平台必须适配：

- Window/surface。
- Input/IME。
- Filesystem sandbox。
- Dynamic library。
- Runtime loading/AOT policy。
- Vulkan feature profile。
- App lifecycle。

### 43.4 Apple 边界

MoltenVK 提供 Vulkan 1.4 的 portability subset 并把 SPIR-V 转换为 Metal Shading Language。它不是 Apple 原生 Vulkan driver，因此必须：

- 启用 portability enumeration/subset。
- 对不支持 feature 提供 render feature fallback。
- 单独验证 shader 和同步行为。

iOS 的 AOT 环境不能动态 Assembly.LoadFile，因此动态插件和进程内热重载必须转换为构建期静态插件与重新部署。不能用文件夹结构掩盖这个差异。

### 43.5 ARM 不是独立平台

Arm64 是 architecture dimension。数学、序列化和资产格式不能依赖 x64 layout；SIMD 使用：

- System.Numerics 通用路径。
- X86 intrinsics 可选路径。
- Arm intrinsics 可选路径。
- Scalar reference implementation。

所有向量化优化必须与 reference path 做 bit/epsilon 验证。

## 44. DSL 统一语法原则

### 44.1 统一语法表

| 语义 | 统一形态 |
|---|---|
| 创建/组合名词 | PascalCase：Div、Span、Button、Input、Pass |
| 资源访问 | Read、Write、Create、Import |
| 注册 | Add、Provide、Require |
| 事件 | OnClick、OnInput、OnChange；游戏增强可增加 OnPress |
| 执行 | Execute、Run、Schedule |
| 类型安全 | 泛型参数、typed handle、generic attribute |
| 生命周期 | UiComponent、RenderPass、EnginePlugin 浅继承 |
| 样式/属性 | typed carrier，setter 不跨属性链 |
| 热路径 | static delegate、生成器缓存、struct data |

### 44.2 不采用的风格

- 全大写 PANEL、WRITE、PASS。
- 隐藏的全局 current builder。
- 大量字符串 ID。
- 为一句普通操作要求三参数 lambda。
- 每个 property 都返回整个 element 的无限 fluent chain。
- 把内部 Vulkan flags 直接暴露给游戏 UI。
- 用反射式 service locator 代替构造函数依赖。

### 44.3 UI

~~~csharp
protected override void Compose(Ui<GameTheme> ui)
{
    ui.Div(root =>
    {
        root.Display.Flex();
        root.FlexDirection.Column();
        root.JustifyContent.Center();
        root.AlignItems.Center();
        root.MinHeight.Vh(100);
        root.BackgroundImage.Set(GameAssets.MenuBackground);
        root.Padding.SafeArea();

        root.Div(menu =>
        {
            menu.Use(GameStyles.MenuCard);
            menu.Width.Rem(28);
            menu.MaxWidth.Percent(90);
            menu.Gap.Rem(0.75f);

            menu.H1("ZEngine", title =>
            {
                title.FontFamily.Display();
                title.Color.TextPrimary();
            });

            menu.Button("继续游戏", onClick: ResumeGame);

            menu.Button("设置", button =>
            {
                button.Variant.Secondary();
                button.OnClick(OpenSettings);
            });
        });
    });
}
~~~

简单 Button 使用短重载；需要设置多个属性时才进入 builder。父节点就是子节点的 receiver，结构不依赖隐式上下文。

### 44.4 Builder 继承和能力接口

~~~text
UiNode<TTheme>
  ├─ Element<TElement, TTheme>
  │    ├─ BoxElement<TElement, TTheme>
  │    │    ├─ Div<TTheme>
  │    │    ├─ Section<TTheme>
  │    │    └─ Form<TTheme>
  │    ├─ InlineElement<TElement, TTheme>
  │    │    ├─ Span<TTheme>
  │    │    └─ Text<TTheme>
  │    └─ InteractiveElement<TElement, TTheme>
  │         ├─ Button<TTheme>
  │         ├─ Input<TTheme>
  │         └─ TextArea<TTheme>
  └─ ComponentNode<TComponent, TTheme>
~~~

浅继承提供 DOM/box/interactive 的真实共性；Children、Focus、TextStyle、WorldSpace 和平台 facet 使用小接口或组合 carrier。增强包通过 extension block 增加 API，setter 返回 void，不需要 CRTP。

### 44.5 Render Graph

~~~csharp
graph.Pass<LightingData>("Scene.Lighting", pass =>
{
    pass.Data.Scene = pass.Read(scene);
    pass.Data.Target = pass.Write(hdr, ColorAttachment.Load());
    pass.Execute(RenderLighting);
});
~~~

RenderLighting 是静态方法组，常规代码不需要书写显式 static 三参数 lambda。复杂 pass 继承 RenderPass<TData>。

### 44.6 ECS

~~~csharp
public void Update(
    in FrameTime time,
    Query<Write<Transform>, Read<Velocity>> query)
{
    foreach (var chunk in query.Chunks)
    {
        var transforms = chunk.Write<Transform>();
        var velocities = chunk.Read<Velocity>();
        Movement.Integrate(transforms, velocities, time.FixedDelta);
    }
}
~~~

Read/Write 在签名中同时表达类型和调度权限。

### 44.7 Plugin

~~~csharp
[Plugin<PhysicsContract>("zengine.physics", "1.2.0")]
[Requires<InputContract>("[1.0.0,2.0.0)")]
public sealed partial class PhysicsPlugin(IInput input) : EnginePlugin
{
    protected override void Configure(PluginBuilder plugin)
    {
        plugin.Provide<IPhysicsWorld>(new PhysicsWorld());
        plugin.Systems.Add<PhysicsSystem>();
        plugin.Rendering.Add<PhysicsDebugPass>();
        plugin.Agent.AddObservation<PhysicsObservation>();
    }
}
~~~

泛型 attribute 表达 contract，主构造函数表达 service dependency，继承表达生命周期，PluginBuilder 表达可逆 contribution。

### 44.8 DSL 验收标准

每个公共 DSL 必须同时满足：

- Rider 补全能够自然找到下一步。
- 普通 C# 开发者不读生成代码也能理解。
- 错误落在用户源码位置。
- 简单操作有短重载。
- 复杂操作仍在单个局部 scope。
- 不在稳态帧分配。
- 可以被插件拥有和热卸载。
- 可以生成 Agent semantic metadata。

## 45. 一句话架构

ZEngine 是一个共享强类型 C# 引擎与 UI 平台：Component、State、Event、DOM-like Node、Theme、typed CSS 和 Agent semantics 由 Basic/Portable Enhancement 包提供；Web Host 通过 Blazor WebAssembly 把同一 UI tree 投影到真实 DOM/CSS，原生 Host 通过 computed style、layout、text 和 Vulkan 1.4 renderer 呈现；Web、Windows、Linux、Android 和 Apple 增强包可以扩展已有组件或增加专属组件，但不依赖组件 attribute；游戏、编辑器、渲染和 Agent 扩展仍使用强类型 Contract、SemVer 依赖、作用域所有权和 generation 热重载，当前 Vulkan 性能只在 Win11 x64 + Radeon RX 9070 GRE 上验收。
