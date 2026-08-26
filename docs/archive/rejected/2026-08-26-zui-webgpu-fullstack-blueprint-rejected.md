# 已归档：ZUI 纯 C#、WebGPU 优先的跨平台全栈 UI 蓝图

> 状态：Archived / Rejected  
> 归档日期：2026-08-26  
> 否决原因：该方案仍以浏览器为主要宿主，无法摆脱 DOM 语义桥、隐藏输入节点、JavaScript 启动与 WebGPU 互操作边界；这些约束与新的“桌面原生 Vulkan、强类型 C# 游戏引擎、插件依赖和插件热重载”目标不一致。  
> 替代方向：参见 [ZEngine 统一 C# UI、Blazor DOM/CSS 与 Vulkan 游戏引擎蓝图](../../architecture/zengine-unified-csharp-ui-blazor-vulkan-blueprint.md)。  
>
> 本文件只保留为决策历史，不应继续作为实现依据。
>
> 目标基线：.NET 11 Preview 7、C# Preview、WebGPU  
> 首要平台：现代浏览器  
> 同步验证平台：Windows 11 x64  
> 历史目标仓库：C:\Users\lionheart\RiderProjects\zadmin
> 本文性质：架构与产品蓝图，不代表已经批准的最终公共 API

## 0. 文档目的

本文把此前讨论的 UI DSL、响应式运行时、WebGPU 渲染、系统字体、浏览器客户端渲染、Windows 桌面、无显式接口的全栈开发、单体部署、热重载和工程组织统一为一份可以逐条审阅的蓝图。

这份蓝图首先回答五个问题：

1. 这条技术路线是否可行。
2. 每一层实际负责什么，以及哪些能力明确不做。
3. 开发者最终书写的 C# 代码是什么形态。
4. 浏览器和 Windows 如何共用绝大部分代码，又如何保留合理的平台差异。
5. 如何用阶段性原型和停止门避免一次性造出一个无法验证的大框架。

本文不是对任何现有框架的照搬。Svelte、SvelteKit、ASP.NET Core、DirectWrite、浏览器 Canvas/WebGPU 和现有 C# UI 框架都只作为可验证的设计来源；最终 API 必须服从纯 C#、强类型、浏览器优先和可调试性这几个目标。

## 1. 结论摘要

### 1.1 可行性结论

该方案可行，但它本质上不是一个小型控件库，而是一个新的 UI 运行时和全栈应用平台。最困难的部分不是调用 WebGPU，而是以下四项工作的组合：

- 在 C# 中建立足够简洁、可静态分析的 UI DSL。
- 建立细粒度响应式图，并把更新精确映射到布局、场景和 GPU 资源。
- 建立可用的文本、字体、输入法、焦点和无障碍系统。
- 同时维持浏览器 WebAssembly、ASP.NET Core 服务端和 Windows NativeAOT 的工程闭环。

建议继续推进，但采用“窄切片验证、逐层冻结 ABI”的方式，而不是先实现大量控件。第一个真正的里程碑应是：

> 同一份纯 C# 页面代码，在浏览器和 Windows 11 显示中文、响应状态变化、触发同文件夹内的服务端函数，并且页面代码与 WGSL 都能热更新。

如果这个纵向切片无法达到可接受的开发体验和性能，再多的控件都没有价值。

### 1.2 推荐的总体选择

| 领域 | 推荐选择 | 原因 |
|---|---|---|
| UI 表达 | 大写节点函数加单一 builder 作用域 | 结构、样式、事件和子节点在同一处可读 |
| 响应式 | 编译期状态槽加运行时动态依赖图 | 保留普通 C# 控制流，同时实现细粒度更新 |
| 浏览器渲染 | WebGPU 画布加最小语义 DOM | 获得 GPU 控制力，同时保留输入法和无障碍能力 |
| Windows 渲染 | 同一场景协议加原生 WebGPU 后端 | 共用布局、场景和控件逻辑，避免 WebView 依赖 |
| 文本 | 浏览器系统文本栅格化桥接；Windows DirectWrite | 不自带大型中日韩字体，遵循各平台字体和回退 |
| 全栈调用 | 源生成的 Server Function 代理 | 业务代码不手写 URL、DTO 客户端或接口胶水 |
| 部署 | ASP.NET Core 单发布目录内置浏览器包 | 不需要单独部署前端服务器 |
| 热重载 | DevHost 统一协调不可变构建代次 | 避免浏览器、服务端和契约版本互相错配 |
| 发布 | 浏览器 Wasm AOT；Windows NativeAOT | 发布性能和可分发性优先 |
| 调试 | Browser Mono/CoreCLR 路径；Windows CoreCLR | AOT 不适合作为日常热重载开发宿主 |

### 1.3 非目标

首版明确不追求：

- 完整兼容 HTML、DOM 或 CSS。
- 让任意现有 Razor、Blazor、React、Svelte 组件直接嵌入 ZUI 场景树。
- 浏览器和 Windows 像素级完全一致。
- 在 GPU 上执行组件差异比较、通用布局或响应式依赖计算。
- 默认打包完整中文字体。
- 在浏览器客户端包含服务端实现、数据库连接或密钥。
- 用大量注解代替清晰的 C# 类型、构造函数和控制流。
- 首版就提供插件沙箱、SSR、SEO 页面生成、复杂矢量编辑器或完整 CSS 排版。

## 2. 核心设计原则

### 2.1 纯 C# 是语义约束，不只是文件扩展名

纯 C# 意味着：

- UI 结构由 C# 函数和类型表达。
- 不出现 HTML 模板字符串、Template 字符串、字符串事件名或字符串属性路径。
- 编译器能够检查事件参数、属性类型、主题令牌和服务端函数参数。
- 常规的 if、switch、foreach、模式匹配、泛型和委托就是 UI 组合语言。
- 生成器减少重复代码，但生成结果必须可诊断、可导航、可在构建产物中查看。

### 2.2 局部可读性优先

一个盒子的尺寸、背景、布局、事件、语义和子节点应尽量出现在该盒子的一个作用域内。开发者阅读页面时不需要在结构 DSL、样式 DSL、单独 CSS 文件和事件映射之间来回跳转。

这不等于所有代码都写在一个巨型方法中。重复视觉模式应提取为强类型组合函数；有独立状态和生命周期的部分应提取为组件。

### 2.3 平台共享逻辑，平台承认差异

以下内容应跨平台共享：

- DSL 和生成的组件描述。
- 响应式图。
- 布局算法。
- 控件行为状态机。
- 场景树、绘制列表和帧数据协议。
- 主题和设计令牌。
- 服务端函数契约。

以下内容允许平台实现不同：

- WebGPU 设备创建和表面呈现。
- 系统字体发现、字形整形和栅格化。
- 输入法、剪贴板、拖放、窗口和无障碍桥接。
- 文件选择器、通知、系统菜单等平台能力。

### 2.4 编译期生成减少胶水，运行时保持可解释

源生成器负责稳定且可推导的工作，例如：

- 状态槽和组件元数据。
- 主题令牌载体。
- 路由表。
- Server Function 客户端代理、服务端分派器和序列化上下文。
- 热重载边界和状态迁移元数据。
- DSL 诊断和无障碍诊断。

运行时仍应暴露清晰的节点、依赖、布局、绘制和网络诊断，而不能把所有行为藏进不可观察的生成代码。

### 2.5 构建阶段纯净，副作用有所有者

组件 Build 只描述当前状态对应的 UI，不允许在 Build 中执行：

- 写状态。
- 调用远程 Server Function。
- 启动无所有者的异步任务。
- 读取文件或进行不可重复的 I/O。

副作用必须属于一个可释放的作用域。节点卸载、条件分支消失、列表项移除或热重载时，该作用域内的订阅、取消令牌、GPU 资源和异步任务都能确定性清理。

## 3. 使用者最终看到的编程模型

### 3.1 页面与主题

页面通过泛型参数声明主题类型，主题沿组件树和所有 builder 自动传播：

~~~csharp
public sealed class MyTheme : SysTheme
{
    public override void Configure(ThemeBuilder theme)
    {
        theme.Color.Primary.Set(Color.FromRgb(61, 92, 255));
        theme.Color.Page.Set(Color.FromRgb(247, 248, 252));
        theme.Color.Text.Set(Color.FromRgb(28, 31, 38));
        theme.Space.Large.Set(24);
        theme.Radius.Card.Set(14);
    }
}

[Route("/")]
public sealed partial class HomePage : Page<MyTheme>
{
    [State]
    private partial int Count { get; set; }

    protected override void Build(PageBuilder<MyTheme> page)
    {
        page.Title = "ZUI";

        DIV(panel =>
        {
            panel.BackgroundColor.Page();
            panel.Color.Text();
            panel.Padding.Large();
            panel.Gap.Medium();
            panel.Display.Column();
            panel.MinHeight.Viewport();

            TEXT("纯 C# WebGPU UI", text =>
            {
                text.FontSize.Title();
                text.FontWeight.Bold();
            });

            BUTTON($"Count: {Count}", button =>
            {
                button.BackgroundColor.Primary();
                button.Color.OnPrimary();
                button.Padding.Block.Medium();
                button.Padding.Inline.Large();
                button.BorderRadius.Card();
                button.OnClick += _ => Count++;
            });
        });
    }
}
~~~

这里故意不把 theme 作为第二个 lambda 参数传入。page、panel、button 等强类型 builder 已经知道 TTheme，因此属性载体直接提供 Page、Primary、Text、Large 等主题令牌方法。

### 3.2 为什么节点函数使用大写

推荐公共节点函数使用 DIV、BUTTON、INPUT、TEXT、IMAGE、SCROLL 等大写名称，理由是：

- 在普通 C# 业务逻辑中一眼识别 UI 节点。
- 与类型名、局部函数和普通方法形成视觉边界。
- 与用户希望的盒子结构阅读方式一致。
- 不依赖特殊模板语法或编辑器插件即可辨识。

大写是 API 约定，不是编译器魔法。自定义组合函数可以遵循同样的命名约定。

### 3.3 单一 DSL 作用域

一个节点 lambda 中允许同时出现：

- 元素本身属性。
- 布局和视觉属性。
- 交互事件。
- 语义和无障碍信息。
- 子节点。

~~~csharp
DIV(card =>
{
    card.Width.Fill();
    card.MaxWidth.Px(720);
    card.Padding.Large();
    card.BackgroundColor.Surface();
    card.BorderColor.Subtle();
    card.BorderWidth.Px(1);
    card.BorderRadius.Card();
    card.Semantics.Role = SemanticRole.Region;
    card.Semantics.Label = "用户资料";

    TEXT(user.DisplayName);

    BUTTON("保存", save =>
    {
        save.Disabled = !CanSave;
        save.OnClick += SaveAsync;
    });
});
~~~

这不是结构 DSL 和样式 DSL 两条链，而是一个 DivBuilder 作用域中的不同强类型能力。

### 3.4 属性载体和主题令牌

推荐属性使用专用载体，而不是一条无限长 fluent 链：

~~~csharp
page.BackgroundColor.Primary();
page.Color.Text();
page.Width.Px(320);
page.Height.Fill();
page.Padding.Inline.Large();
page.Padding.Block.Px(12);
page.BorderRadius.Set(themeValue);
~~~

每个 setter 返回 void。这样做有几个好处：

- 不会出现 page.BackgroundColor.Primary().Padding.Large() 这类跨属性游走。
- IntelliSense 的下一步始终围绕当前属性，而不是整个元素。
- 主题令牌、数值单位和任意值的边界明确。
- builder 不需要为 fluent 链使用复杂的自引用泛型。

推荐的属性载体分组包括：

| 类别 | 示例 |
|---|---|
| 颜色 | BackgroundColor、Color、BorderColor |
| 尺寸 | Width、Height、MinWidth、MaxHeight |
| 间距 | Margin、Padding、Gap 及 Inline、Block、Top 等轴向子载体 |
| 布局 | Display、Align、Justify、Position、Overflow |
| 文字 | FontFamily、FontSize、FontWeight、LineHeight、TextAlign |
| 绘制 | Opacity、Transform、Shadow、Clip、Filter |
| 语义 | Semantics.Role、Label、Description、Value |

### 3.5 Builder 继承层次

建议以能力继承表达元素之间的共性：

~~~text
ElementBuilder<TTheme>
  ├─ BoxBuilder<TTheme>
  │    ├─ ContainerBuilder<TTheme>
  │    │    ├─ DivBuilder<TTheme>
  │    │    ├─ ScrollBuilder<TTheme>
  │    │    └─ GridBuilder<TTheme>
  │    └─ InteractiveBuilder<TTheme>
  │         ├─ ButtonBuilder<TTheme>
  │         └─ InputBuilder<TTheme>
  ├─ TextBuilder<TTheme>
  └─ ImageBuilder<TTheme>
~~~

继承只表示真实共享能力。不能为了形式统一，让 TEXT 暴露不合理的容器子节点 API，或让 IMAGE 假装拥有文字输入状态。

因为 setter 不需要跨属性链式返回元素本身，首版无需使用 CRTP。泛型主要承担主题传播、事件类型、值类型、组件参数和生成器约束。

### 3.6 普通 C# 控制流就是组合语法

~~~csharp
DIV(list =>
{
    list.Display.Column();
    list.Gap.Small();

    if (Loading)
    {
        PROGRESS();
    }
    else if (Users.Count is 0)
    {
        EMPTY_STATE("暂无用户");
    }
    else
    {
        foreach (var user in Users)
        {
            USER_ROW(user, key: user.Id);
        }
    }
});
~~~

生成器和运行时需要识别稳定 key，以保证条件和列表变化时尽可能复用节点、组件状态、字形缓存和 GPU 资源。

### 3.7 组合函数和有状态组件

无独立状态的视觉组合优先写成扩展方法：

~~~csharp
public static class UserUiExtensions
{
    extension<TTheme>(IChildrenBuilder<TTheme> ui)
        where TTheme : SysTheme
    {
        public void USER_BADGE(UserSummary user)
        {
            ui.DIV(badge =>
            {
                badge.Display.Row();
                badge.Align.Center();
                badge.Gap.Small();

                ui.AVATAR(user.Avatar);
                ui.TEXT(user.DisplayName);
            });
        }
    }
}
~~~

这里可利用 C# 14 扩展块，让自定义 UI 组合在 IntelliSense 中表现为子节点能力。

当一个部分拥有以下任意一种需求时，提升为 Component：

- 独立状态。
- 生命周期。
- 异步资源。
- 可单独热重载的边界。
- 可复用且需要稳定身份。

~~~csharp
public sealed partial class UserEditor<TTheme>(UserId userId)
    : Component<TTheme>
    where TTheme : SysTheme
{
    [State]
    private partial EditorState State { get; set; }

    protected override void Build(ComponentBuilder<TTheme> ui)
    {
        // UI 描述
    }
}
~~~

继承 Component 本身就足以被生成器识别。UiComponent 注解仅用于别名、导出名称或显式工具配置，不能成为每个组件必写的样板。

## 4. 注解策略

### 4.1 首阶段允许的注解

| 注解 | 用途 |
|---|---|
| UiTheme | 标记主题根并生成令牌载体 |
| UiComponent | 可选；定制组件别名、导出信息或工具行为 |
| Route | 生成强类型客户端路由表 |
| State | 生成状态槽、版本和失效通知 |
| ServerFunctions | 标记一组远程可调用服务端函数 |
| ServerFunction | 标记具体远程函数 |
| HotReloadBoundary | 定制热重载重建边界 |
| Authorize | 复用标准授权语义并生成服务端检查元数据 |

### 4.2 状态属性

C# partial property 非常适合减少状态样板：

~~~csharp
[State]
private partial int Count { get; set; }
~~~

生成器为它生成：

- 稳定的状态槽 ID。
- 强类型读写逻辑。
- 读取时的依赖登记。
- 写入时的相等性判断、版本增加和失效传播。
- 调试名称和来源位置。
- 热重载时可迁移的状态元数据。

不应依赖运行时反射发现 State。生成代码必须适配裁剪和 NativeAOT。

### 4.3 明确不增加的注解

首版不提供以下注解：

- Inject、Autowired、Service：使用构造函数注入或主构造函数。
- Style、Css：样式就在元素 builder 中，或由强类型组合函数复用。
- Computed、Memo、Effect：先提供明确的运行时 API，待真实使用证明注解有价值。
- Bindable：绑定应由强类型 InputValue、Selection 等属性和生成器处理。

原则是：注解只简化编译器确实可以稳定生成的机械工作，不隐藏控制流，不制造隐式全局容器。

## 5. 编译期工具 Zui.Tooling

### 5.1 单一工具项目

第一阶段使用一个 Zui.Tooling 项目承载增量源生成器和分析器，作为 Analyzer 引用进入业务项目，不作为运行时依赖。

内部可以分为这些逻辑模块：

- Components：组件识别、参数和节点调用元数据。
- State：partial 状态属性实现、状态槽和版本代码。
- Theme：主题令牌目录和属性载体扩展。
- Dsl：节点函数、key、事件和 builder 使用诊断。
- Routes：路由模式、参数解析和导航表。
- ServerFunctions：代理、分派器、契约哈希和序列化上下文。
- HotReload：边界、状态迁移和缓存失效表。
- Accessibility：交互节点语义、label、焦点和键盘可达性诊断。

先放在一个项目中可以降低 Roslyn 版本、打包和测试复杂度；只有当构建时间和发布边界证明需要时再拆分。

### 5.2 关键诊断

建议首版至少提供：

| 诊断 | 级别 |
|---|---|
| 在 Build 中写 State | Error |
| 在 Build 中直接调用 Server Function | Error |
| 交互元素无可访问名称 | Warning，可提升为 Error |
| foreach 的有状态子树无稳定 key | Warning |
| 主题令牌类型与属性类型不兼容 | Compile error |
| Server Function 使用不可序列化类型 | Error |
| Server Function 返回 IQueryable、Stream 等不稳定边界类型 | Error |
| 客户端项目引用 Server 实现程序集 | Error |
| NativeAOT 不安全的动态反射路径 | Warning 或 Error |
| 异步任务没有 Scope 或取消所有者 | Warning |

### 5.3 生成物可观察性

每一类生成代码应：

- 使用稳定文件名。
- 通过 GeneratedCode 标识。
- 携带源位置映射。
- 可在 Rider 中导航。
- 在诊断模式下输出人类可读的契约清单。
- 不把安全敏感实现复制到浏览器产物。

## 6. 响应式系统

### 6.1 从 Svelte 吸取的核心启发

应吸收的是响应式运行时原则，而不是 Svelte 模板语法：

- 读取时动态收集依赖。
- 写入只标脏真正依赖它的反应。
- 派生值懒计算。
- 派生结果未变化时阻断下游传播。
- 条件分支变化时复用依赖前缀并清除旧分支依赖。
- 副作用属于树状 scope，卸载时递归清理。
- 同一事务内的多次写入批处理。

### 6.2 状态节点

建议运行时提供以下内部或公共抽象：

| 节点 | 作用 |
|---|---|
| StateCell<T> | 组件局部可变状态 |
| StoreCell<T> | 可跨组件共享、显式提供所有者的状态 |
| ComputedCell<T> | 懒计算派生值 |
| ResourceCell<T> | 带取消、代次和加载状态的异步资源 |
| MotionCell<T> | 高频动画值，允许直接更新渲染节点 |

每个节点至少记录：

- 当前值。
- 写版本。
- 读取版本或确认版本。
- 订阅的 Reaction 列表。
- 等值比较器。
- 调试标签。

### 6.3 Reaction 类型

| Reaction | 作用 |
|---|---|
| BuildReaction | 重建组件的声明式节点描述 |
| BindingReaction | 更新一个已知属性或 RenderNode 字段 |
| ComputedReaction | 重新验证派生值 |
| EffectReaction | 框架内部和受控公共副作用 |

BuildReaction 是较重的路径。光标闪烁、滚动、动画、输入框选择区等高频值应尽量走 BindingReaction 或 MotionCell，直接更新已存在的 RenderNode，不触发整个组件 Build 和 reconcile。

### 6.4 脏状态传播

采用三级状态：

- CLEAN：依赖均已确认，结果可直接使用。
- MAYBE_DIRTY：某个上游派生值可能变化，需要按需确认。
- DIRTY：源状态或已确认的派生结果发生变化，必须重新执行。

StateCell 写入时：

1. 比较新旧值。
2. 未变化则退出。
3. 更新值和版本。
4. 将直接依赖标为 DIRTY。
5. 下游经过 ComputedCell 时先标为 MAYBE_DIRTY。
6. 调度到当前批次或下一帧。

ComputedCell 被读取或提交前验证时：

1. 先确认所有上游。
2. 只在必要时执行计算。
3. 比较派生结果。
4. 结果相等则把下游恢复为 CLEAN。
5. 结果变化才继续传播 DIRTY。

### 6.5 动态依赖

以下代码在 Admin 为 false 时不应继续订阅 AdminOnlyData：

~~~csharp
var title = IsAdmin ? AdminOnlyData.Title : PublicTitle;
~~~

Reaction 每次运行时维护新的依赖序列。运行时复用与上次相同的前缀；从第一个不同依赖开始解除旧订阅并记录新订阅。这样既支持普通 C# 条件控制流，也避免每次重建全部订阅集合。

### 6.6 批处理和帧提交

一次事件回调或 Server Function 完成回调构成默认事务边界：

~~~text
事件开始
  多次 State 写入
  标记 Reaction
事件结束
  计算必要的 Computed
  执行 Build 和 Binding
  布局脏节点
  编译场景差异
  在下一次可用帧提交 GPU
~~~

运行时提供：

- Batch(Action)：显式批处理。
- FlushSync()：测试或极少数命令式场景中同步刷新 CPU 状态。
- SettledAsync()：等待当前响应式、异步资源和渲染提交达到稳定点。

默认 UI 更新与显示器帧对齐。没有状态变化、动画或待提交资源时，不持续请求帧。

### 6.7 异步资源

ResourceCell 应携带 generation 和 CancellationToken。参数变化时：

1. 取消旧请求。
2. 增加 generation。
3. 启动新请求。
4. 旧请求即使晚返回，也因 generation 不匹配而被丢弃。
5. scope 卸载时取消当前请求。

这避免路由切换、快速搜索和热重载后旧结果覆盖新状态。

### 6.8 Scope 所有权

推荐所有权关系：

~~~text
ApplicationScope
  └─ RouteScope
      └─ ComponentScope
          ├─ StateCell
          ├─ ResourceCell
          ├─ Reaction
          ├─ RenderNode
          ├─ EventRegistration
          └─ OwnedGpuResource
~~~

条件子树和列表项也可以拥有更细粒度的 scope。移除节点时先停止调度，再取消异步工作，解除依赖和事件，最后延迟退休仍可能被 GPU 使用的资源。

### 6.9 转场生命周期

为出场动画保留内部状态：

- Active：正常参与响应式和布局。
- Paused：暂时不运行非必要 Reaction。
- Exiting：逻辑上已移除，但场景节点保留到出场动画结束。
- Disposed：彻底解除所有资源。

首版可以只实现基本淡入淡出，但生命周期模型要预留，避免以后为动画破坏 scope 清理语义。

## 7. 节点重建与差异协调

### 7.1 三棵相关结构

需要区分：

1. 描述节点：Build 产生的轻量声明式结果。
2. 实例节点：保存组件、事件、焦点和平台资源身份。
3. RenderNode：布局和场景编译使用的紧凑渲染节点。

Build 不直接创建 GPU 资源。它产生或写入可比较的描述，reconciler 决定复用、更新、插入或移除实例，再把变化投射到 RenderNode。

### 7.2 身份规则

节点身份按以下优先级确定：

1. 显式 key。
2. 生成器提供的稳定调用点 ID。
3. 同一父节点下的类型和顺序。

对动态列表，有状态节点必须使用 key。开发版本记录身份变化和异常重建原因。

### 7.3 属性级脏标记

不同属性变化产生不同脏标记：

| 属性变化 | 脏标记 |
|---|---|
| 颜色、透明度 | Paint |
| 宽高、间距、字体大小 | Measure、Arrange、Paint |
| 位置变换 | Transform、HitTest、Paint |
| 文本内容 | TextShape、Measure、Paint、Semantics |
| 语义标签 | Semantics |
| 点击事件委托 | EventBinding |
| 子节点集合 | Children、Measure、Arrange、Paint |

布局和场景编译只遍历受影响子树。高频 MotionCell 可以在满足约束时只设置 Transform 或 Paint。

## 8. 布局系统

### 8.1 自定义布局而非完整 CSS

ZUI 首版应实现一个受控的 C# 布局系统，不追求完整 CSS 兼容。理由是完整 CSS 规范、浏览器排版历史行为和 WebGPU 场景树之间的适配成本过高，也会削弱强类型属性模型。

布局使用 Measure 和 Arrange 两阶段：

- Measure：父节点给出约束，子节点返回期望尺寸。
- Arrange：父节点确定子节点最终位置和尺寸。

### 8.2 首版布局能力

必须支持：

- Block 和基础 inline text。
- Row、Column、Stack。
- 基本 Flex。
- 基本 Grid。
- Absolute 定位。
- Scroll 容器。
- min、max、fixed、auto、fill。
- margin、padding、gap。
- 主轴和交叉轴对齐。
- 百分比、视口单位和 aspect ratio。
- 文本测量和换行。
- clip 和 overflow。

暂缓：

- 完整 CSS Grid 自动放置算法。
- float、shape-outside 等网页历史布局。
- 多栏排版。
- 通用分页排版。
- 任意 CSS cascade 和 selector。

### 8.3 约束与单位

建议单位类型强类型化：

~~~csharp
public readonly record struct Length
{
    public static Length Px(float value);
    public static Length Percent(float value);
    public static Length ViewportWidth(float value);
    public static Length ViewportHeight(float value);
    public static Length Auto { get; }
    public static Length Fill { get; }
}
~~~

公共 DSL 仍以 Width.Px(320)、Width.Fill() 等更自然的载体方法为主。Length 作为存储和高级 API 类型。

### 8.4 滚动

滚动必须从一开始作为布局、输入和场景的共同能力设计：

- 内容尺寸和视口尺寸分离。
- 滚动偏移是高频 MotionCell。
- 滚动只更新变换和可见区域，不重建整个子树。
- 浏览器接收 wheel、touch、pointer；Windows 接收 Pointer 和精确触控板数据。
- 后续可加入虚拟列表和 overscan。

## 9. WebGPU 渲染架构

### 9.1 CPU 与 GPU 的职责边界

推荐边界：

| CPU | GPU |
|---|---|
| 响应式依赖和组件 Build | 图元栅格化 |
| 节点协调 | 批量实例绘制 |
| 文本整形和换行 | 图层合成 |
| Measure 和 Arrange | 裁剪、透明度和变换 |
| 命中测试 | 阴影、模糊等可并行视觉效果 |
| 无障碍树 | 动画参数插值的部分路径 |
| 场景编译和批次构建 | 最终表面呈现 |

首版不要把组件 diff、通用布局或文本整形放到 compute shader。它们拥有复杂分支、动态内存和平台语义，GPU 化会显著增加调试难度，却不一定改善实际页面性能。

### 9.2 场景流水线

~~~text
Reactive State
  ↓
Component Build
  ↓
Description Reconcile
  ↓
RenderNode Tree
  ↓
Layout
  ↓
Display List
  ↓
Scene Compiler
  ↓
Draw Lists + Resource Updates
  ↓
Frame Packet
  ↓
IWebGpuApi
  ├─ Browser WebGPU adapter
  └─ Native WebGPU adapter
~~~

RenderNode 不携带面向开发者的复杂 builder 对象。它应是紧凑、可增量更新、便于遍历的数据结构。

### 9.3 首版绘制图元

首版最低集合：

- 纯色矩形。
- 圆角矩形。
- 带边框圆角矩形。
- 线性和径向渐变。
- 图片。
- 字形和文本 run。
- 矩形与圆角 clip。
- opacity。
- 2D transform。
- 基础 box shadow。
- 基础 blur。

圆角盒子优先使用解析式或 signed-distance 片元计算，并通过实例缓冲传递位置、尺寸、圆角、边框和颜色。避免为每个普通 DIV 建立独立网格和 pipeline。

### 9.4 绘制顺序和批处理

UI 默认遵循 painter order。批处理不能破坏可见顺序。推荐：

- 在不跨越可能相交的透明图元时合并批次。
- 相同 pipeline、bind group 布局、纹理页和 clip 状态尽量合并。
- 不透明、可证明不相交的图元允许安全重排。
- z-index 和独立图层建立显式 stacking context。
- 诊断工具显示批次断点原因。

### 9.5 GPU 资源生命周期

长期存在：

- RenderPipeline。
- BindGroupLayout。
- Sampler。
- 字形和图片纹理图集。
- 常用静态几何。

逐帧循环：

- 2 到 3 帧 ring buffer。
- instance 数据。
- uniform 和动态 clip 数据。
- dirty range 上传。

销毁必须延迟到 GPU 不再引用对应帧。场景节点卸载后，CPU 身份可以立即失效，但底层 buffer 区域、texture view 等进入退休队列。

### 9.6 Device Lost

浏览器切换 GPU、驱动复位或休眠恢复时可能丢失 device。运行时必须：

1. 停止提交新帧。
2. 保留 CPU 场景和资源描述。
3. 重新请求 adapter 和 device。
4. 重建 pipeline、buffer、纹理图集和表面。
5. 从 CPU 资源缓存重新上传。
6. 完整重绘。
7. 若重建失败，展示平台级错误层，而不是空白画布。

### 9.7 空闲和背压

没有动画、状态更新、资源上传或窗口暴露时不请求新帧。若 CPU 生成帧速度超过 GPU：

- 合并尚未提交的视觉更新。
- 保留最新状态，不排队所有中间帧。
- 输入和无障碍更新不能被无限延迟。
- 性能面板展示 CPU Build、Layout、Scene Compile、Upload、GPU 和 Present 耗时。

### 9.8 Compute Shader 的引入顺序

只有经过基准证明后再依次考虑：

1. 大半径模糊。
2. 路径 tessellation 或 coverage。
3. 超大场景的可见性剔除。
4. 粒子和特殊视觉效果。

首版不使用 compute shader 做通用 Flex/Grid、组件 reconcile 或 Server Function 数据处理。

## 10. 浏览器 WebGPU 后端

### 10.1 启动过程

浏览器页面是一个固定壳：

~~~html
<!doctype html>
<html>
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width,initial-scale=1">
  </head>
  <body>
    <canvas id="zui-surface"></canvas>
    <div id="zui-semantics"></div>
    <textarea id="zui-ime"></textarea>
    <script type="module" src="/zui/browser-host.js"></script>
  </body>
</html>
~~~

这里的 HTML 只是框架固定宿主，不是业务 UI 模板。业务开发者不书写 HTML 模板字符串。

browser-host.js 是框架随 SDK 版本生成的极薄启动适配器，负责导入该 SDK 对应的 .NET WebAssembly 启动模块和平台桥。蓝图不把某一版 SDK 的具体 boot 文件名固化为公共 ABI。

启动顺序：

1. 加载 .NET WebAssembly 运行时和 ZAdmin.App。
2. 初始化浏览器平台服务。
3. 请求 WebGPU adapter 和 device。
4. 配置 canvas context。
5. 建立字体、输入法和语义桥。
6. 从当前 location 匹配生成的路由表。
7. 创建页面和响应式 scope。
8. Build、布局、场景编译并提交首帧。
9. 移除启动占位层。

### 10.2 JS 边界

浏览器必须经过 JavaScript 访问 WebGPU 和部分 Web API，但业务层仍保持纯 C#。关键性能原则是粗粒度调用：

不推荐：

~~~text
C# 每个节点调用一次 JS
C# 每个属性调用一次 JS
C# 每个 draw 调用一次 JS
~~~

推荐：

~~~text
C# 构造紧凑 Frame Packet
  ↓ 一次或少量跨边界调用
JS 解码器更新 buffer、texture 和 command encoder
  ↓
提交 GPU queue
~~~

Frame Packet 可以使用固定头、操作码、对齐结构和 Blob 区域。协议必须版本化，并拥有独立编码器、解码器和回放测试。

在 .NET 与浏览器 WebGPU 绑定成熟度足够时，可以减少 JS 适配层；但公共上层只依赖 IWebGpuApi，不绑定某一种互操作方式。

### 10.3 客户端路由

浏览器可见 UI 始终采用客户端渲染：

- 首次请求由服务端返回固定 index 壳和静态资源。
- 客户端读取 location，匹配生成的 RouteTable。
- 导航使用 History API。
- popstate 驱动路由 scope 切换。
- 直接访问任意客户端路由时由服务端 fallback 到 index。

没有 UI SSR，也没有 DOM hydration。服务端可以把公开、固定、无用户敏感信息的 bootstrap 元数据放入启动响应，但不能生成业务 UI 树。

### 10.4 浏览器兼容和降级

启动时检测：

- WebGPU 是否存在。
- 请求到的 adapter feature 和 limit。
- 所需纹理格式。
- 字体栅格化桥是否可用。

首版不建议实现 Canvas 2D 完整渲染后备，这会使测试面翻倍。WebGPU 不可用时显示明确的兼容性页面，列出浏览器版本、操作系统和诊断信息。后续可评估只读或紧急降级模式。

## 11. Windows 11 原生后端

### 11.1 宿主形态

ZAdmin.Windows 是原生窗口宿主，不使用 WebView 作为主要 UI 表面。它负责：

- Win32 窗口和消息循环。
- DPI、缩放和多显示器。
- WebGPU native surface。
- DirectWrite 文本服务。
- TSF 输入法。
- UI Automation。
- 系统剪贴板、拖放和文件对话框。

上层页面、组件、响应式、布局、场景编译和主题与浏览器共享。

### 11.2 WebGPU Native 实现

IWebGpuApi 的原生适配器可以在验证阶段比较 Dawn 和 wgpu-native，最终选择依据：

- Windows Direct3D 12 后端稳定性。
- 与 WebGPU 规范同步程度。
- 原生库发布体积。
- NativeAOT P/Invoke 友好度。
- 调试层、GPU 捕获和错误信息。
- 许可证和升级节奏。

这项选择在 P1 原型后形成 ADR，不在蓝图阶段锁死具体实现。

### 11.3 NativeAOT 边界

Windows Release 目标使用 NativeAOT，因此从第一天遵守：

- 不依赖运行时扫描程序集注册组件或服务。
- JSON 使用 System.Text.Json 源生成上下文。
- P/Invoke 使用 LibraryImport 等可生成形式。
- 服务注册尽量显式或源生成。
- 动态代码和表达式编译需要替代路径。
- 反射仅用于调试工具且有裁剪注解，不能进入核心启动路径。

Debug 目标使用 CoreCLR JIT，以获得 Edit and Continue、热重载和更完整调试能力。

## 12. 文本、系统字体和中文

### 12.1 目标

默认使用操作系统或浏览器已有字体显示中文和其他文字，而不是在应用中自带一整套中日韩字体。公共层只依赖：

~~~csharp
public interface ITextEngine
{
    TextMetrics Measure(in TextRunRequest request);
    ShapedText Shape(in TextRunRequest request);
    GlyphRasterBatch Rasterize(in GlyphRasterRequest request);
    FontMatchResult Match(in FontRequest request);
}
~~~

实际接口会在原型中细化，但高层必须隐藏平台的字体文件、DirectWrite 对象和 Canvas 对象。

### 12.2 浏览器默认路径

浏览器安全模型通常不允许应用任意读取系统字体原始文件，但显示系统字体并不需要读取字体文件。

推荐路径：

1. 使用 CSS 字体族描述和系统通用族进行匹配。
2. 通过 Canvas 2D 或 OffscreenCanvas 的 measureText 获取平台排版测量。
3. 将一个文本 run 栅格化到可复用画布。
4. 使用 copyExternalImageToTexture 上传到 WebGPU 纹理。
5. 在 WebGPU 场景中绘制该 run。
6. 以文本、字体、字号、方向、语言、缩放和样式作为缓存键。

候选 CSS 字体栈由平台选择，例如 system-ui、sans-serif 及本地中文回退。不能假设每台系统都拥有微软雅黑，也不能用单一字体名代替浏览器的 fallback。

这个路径的优点：

- 不需要字体权限。
- 使用浏览器成熟的 shaping 和系统 fallback。
- 中文、阿拉伯文、emoji 和复杂脚本更早可用。
- 不必在 Wasm 中立即移植完整 HarfBuzz、FreeType 和系统字体发现。

代价：

- 每种文本 run 的栅格结果是位图，不是统一的字形级矢量管线。
- 极端缩放可能需要重新栅格化。
- 浏览器和 Windows 的字形位置不能保证像素级相同。

首版接受这些差异，以优先保证正确文字和开发速度。

### 12.3 浏览器高级字体访问

Local Font Access API 只作为用户显式授权的高级能力，用于：

- 字体设计或排版工具。
- 用户选择本地字体文件的场景。
- 需要精确字体元数据的应用。

它不能成为普通 ZAdmin 页面显示中文的前提。权限被拒绝时必须回到标准 CSS/Canvas 字体路径。

### 12.4 Windows DirectWrite 路径

Windows 使用 DirectWrite：

1. 查询系统字体 collection 或 font set。
2. 按 family、weight、style 和 locale 匹配。
3. 使用系统 fallback 分割 run。
4. 进行 GetGlyphs、GetGlyphPlacements 等 shaping。
5. 使用 GlyphRunAnalysis 或等价 API 生成 alpha coverage。
6. 上传到 WebGPU 字形图集。
7. 场景编译器输出字形实例。

这样无需自行读取字体目录，也能使用系统字体、中文回退、emoji 和本地化规则。

### 12.5 字体缺失策略

提供明确策略：

~~~csharp
public enum MissingFontPolicy
{
    SystemOnly,
    SystemThenRemote,
    SystemThenPackaged
}
~~~

默认 SystemOnly。可选策略允许应用声明受许可的远程或打包字体。若系统确实没有覆盖某字符，且应用没有配置远程或打包回退，显示 tofu 方框是客观不可避免的结果，框架应提供诊断而不是静默替换。

### 12.6 缓存

文本缓存至少分两级：

- shaping cache：字体匹配和 glyph positioning。
- raster cache：字形或文本 run 的纹理区域。

缓存键包含 DPI 或 device pixel ratio。缩放变化时保留短期旧缓存以平滑迁移，新缓存准备好后原子替换。

### 12.7 文本一致性验收

不要求浏览器与 Windows 截图像素相同。验收分为：

- 语义一致：字符、换行意图、选择区、方向正确。
- 几何容差：布局边界在定义容差内。
- 平台视觉基准：浏览器和 Windows 分别保存 golden。
- 复杂脚本用专门文本用例验证，不只验证英文。

必须包含简体中文、繁体中文、日文、韩文、阿拉伯文、emoji、拉丁组合字符和中英文混排。

## 13. 输入、焦点和无障碍

### 13.1 浏览器输入

WebGPU 画布自身不能提供完整输入法和无障碍语义，因此采用混合架构：

- 可见 UI：WebGPU canvas。
- 文本输入：屏幕外或透明定位的 textarea。
- 无障碍：最小语义 DOM 树。
- 指针和滚轮：canvas 事件桥。

聚焦文本框时：

1. 将隐藏 textarea 的值、选择区和输入模式同步到当前 TextInputNode。
2. 合理定位 textarea，以帮助移动设备候选框。
3. 处理 compositionstart、compositionupdate、compositionend。
4. 将编辑操作写回 C# 文本模型。
5. 由响应式和 RenderNode 更新 WebGPU 可见文本、光标和选择区。

### 13.2 Windows 输入

Windows 平台使用：

- Pointer、Keyboard 和窗口消息统一成 ZUI 输入事件。
- TSF 提供 IME composition 和候选窗口。
- DirectWrite 提供命中位置与字符几何。
- UI Automation 提供控件语义和操作模式。

### 13.3 焦点系统

焦点管理在共享层实现：

- 焦点树。
- Tab 顺序。
- FocusScope。
- 方向导航。
- focus visible。
- 模态窗口焦点圈闭。
- 路由切换后的焦点恢复策略。

平台层只负责把系统焦点、键盘和辅助技术事件映射到共享焦点管理器。

### 13.4 语义树

SemanticsNode 与 RenderNode 相关但不相同。一个复杂视觉节点可能没有语义；多个绘制节点也可能合并成一个语义控件。

语义信息包括：

- role。
- name、description、value。
- enabled、selected、expanded、checked 等状态。
- 可用 action。
- 屏幕边界。
- 父子关系和阅读顺序。

浏览器将差异同步到语义 DOM；Windows 映射到 UI Automation provider。分析器应阻止常见的无 label 交互控件。

## 14. 全栈开发模型

### 14.1 目标体验

业务功能按 feature 放在一起：

~~~text
src/ZAdmin/Features/Users/
  Users.Models.cs
  Users.Page.cs
  Users.Server.cs
  Users.Validation.cs
  Users.Tests.cs
~~~

开发者不手写：

- Controller 和 MapPost 路由。
- URL 常量。
- HttpClient 调用。
- 重复客户端接口。
- OpenAPI 客户端。
- 手工 JSON 序列化配置。

但是浏览器到服务端的安全边界仍然是真实 HTTP，不能把远程调用伪装成本地零成本方法。API 名称和诊断界面必须让开发者知道这是 Server Function。

### 14.2 服务端函数示例

~~~csharp
public sealed record UserQuery(string? Search, int Page, int PageSize);
public sealed record UserSummary(UserId Id, string DisplayName, string Email);
public sealed record UserPageResult(IReadOnlyList<UserSummary> Items, int Total);

[ServerFunctions]
public sealed partial class UsersServer(AdminDb db, IUserAccess access)
{
    [ServerFunction]
    [Authorize(Policy = Policies.UsersRead)]
    public async ValueTask<UserPageResult> Query(
        UserQuery query,
        CancellationToken cancellationToken)
    {
        await access.EnsureCanReadAsync(cancellationToken);
        return await db.Users.QueryPageAsync(query, cancellationToken);
    }
}
~~~

页面调用：

~~~csharp
var result = await UsersServer.Query(
    new UserQuery(Search, Page, PageSize),
    cancellationToken);
~~~

### 14.3 编译后物理形态

同一份方法签名产生两个不同产物：

服务端程序集：

- 保留 UsersServer 的真实实现。
- 通过构造函数注入数据库和授权服务。
- 生成 dispatcher。
- 生成授权、验证、追踪和异常映射元数据。

浏览器和 Windows App 程序集：

- 不包含方法体、数据库类型或服务端依赖。
- 生成同名静态代理入口。
- 序列化参数并发送函数 ID。
- 反序列化强类型结果。
- 支持取消、超时、追踪和错误联合类型。

共享的 Models 文件进入 Shared 或 App 合约编译；Server 文件只编译到 Server。项目结构和生成器必须在编译期阻止客户端引用服务端实现。

#### 14.3.1 客户端如何在没有显式接口时获得签名

这里需要一个明确的跨项目编译步骤，不能假设普通 Source Generator 可以读取另一个项目的语义模型。

推荐流水线：

~~~text
ZAdmin.Shared build
  ↓
ZAdmin.Server target: ZuiExportContracts
  ├─ 对真实 Server compilation 运行增量契约导出
  ├─ 只读取标记为 ServerFunction 的公开签名和元数据
  └─ 写入 obj/zui-contracts/{hash}/
       manifest.zui.json
       client-proxies.g.cs
       serializer-roots.g.cs
  ↓
ZAdmin.App target: ZuiImportContracts
  ├─ 校验 hash、Shared 程序集身份和生成器版本
  ├─ 将 client-proxies.g.cs 作为 GeneratedCompile 输入
  └─ 将 manifest 作为运行时诊断资源
  ↓
ZAdmin.Browser / ZAdmin.Windows
~~~

Zui.Tooling NuGet 包除 Analyzer 外携带 buildTransitive MSBuild targets 和契约导出任务。设计时构建也执行增量导出，因此 Rider 能补全 UsersServer.Query 并导航到 Users.Server.cs；不要求开发者先手工构建整个解决方案。

安全和可重复性约束：

- 导出器基于 Server 的真实 Roslyn compilation 解析签名，因此能正确处理 using、别名、泛型、可空性和 attribute。
- 导出物不包含方法体、主构造函数中的数据库类型、私有成员、常量秘密或服务端程序集引用。
- 客户端只编译生成代理和共享合约类型。
- 修改实现方法体但不改签名时，contract hash 不变化。
- 签名或授权元数据变化时，contract hash 变化并触发 Browser、Windows 和 Server 的协调 generation。
- 生成目录位于 obj，不提交 Git；可用显式诊断命令导出人类可读副本。

#### 14.3.2 避免构建环

ZAdmin.Server 不建立到 ZAdmin.Browser 的 ProjectReference。构建由根级 target 或 eng 脚本按以下顺序协调：

~~~text
Shared
  → Server:ZuiExportContracts
  → App
  → Browser AppBundle
  → Server compile/publish + static asset manifest
~~~

Server 的普通编译不需要 Browser。只有最终 Web 发布 target 接收已完成的 AppBundle 并写入 Server 的 obj 中间目录。这样 App 可以依赖导出的契约，最终 Server 包又可以包含 AppBundle，但 MSBuild 项目引用图仍然无环。

### 14.4 函数身份和契约

每个函数生成稳定 FunctionId，来源可以是：

- 程序集契约命名空间。
- ServerFunctions 类型的稳定名称。
- 方法名。
- 参数和返回值的规范化签名。
- 显式版本。

不要使用仅由源码行号或随机 GUID 产生的 ID。

生成物包括：

- Client proxy。
- Server dispatcher。
- System.Text.Json source generation context。
- Function manifest。
- Contract hash。
- 授权和验证元数据。
- 开发诊断名称。

### 14.5 传输协议

浏览器默认 HTTP：

~~~text
POST /__zui/functions/{functionId}
Content-Type: application/zui+json
X-Zui-Contract: {contractHash}
Traceparent: ...
~~~

首版可使用 JSON 以获得可诊断性。Frame Packet 是二进制 ABI，但业务 Server Function 不必过早使用自定义二进制协议。后续对高频或流式函数再增加 MessagePack、NDJSON、SSE 或 WebSocket transport。

Windows 客户端默认调用同一 HTTP 边界。当 Windows 和 Server 真正在同一进程部署时，可由显式配置选择 generated in-process transport，但语义仍保留授权、取消、验证和追踪，不能因本地优化绕过安全检查。

### 14.6 错误模型

网络、授权、验证和业务错误要区分：

~~~csharp
public abstract record ServerError
{
    public sealed record Offline : ServerError;
    public sealed record Timeout : ServerError;
    public sealed record Unauthorized : ServerError;
    public sealed record Forbidden : ServerError;
    public sealed record Validation(
        IReadOnlyDictionary<string, string[]> Errors) : ServerError;
    public sealed record Conflict(string Code, string Message) : ServerError;
    public sealed record Unexpected(string TraceId) : ServerError;
}
~~~

具体公共 API 可以是 Result<T, ServerError>、抛出受控异常或两者分层，但不能把服务端堆栈和数据库信息发送给客户端。

### 14.7 安全约束

无显式接口不等于无安全边界：

- ServerFunction 默认不可匿名，除非显式 AllowAnonymous。
- 所有输入在服务端重新验证。
- 客户端生成的权限提示只用于体验，不能替代服务端授权。
- 防跨站请求、cookie、token 和 origin 策略由宿主统一配置。
- 函数 manifest 不包含密钥或实现细节。
- 上传下载使用专门的流式句柄协议，不能把任意文件路径作为普通参数。
- 数据库实体不直接作为默认合约类型。

### 14.8 可观察性

开发工具对每个 Server Function 展示：

- 逻辑函数名和 FunctionId。
- 调用位置。
- 请求大小和响应大小。
- 序列化、网络、服务端执行耗时。
- 当前 contract hash。
- 重试和取消原因。
- trace ID。

这样既保留本地方法般的书写体验，又不会隐藏远程调用成本。

## 15. ASP.NET Core 单体托管

### 15.1 生产发布形态

生产环境只发布并启动 ZAdmin.Server：

~~~text
publish/
  ZAdmin.Server.exe
  ZAdmin.Server.dll
  appsettings.json
  wwwroot/
    index.html
    _framework/
    assets/
    zui/
~~~

它同时承担：

- 静态浏览器 AppBundle。
- SPA fallback。
- Server Functions。
- 身份认证和授权。
- 健康检查。
- 静态资源压缩和缓存。
- 需要时的 HTTP/2、HTTP/3。
- 可选、受控的反向代理。

不需要另行部署 Node、Nginx 或独立前端容器。生产前仍可在真实基础设施前放置云负载均衡、CDN 或 Nginx，但那是部署选择，不是框架必需品。

### 15.2 ASP.NET Core 入口

目标形态：

~~~csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddZuiServerFunctions()
    .AddZAdminFeatures();

var app = builder.Build();

app.UseResponseCompression();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapZuiServerFunctions("/__zui/functions");
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();
~~~

具体中间件顺序在安全原型中验证。

### 15.3 缓存策略

| 资源 | 推荐缓存 |
|---|---|
| index.html | no-cache 或很短 max-age |
| 带内容指纹的 .wasm、.dll、JS、WGSL、图片 | public, max-age=31536000, immutable |
| service worker manifest | no-cache |
| 开发期所有资源 | no-store |
| 用户特定 bootstrap | private, no-store |

资源文件名内容寻址，index 引用当前 generation。发布切换时先上传完整新资源，再切换 index，防止新壳引用不存在的旧或新文件。

### 15.4 浏览器包进入 Server 的构建方式

不能在 Server publish 完成后再随意复制到源码 wwwroot，否则 Static Web Assets manifest、压缩资源和指纹可能不一致。

推荐构建图：

~~~text
ZAdmin.App
  ↓
ZAdmin.Browser build and publish
  ↓ 产出 immutable AppBundle
Copy to ZAdmin.Server intermediate directory
  ↓
Generate or merge static web asset manifest
  ↓
ZAdmin.Server publish
~~~

复制目标位于 Server 的 obj 中间目录，不修改受版本控制的源码 wwwroot。MSBuild target 声明 Inputs 和 Outputs，支持增量构建。

### 15.5 Nginx 类能力边界

内置宿主可覆盖大多数单应用需求：

- TLS 终止。
- HTTP/2 和可选 HTTP/3。
- 静态文件。
- 压缩。
- 缓存头。
- SPA fallback。
- 健康检查。
- 请求限制。
- 日志和追踪。

需要反向代理时可选集成 YARP，但只允许配置明确的 route 和 cluster 白名单。不把“任意代理到任意地址”作为应用默认能力，也不把 YARP 加入每个最小部署。

## 16. 开发宿主与热重载

### 16.1 为什么需要 ZAdmin.DevHost

浏览器、服务端、生成契约和 WGSL 可能以不同速度编译。如果只分别运行 dotnet watch，浏览器很容易在短时间内调用到旧契约服务端，或者刷新时拿到一半写入的静态资源。

ZAdmin.DevHost 提供稳定入口并协调构建代次：

~~~text
Browser
  ↓ fixed http://localhost:port
ZAdmin.DevHost
  ├─ /               → current Browser generation
  ├─ /__zui/functions → current Server worker
  ├─ /__zui/dev       → reload/status channel
  └─ diagnostics      → build and contract state
~~~

Server worker 可以在临时 loopback 端口启动；浏览器始终连接 DevHost 固定端口。

### 16.2 不可变 Generation

每次构建生成：

~~~text
.zui/dev/generations/{generationId}/
  browser/
  server/
  manifest.json
  contract.json
  shaders/
~~~

流程：

1. 在新 generation 目录完整构建。
2. 验证 Browser manifest、Server health 和 contract hash。
3. 启动新 Server worker。
4. 运行 smoke call。
5. 原子切换 current generation 指针。
6. 通知浏览器采用 H0、H1 或 H2 更新。
7. 旧 worker 排空进行中的请求。
8. 确认无人引用后清理旧 generation。

构建失败时保持旧 generation 服务，不让页面进入半更新状态。

### 16.3 热重载等级

| 等级 | 行为 | 典型变化 |
|---|---|---|
| H0 In-place | 原组件和状态保留，重新执行受影响 Build | 方法体、颜色、局部布局、事件体 |
| H1 Boundary remount | 重建组件边界，迁移兼容状态 | 字段、参数、局部节点形态变化 |
| H2 App hot restart | 保留可序列化会话状态，重建客户端 App | 路由表、主题根、契约或大量类型变化 |
| H3 DevHost restart | 重启开发宿主 | 端口、宿主中间件、DevHost 自身变化 |

开发工具必须告诉用户本次采用哪个等级以及为什么升级，不能只显示“热重载失败”。

### 16.4 .NET Metadata Update

运行时使用 MetadataUpdateHandler 接收应用更新：

- ClearCache：清除生成的组件工厂、委托和反射调试缓存。
- UpdateApplication：定位受影响类型和组件实例。
- 标记对应 HotReloadBoundary。
- H0 时重新运行 Build 和差异协调。
- H1 时导出兼容 State 槽、重建边界并导入状态。

状态迁移以稳定槽 ID、属性类型和可选迁移器为依据。类型不兼容时放弃该槽并输出明确诊断。

### 16.5 浏览器热重载

Debug：

- 使用 .NET WebAssembly 的调试运行时和 SDK 热重载支持。
- 不启用 Wasm AOT。
- DevHost 通过开发通道通知补丁、资源 revision 或整 App 重启。
- 修改只影响服务器方法体时，无需刷新客户端。
- 修改共享 Server Function 合约时，构建 Browser 和 Server 的协调 generation。

Release：

- 使用 Wasm AOT。
- 不包含开发通道、补丁元数据和调试端点。

### 16.6 Windows 热重载

Debug 使用 CoreCLR：

- 连接 Rider 或 dotnet watch。
- 应用 Metadata Update。
- 复用同一 ZUI HotReloadBoundary 机制。

Release 使用 NativeAOT，不承诺运行时热重载。

### 16.7 WGSL 热替换

Shader 文件独立监控：

1. 编译或由 WebGPU 创建新 ShaderModule。
2. 异步创建新 pipeline。
3. 验证 bind group layout 和 Frame Packet ABI。
4. 成功后在帧边界原子交换。
5. 旧 pipeline 延迟退休。
6. 失败时继续使用旧 pipeline，并把编译错误映射到源文件和行列。

### 16.8 图片和字体资源热更新

资源使用 revision：

- 新资源后台解码或栅格化。
- 上传新纹理。
- 完成后更新资源句柄所指 revision。
- 下一帧切换。
- 旧纹理延迟退休。

这样避免保存图片或字体后出现一帧空白。

### 16.9 热重载所有权

开发时只能有一个协调者：

- 默认 DevHost 模式。
- 或显式 IDE debugger 模式。

不能同时由多个 watch 进程争夺浏览器刷新和 Server worker。启动时检测重复协调者并报错。

### 16.10 Hot Reload Lab 验收门

在构建复杂控件前，建立专门样例，连续验证：

- 改文字和颜色：H0，状态保留。
- 改事件逻辑：H0。
- 增减普通节点：H0 或 H1。
- 新增 State 属性：H1，旧状态尽量保留。
- 修改主题令牌：所有使用节点更新。
- 修改路由：H2。
- 修改 Server 实现：只换 worker，页面不刷新。
- 修改 Server Function 签名：Browser 和 Server 协调切换。
- 修改 WGSL：pipeline 原子热替换。
- 制造编译错误：旧代次继续运行。
- 修复错误：自动切到新代次。
- 浏览器和 Windows 分别通过。

只有这个实验室稳定后，才批准进入大规模控件开发。

## 17. SvelteKit 客户端渲染对本方案的启发

### 17.1 可借鉴的机制

SvelteKit 能从服务端加载切换到客户端导航，核心不是“把所有服务端代码搬进浏览器”，而是清楚划分：

- 服务端负责应用壳、数据端点和部署适配。
- 客户端运行时根据当前 URL 创建页面。
- History API 导航后，只在客户端更新页面。
- 服务端为直接访问客户端路由提供 fallback 或首次响应。
- 服务端专属模块永远不进入客户端 bundle。

ZUI 采用同样的边界意识，但不采用 Svelte 模板、DOM 渲染或 hydration。

### 17.2 ZUI 的对应形态

~~~text
SvelteKit 概念              ZUI 对应
-----------------------------------------------------------
route manifest             generated RouteTable
client router              Zui.Navigation
SSR HTML                   不提供 UI SSR
hydration                  不需要
client bundle              .NET Wasm AppBundle
server-only module         Features/*.Server.cs
load or remote call        generated Server Function proxy
adapter-node/server        ZAdmin.Server publish
Vite dev server            ZAdmin.DevHost
HMR boundary               HotReloadBoundary
~~~

### 17.3 坚持客户端渲染的结果

优点：

- 浏览器和 Windows 共用同一个场景模型。
- 不需要定义 HTML 与 WebGPU 节点的 hydration 对应。
- 运行时和 DSL 更一致。

代价：

- 首帧需要下载和启动 .NET Wasm。
- SEO 和无脚本内容不是首要能力。
- 需要认真优化 AppBundle、懒加载和启动占位。

ZAdmin 属于管理和应用型 UI，这个取舍合理。若未来需要公开内容站，应单独使用适合 SSR 的站点层，而不是让核心 WebGPU UI 同时承担两种冲突模型。

## 18. 解决方案和目录蓝图

### 18.1 顶层结构

推荐最终结构：

~~~text
zadmin/
  global.json
  Directory.Build.props
  Directory.Build.targets
  Directory.Packages.props
  NuGet.config
  zadmin.slnx

  eng/
    build.ps1
    test.ps1
    publish-browser.ps1
    publish-windows.ps1
    verify-aot.ps1

  docs/
    architecture/
      zui-webgpu-fullstack-blueprint.md
      decisions/
    development/
    protocols/

  src/
    Zui/
      Zui.Core/
      Zui.Layout/
      Zui.Text/
      Zui.Text.Browser/
      Zui.Text.DirectWrite/
      Zui.Scene/
      Zui.Runtime/
      Zui.Controls/
      Zui.Rendering.WebGpu/
      Zui.Rendering.WebGpu.Native/
      Zui.Platform.Browser/
      Zui.Platform.Windows/
      Zui.FullStack/
      Zui.FullStack.Server/
      Zui.Tooling/
      Zui.Testing/

    ZAdmin/
      Features/
        Home/
        Users/
      ZAdmin.Shared/
      ZAdmin.App/
      ZAdmin.Browser/
      ZAdmin.Server/
      ZAdmin.Windows/
      ZAdmin.DevHost/

  samples/
    HelloZui/
    HotReloadLab/
    TextLab/
    LayoutLab/
    FullStackLab/

  tests/
    Zui.Core.Tests/
    Zui.Layout.Tests/
    Zui.Runtime.Tests/
    Zui.Scene.Tests/
    Zui.Tooling.Tests/
    Zui.FullStack.Tests/
    Zui.Browser.Tests/
    Zui.Windows.Tests/
    Zui.Performance.Tests/
~~~

### 18.2 Zui 项目职责

| 项目 | 职责 | 不允许依赖 |
|---|---|---|
| Zui.Core | 基础值类型、主题协议、诊断、scope 基元 | 平台、ASP.NET Core、WebGPU native |
| Zui.Layout | Measure、Arrange、约束、布局节点 | 浏览器、Win32 |
| Zui.Text | 文本请求、shaping 结果、ITextEngine | Canvas、DirectWrite 具体类型 |
| Zui.Text.Browser | Canvas/OffscreenCanvas 文本桥 | Windows |
| Zui.Text.DirectWrite | DirectWrite 匹配、整形和栅格 | 浏览器 |
| Zui.Scene | RenderNode、Display List、Frame Packet | 业务组件 |
| Zui.Runtime | 组件、状态、Reaction、协调、导航 | 具体 GPU 后端 |
| Zui.Controls | Button、Input、Scroll 等行为和默认视觉 | 应用业务 |
| Zui.Rendering.WebGpu | WebGPU 抽象、pipeline 和场景提交 | 具体窗口系统 |
| Zui.Rendering.WebGpu.Native | Native WebGPU FFI | 浏览器 |
| Zui.Platform.Browser | 浏览器启动、输入、语义、Web API 桥 | Server 实现 |
| Zui.Platform.Windows | Win32、TSF、UIA、窗口 | ASP.NET Core |
| Zui.FullStack | 客户端契约和 transport 抽象 | 数据库和服务实现 |
| Zui.FullStack.Server | 分派、中间件、ASP.NET Core 集成 | UI 平台后端 |
| Zui.Tooling | Roslyn 生成器和分析器 | 任何运行时项目 |
| Zui.Testing | 测试宿主、场景快照、确定性调度器 | 生产启动路径 |

### 18.3 ZAdmin 项目职责

| 项目 | 职责 |
|---|---|
| ZAdmin.Shared | 客户端和服务端真正共享的无特权合约值类型 |
| ZAdmin.App | 页面、主题、客户端状态、功能 UI |
| ZAdmin.Browser | Wasm 入口和浏览器平台装配 |
| ZAdmin.Server | Server Function 实现编译、ASP.NET Core 宿主和 AppBundle |
| ZAdmin.Windows | Windows 原生入口和平台装配 |
| ZAdmin.DevHost | 开发代次、代理、热重载协调和诊断 |

Features 目录按编译项规则进入不同项目：

- 星号.Models.cs：Shared。
- 星号.Page.cs：App。
- 星号.Server.cs：Server。
- 星号.Validation.cs：根据声明的边界进入 Shared 或 Server。

如果基于文件命名的编译规则影响 Rider 导航，可改为同一 feature 目录下用子目录 Shared、App、Server。原则是 feature 邻近性优先，但安全的程序集边界不能牺牲。

### 18.4 依赖方向

~~~text
ZAdmin.Browser ─┐
               ├─> ZAdmin.App ─> Zui.Controls ─> Zui.Runtime
ZAdmin.Windows ─┘                                 │
                                                 ├─> Zui.Layout
                                                 ├─> Zui.Text
                                                 └─> Zui.Scene

ZAdmin.Browser ─> Zui.Platform.Browser ─> Zui.Rendering.WebGpu
ZAdmin.Windows ─> Zui.Platform.Windows ─> Zui.Rendering.WebGpu.Native

ZAdmin.Server ─> Zui.FullStack.Server
ZAdmin.App    ─> Zui.FullStack

Zui.Tooling  -- Analyzer reference only --> App, Shared, Server
~~~

禁止反向依赖。尤其：

- Core 不依赖 Runtime。
- Runtime 不依赖 Browser 或 Windows。
- App 不依赖 Server。
- Shared 不依赖数据库、ASP.NET Core 或平台 UI。
- Tooling 不作为普通程序集进入发布产物。

### 18.5 不创建空泛项目

不预先创建 Common、Utils、Abstractions、Infrastructure 等没有清晰所有权的项目。新项目必须回答：

- 它隔离了哪个真实的平台或发布边界。
- 它减少了哪一类不应存在的依赖。
- 它是否拥有可独立测试的职责。

仅为了让目录看起来“分层完整”而拆项目，会增加构建时间、循环依赖和导航成本。

## 19. SDK、语言和构建配置

### 19.1 SDK 固定

批准实施后，在仓库根创建 global.json，固定当前机器实际安装的 .NET 11 Preview 7 SDK：

~~~json
{
  "sdk": {
    "version": "11.0.100-preview.7.26381.103",
    "rollForward": "latestPatch",
    "allowPrerelease": true
  }
}
~~~

该版本号必须在真正创建骨架时再次执行 dotnet --info 验证；如果本机 SDK 已变化，以实测可用的 Preview 7 精确版本为准。

### 19.2 公共编译设置

Directory.Build.props 目标：

~~~xml
<Project>
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
~~~

可发布库按适用性增加：

~~~xml
<IsAotCompatible>true</IsAotCompatible>
<IsTrimmable>true</IsTrimmable>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
~~~

具体浏览器工作负载属性以 .NET 11 Preview 7 SDK 实测为准，不能照抄旧版 Blazor 属性名。

### 19.3 当前根项目的迁移说明

当前仓库已有：

- Program.cs。
- zadmin.csproj。
- zadmin.sln。
- Rider 的 .idea。
- bin 和 obj。

本蓝图阶段不移动、删除或重写它们。批准后进入 P0 时再决定：

- 将根可执行项目迁为 samples/HelloZui，或将其替换为新的解决方案入口。
- 从 zadmin.sln 迁移到 slnx，还是暂时并存。
- 清理哪些生成目录和忽略规则。

当前 zadmin.csproj 中的 InvariantGlobalization=true 与中文、本地化、语言标签和文化相关格式目标冲突。批准实施后应移除，并通过 NativeAOT 和 Wasm 发布测试测量国际化数据的体积影响。此次文档提交不修改该项目文件。

### 19.4 依赖版本管理

使用 Directory.Packages.props 集中管理 NuGet 版本。原则：

- .NET Preview SDK 相关包与 SDK 对齐。
- Roslyn 包版本与目标编译器验证兼容。
- WebGPU native 绑定固定到可重复构建版本。
- 不默认采用浮动 latest。
- 升级需要同时通过 Browser、Windows、Tooling 和 AOT 验收。

## 20. 公共 API 设计准则

### 20.1 稳定层和实验层

命名空间分级：

- Zui：稳定的应用开发 API。
- Zui.Advanced：需要理解生命周期或性能语义的高级 API。
- Zui.Experimental：可能变化的能力。
- Zui.Internal：不承诺兼容。

首个版本发布前仍可调整，但样例和生成器必须只使用计划公开的 API，避免内部实现意外成为事实标准。

### 20.2 委托和分配

日常代码允许捕获 closure，以可读性为主：

~~~csharp
button.OnClick += _ => Count++;
~~~

框架内部和超大列表提供显式高性能入口，但不把三参数 lambda 暴露成默认节点形态。可选方案例如：

~~~csharp
BUTTON(
    item,
    static (button, value) =>
    {
        button.Text = value.Name;
    },
    key: item.Id);
~~~

这类 API 只有基准证明 closure 或重复委托产生实际压力后才公开。默认 DIV(page => { ... }) 保持最简。

### 20.3 泛型使用边界

泛型适合：

- TTheme 传播。
- Component 参数和强类型事件。
- StateCell、ComputedCell 和 ResourceCell 值类型。
- Server Function 请求和结果。
- 属性载体的令牌类型约束。

不适合：

- 为每个运行时节点建立深层泛型类型，导致代码膨胀。
- 用复杂泛型模拟所有可能的 DSL 语法。
- 把平台差异变成整个组件树的类型参数。

在 NativeAOT 和 Wasm AOT 下，过度值类型泛型实例化会扩大二进制。场景树内部应在类型安全入口后归一为紧凑表示。

### 20.4 继承与组合

- Builder 用浅继承表达真实能力集合。
- Page、Component 提供生命周期模板。
- 服务和平台能力通过接口组合。
- 控件的视觉部分优先组合，不建立几十层皮肤继承。
- sealed 是默认；只在有明确扩展协议时开放继承。

### 20.5 命名

- UI 节点：DIV、BUTTON、INPUT。
- 类型：DivBuilder、ButtonBuilder。
- 远程类型：UsersServer，保留 Server 后缀提醒调用边界。
- 响应式：StateCell、ComputedCell、Reaction。
- 渲染：RenderNode、DisplayList、FramePacket。
- 平台抽象：ITextEngine、IWebGpuApi、IInputPlatform。

## 21. 测试与验证矩阵

### 21.1 单元和属性测试

Zui.Core：

- Scope 递归释放顺序。
- 稳定 ID。
- 主题令牌类型和解析。
- 资源退休。

Zui.Runtime：

- 动态依赖切换。
- CLEAN、MAYBE_DIRTY、DIRTY 传播。
- Computed 等值阻断。
- 嵌套 Batch。
- 异步 generation 丢弃旧结果。
- keyed reconcile 和状态保留。
- Build 中写状态的运行时保护。

Zui.Layout：

- 约束传播。
- Row、Column、Flex、Grid。
- min、max、percent、viewport。
- 文本换行。
- Scroll 和 clip。
- 随机树属性测试，保证无 NaN、负无限尺寸和循环布局。

Zui.Scene：

- Display List 顺序。
- batch 合并不改变 painter order。
- Frame Packet 编解码 round trip。
- dirty range。
- device lost 后资源重建描述完整。

### 21.2 Tooling 测试

使用 Roslyn generator driver 和编译快照验证：

- State partial property 生成。
- Theme 载体生成。
- Route 冲突诊断。
- Server Function 合约和 hash 稳定。
- 客户端不包含服务端字段或实现。
- JSON 上下文完整。
- 每条分析器诊断的正反例。
- 修改单文件时增量生成器不重新处理全部 compilation。

### 21.3 浏览器测试

Playwright 驱动真实浏览器：

- WebGPU 初始化。
- 首帧。
- 路由和返回键。
- pointer、wheel、keyboard。
- 中文 IME 组合事件。
- 语义 DOM。
- Server Function 成功、验证、未授权、离线和取消。
- device lost 模拟或适配层故障注入。
- 热重载各等级。

WebGPU 浏览器环境在 CI 中可能需要特定启动参数或软件适配器。CI 结果应区分：

- CPU 逻辑测试。
- 软件 GPU 场景测试。
- 真实 Windows GPU 定期验收。

### 21.4 Windows 测试

- Win32 窗口创建和关闭。
- DPI 和多显示器缩放。
- DirectWrite 中文和 fallback。
- TSF 输入法。
- UI Automation 树和基本 action。
- 剪贴板。
- device lost 或窗口 surface 重建。
- NativeAOT 发布后的真实可执行文件启动。

不能把 dotnet run 通过视为 Windows 发布验收。必须运行 publish 目录内的 NativeAOT exe。

### 21.5 视觉测试

为 Browser 和 Windows 分别维护 golden：

- 盒子基础图元。
- 边框圆角。
- 渐变。
- clip 和 transform。
- 文本和复杂脚本。
- 控件状态。
- 主题。

比较策略：

- 几何区域严格或小容差。
- 字体抗锯齿使用感知差异阈值。
- 平台之间不互相作为 golden。
- 失败报告同时输出 expected、actual、diff 和布局树。

### 21.6 全栈契约测试

- Client proxy 和 Server dispatcher 对同一 manifest。
- contract hash 不一致时明确拒绝并触发开发期协调。
- 参数取消映射到 HttpContext RequestAborted。
- 授权在真实服务端执行。
- 错误不泄露堆栈和数据库信息。
- AOT 下 JSON 无反射回退。
- 旧客户端与允许的兼容服务端版本策略。

### 21.7 生产验收

Browser：

1. clean restore。
2. Release Wasm AOT publish。
3. Server publish 包含 AppBundle。
4. 从 publish 目录单独启动 Server。
5. 新浏览器上下文访问根路由和深层路由。
6. 验证静态缓存头、Server Function、中文、输入和首帧。
7. 停止服务后确认没有外部前端进程仍在提供页面。

Windows：

1. Release NativeAOT publish。
2. 在没有 dotnet 开发宿主参与的情况下运行 exe。
3. 验证字体、输入、窗口、渲染和远程函数。
4. 检查依赖库和许可清单。

## 22. 性能预算

这些是原型阶段的目标预算，不是未经测量的承诺。

### 22.1 帧预算

60 Hz 可交互页面：

| 阶段 | 常规更新目标 |
|---|---:|
| 响应式调度和 Build | 小于 2 ms |
| Reconcile | 小于 1 ms |
| Layout | 小于 2 ms |
| Scene compile 和 packet | 小于 1.5 ms |
| JS bridge 和 upload | 小于 1.5 ms |
| GPU render | 小于 4 ms |
| 余量 | 大于 4 ms |

120 Hz 不是首版强制目标，但滚动和简单 transform 动画应能在支持设备上尽量接近。

### 22.2 规模用例

必须建立这些基准：

- 1,000 个静态盒子首次布局和绘制。
- 10,000 个轻量 RenderNode 的局部颜色更新。
- 1,000 行虚拟列表滚动。
- 100 个状态单元一次事务中更新，但只有 10 个可见订阅者。
- 中英文混排长列表。
- 反复挂载和卸载，检查 scope 和 GPU 资源不增长。

### 22.3 启动和包体

浏览器优先测量：

- cold load 下载字节。
- Wasm runtime 初始化。
- managed assembly 加载。
- WebGPU 设备建立。
- 首次字体和 pipeline 准备。
- 首帧与可交互时间。

先记录基线，再决定懒加载、裁剪和 AOT 范围。不能为了包体盲目牺牲中文和诊断。

建议早期目标：

- 开发期首次可见小于 3 秒，后续热更新远小于完整重启。
- Release 在正常桌面网络和缓存为空条件下尽量将可交互控制在 2 到 4 秒。
- 缓存命中后明显更快。

这些数字需要以实际 .NET 11 Preview 7 和目标浏览器验证后修订。

### 22.4 分配预算

- 空闲帧零持续托管分配。
- 仅颜色变化不重建描述子树。
- 高频 pointer move 和滚动采用结构体事件或池化路径。
- Frame Packet 使用可复用缓冲区。
- 文字和图片缓存有上限、命中率和驱逐统计。
- 调试模式可增加诊断开销，Release 移除非必要跟踪。

## 23. 分阶段路线图

### P0：解决方案骨架与热重载门

交付：

- 固定 SDK 和集中构建配置。
- 建立项目依赖边界。
- HelloZui、HotReloadLab。
- 最小 Source Generator。
- Browser 和 Windows 空窗口宿主。
- Server 和 DevHost 固定拓扑。
- H0 到 H3 状态报告。

退出条件：

- 三个宿主可 clean build。
- 浏览器和 Windows 都能热改一段 C# 绘制代码。
- Server 方法体变更不要求客户端重启。
- 编译错误时旧 generation 继续运行。

### P1：WebGPU 场景切片

交付：

- IWebGpuApi。
- Browser Frame Packet decoder。
- Native backend 原型。
- 矩形、圆角、边框、图片、clip、transform。
- GPU 资源 ring 和 device lost 恢复。
- WGSL 热替换。

退出条件：

- 同一 Display List 在两平台绘制。
- 1,000 盒子基准和局部更新基准。
- Frame Packet ABI 测试冻结为 v1 draft。

### P2：DSL、主题和响应式

交付：

- Page、Component、builder 继承层。
- DIV、TEXT、BUTTON 等最小节点。
- State partial property。
- 动态依赖、Computed、Batch、Scope。
- keyed reconcile。
- 主题令牌载体。
- 分析器基础规则。

退出条件：

- 蓝图中的基础页面示例可编译。
- 条件分支和列表正确保留或释放状态。
- 主题修改只重绘受影响节点。
- Hot Reload Lab 的 State 迁移通过。

### P3：布局、文本、输入和无障碍

交付：

- 基础布局集合。
- Browser Canvas/OffscreenCanvas 文本路径。
- Windows DirectWrite。
- 中文和复杂脚本。
- 浏览器 IME textarea。
- Windows TSF。
- Semantics DOM 和 UIA 最小实现。
- Button、Input、Scroll 基础行为。

退出条件：

- 中文输入、选择、删除和组合事件在两平台通过。
- 单独平台 golden 稳定。
- 键盘可完成主要样例操作。

### P4：全栈闭环

交付：

- ServerFunctions 生成器。
- Client proxy、dispatcher、JSON context、contract hash。
- 授权、验证、错误、取消和追踪。
- Browser AppBundle 集成 Server publish。
- DevHost 协调契约 generation。

退出条件：

- Users feature 不手写 Controller、URL 或客户端接口。
- Browser 和 Windows 都能调用同一服务端。
- 生产只启动 ZAdmin.Server 即可访问浏览器 UI。
- 契约错配不会产生静默错误。

### P5：控件和应用组合

交付：

- Form、Select、Dialog、Menu、Tabs、Table、VirtualList。
- FocusScope 和弹层。
- 更完整主题和可定制视觉。
- ZAdmin 实际管理页面。

退出条件：

- 用真实 feature 验证 DSL 没有退化成巨型 lambda。
- 无障碍和键盘导航通过。
- 大表格和虚拟列表性能达到预算。

### P6：生产强化

交付：

- Browser Wasm AOT 优化。
- Windows NativeAOT。
- 包体和启动优化。
- 安全审计。
- 崩溃、GPU 诊断和遥测。
- 发布和升级策略。
- 协议兼容和迁移文档。

退出条件：

- 真实 publish 目录验收。
- 资源和 scope 长时间压力无泄漏。
- 关键平台故障具有可恢复或明确降级行为。
- 公共 API 和 ABI 形成版本策略。

## 24. 架构停止门

在以下节点必须先评审再继续扩大实现：

### Gate A：DSL 公共形态

用至少三个真实页面书写，评审：

- 单 lambda 是否仍然可读。
- 属性载体 IntelliSense 是否合理。
- 主题令牌是否足够简洁。
- 组合函数与 Component 的边界。
- 编译错误是否可理解。

通过前不批量建设控件。

### Gate B：文本引擎

Browser Canvas run 与 Windows DirectWrite 原型比较：

- 中文正确性。
- 复杂脚本。
- 缓存压力。
- 缩放。
- 输入命中位置。

通过前不承诺高级排版 API。

### Gate C：Frame Packet ABI

冻结 v1 draft 前验证：

- 浏览器跨边界成本。
- 协议可扩展。
- 回放和调试。
- 资源更新和 device lost。

冻结后任何破坏性变更必须升级协议版本。

### Gate D：Server Function 契约

用真实 Users 和 Auth 功能验证：

- 生成代码可读。
- 授权不可绕过。
- 错误模型好用。
- 版本错配可诊断。
- 文件和流式数据有清晰扩展路径。

### Gate E：NativeAOT

不能等全部控件完成才验证。P1、P3、P4 结束均运行 NativeAOT，发现动态反射或原生绑定问题就立即纠正。

## 25. 风险登记

| 风险 | 影响 | 早期验证 | 缓解 |
|---|---|---|---|
| .NET 11 Preview API 变化 | 构建和包升级频繁 | 固定 SDK，周度升级分支 | 隔离 SDK 特定适配层 |
| 浏览器 WebGPU 绑定不成熟 | JS 互操作成本或 API 缺口 | P1 Frame Packet 原型 | 粗粒度 JS decoder，IWebGpuApi 隔离 |
| 文本正确性复杂 | 中文、IME、复杂脚本不可用 | P3 TextLab 优先 | 浏览器复用系统 Canvas，Windows DirectWrite |
| 自定义布局范围膨胀 | 长期追赶 CSS | 明确首版能力矩阵 | 不承诺 CSS 兼容，真实应用驱动扩展 |
| DSL 生成器错误难理解 | 开发体验下降 | 三个真实页面和诊断测试 | 简单生成物、源码映射、分析器说明 |
| Wasm AOT 包体和启动慢 | 浏览器主要目标受损 | 从 P0 记录启动分解 | 裁剪、懒加载、缓存、按证据决定 AOT 范围 |
| NativeAOT 与 FFI | Windows 发布失败 | 每阶段 publish | 源生成 P/Invoke，无运行时扫描 |
| 热重载状态不稳定 | 日常开发低效 | HotReloadLab 作为首个门 | 分等级、旧 generation 回退、明确诊断 |
| Server Function 隐藏网络成本 | N+1 调用和体验差 | 调用诊断和分析器 | Server 名称、追踪、批量函数建议 |
| 无障碍后补成本极高 | 产品不可用 | 最小语义树从 P3 进入 | builder 语义和编译诊断 |
| 浏览器与 Windows 视觉差异 | 测试误判 | 独立 golden | 语义一致和几何容差，不追求像素同一 |
| 过度泛型导致 AOT 膨胀 | 包体和编译时间增加 | AOT size diff | 公共入口强类型，内部归一化数据 |
| GPU 资源泄漏 | 长会话崩溃 | 反复挂载和 device lost 压测 | scope 所有权、延迟退休和预算统计 |

## 26. 待审阅决策清单

下面每项都可单独接受、修改或拒绝。括号内是本蓝图推荐。

1. 浏览器是首要平台，Windows 11 x64 同步通过每个阶段的核心验收。（接受）
2. 业务 UI 只写纯 C#；固定宿主允许框架内部最小 HTML、JS 和 WGSL。（接受）
3. 所有内置节点使用大写函数名，例如 DIV、BUTTON、INPUT。（接受）
4. 元素结构、样式、事件、语义和子节点使用同一个 builder lambda。（接受）
5. Theme 不作为每个节点 lambda 的额外参数；通过 Page<MyTheme> 泛型自动传播。（接受）
6. 属性 setter 返回 void，不提供跨属性 fluent 链。（接受）
7. Builder 使用浅继承；不使用 CRTP 作为默认 API。（接受）
8. 普通 if、switch、foreach 是控制流；动态列表用稳定 key。（接受）
9. State 使用 partial property 加源生成；Build 阶段禁止写状态和发起 I/O。（接受）
10. 首阶段只保留少量注解，不提供 Inject、Css、Computed、Effect 等便利注解。（接受）
11. 响应式采用动态依赖、三级脏状态、懒 Computed、scope 所有权和帧批处理。（接受）
12. 浏览器采用 WebGPU canvas 加最小语义 DOM 和隐藏 textarea，不渲染普通业务 DOM。（接受）
13. 浏览器系统文字优先用 Canvas 或 OffscreenCanvas 栅格化文本 run 后上传 WebGPU。（接受）
14. Windows 使用 DirectWrite、TSF 和 UI Automation。（接受）
15. 默认不自带中日韩字体；默认 MissingFontPolicy 为 SystemOnly。（接受）
16. Browser 和 Windows 各有自己的视觉 golden，不追求跨平台像素完全一致。（接受）
17. UI 使用自定义 Measure/Arrange 布局，不承诺完整 CSS。（接受）
18. Server Function 用同文件夹签名源生成代理；开发者不写 URL、Controller 或客户端接口。（接受）
19. 浏览器与服务端仍保持物理程序集和 HTTP 安全边界。（接受）
20. Server 类型保留 Server 后缀，并在诊断中明确网络成本。（接受）
21. 生产只部署 ZAdmin.Server，浏览器 AppBundle 内置其中。（接受）
22. YARP 是按需的受控能力，不是默认依赖。（接受）
23. DevHost 使用不可变 generation、健康检查和原子切换协调 Browser 与 Server。（接受）
24. Debug 使用 JIT 或 Wasm 调试运行时；Release 才使用 Wasm AOT 和 Windows NativeAOT。（接受）
25. WGSL、图片和字体资源支持帧边界原子热替换，失败保留旧资源。（接受）
26. HotReloadLab 是 P0 的硬门，未稳定前不建设复杂控件。（接受）
27. 解决方案采用 feature-first 应用目录和按职责拆分的 Zui 库，不创建空泛 Common、Utils 项目。（接受）
28. 批准实施后移除当前 InvariantGlobalization=true，以支持中文和文化数据。（接受）
29. WebGPU native 具体选择 Dawn 或 wgpu-native 延迟到 P1 实测后 ADR 决定。（接受）
30. 本次只新增蓝图；现有根项目文件是否迁移在蓝图批准后另行执行。（接受）

## 27. 建议先审阅的三个代码切片

宏观决策通过后，不应马上实现全部项目。先为下面三个切片各写一份可编译 API 草案。

### 切片一：Counter 和主题

验证：

- DIV 的单作用域可读性。
- Page<MyTheme> 的主题传播。
- State partial property。
- 颜色、间距和文字属性载体。
- H0/H1 状态保留。

### 切片二：Users 全栈页面

验证：

- Models、Page、Server 邻近组织。
- Server Function 代理和错误模型。
- 搜索取消和旧结果丢弃。
- 授权。
- Browser 与 Windows 共用页面。

### 切片三：中文输入和列表

验证：

- 系统字体匹配。
- 中文 IME。
- 文本测量、换行和命中位置。
- 滚动与虚拟化。
- 语义树。
- 浏览器和 Windows 独立 golden。

这三个切片足以暴露大部分公共 API 缺陷。它们通过后再冻结 v0.1 API。

## 28. 审阅后的实施动作

只有在本文被明确接受或逐项修订后，才执行：

1. 将文档状态从 Draft 改为 Accepted 或 Accepted with changes。
2. 为以下关键选择建立 ADR：
   - DSL 和主题传播。
   - 响应式图。
   - Frame Packet。
   - 浏览器文字路径。
   - Windows WebGPU native 实现。
   - Server Function 协议。
   - DevHost generation。
3. 再次验证本机 .NET 11 Preview 7 SDK 和浏览器 WebGPU 能力。
4. 创建 P0 的解决方案骨架。
5. 保留现有根文件，直到确认其迁移目标。
6. 先实现 HotReloadLab 和最小空场景，不先实现业务控件库。
7. 每个阶段提供真实 Browser、Windows 和 publish 验收证据。

本文批准前不应：

- 大量创建占位项目或空接口。
- 决定 Dawn 与 wgpu-native 的最终选择。
- 把 C:\code\zui-old 的 API 原样搬入新仓库。
- 把 C:\code\zadmin 的 Svelte/TypeScript 结构机械转换成 C#。
- 删除或重写当前根项目。
- 推送远程分支或发布包。

## 29. 一句话架构

ZUI 是一个以浏览器为首要平台、Windows 11 同步验证的纯 C# 客户端 UI 运行时：C# DSL 和细粒度响应式系统在 CPU 上生成可增量更新的布局与场景，浏览器和 Windows 通过各自的 WebGPU、系统字体、输入法和无障碍适配器呈现；同一 feature 中的服务端函数由编译器生成强类型 HTTP 边界，最终由一个 ASP.NET Core 程序同时托管前端包和服务端逻辑，并由 DevHost 提供跨客户端、服务端、契约和着色器的一致热重载。

## 30. 官方依据、推导边界和版本风险

### 30.1 本次核验状态

核验日期：2026-08-26。

- 目标仓库执行 dotnet --version，实测为 11.0.100-preview.7.26381.103。
- Microsoft 的 .NET 11 总览仍明确标记 .NET 11 为 Preview；网页内容可能落后于本机 Preview 7 SDK，因此具体 MSBuild 属性、Browser Wasm 启动文件和 Hot Reload 能力必须以该固定 SDK 的实际模板、build 和 publish 结果为准。
- MDN 当前仍把相关 WebGPU API 标为非 Baseline，并要求 secure context。因此“现代浏览器”不是无限范围承诺，P0 必须形成支持矩阵，生产必须使用 HTTPS。
- Local Font Access 仍是草案型、需要权限的高级 API，所以本文只把它作为可选能力，不作为中文显示前提。

### 30.2 主要一手资料

| 主题 | 资料 | 支持的蓝图结论 |
|---|---|---|
| .NET 11 状态 | [.NET 11 新特性总览](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview) | net11.0 和 C# 15 仍处于预览期，需固定 SDK |
| C# 扩展块 | [C# extension 声明](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/extension) | 顶级非泛型 static class 内可声明带泛型约束的 extension block |
| partial property | [Partial classes and members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods) | partial property 可由 Source Generator 提供实现 |
| ASP.NET 静态资源 | [Static files in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files) | MapStaticAssets 基于构建期 manifest，支持指纹、压缩和缓存 |
| SPA fallback | [MapFallbackToFile 行为](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/7/fallback-file-endpoints) | fallback 适用于浏览器 GET 和 HEAD 深层路由 |
| .NET Hot Reload | [.NET Hot Reload for ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/hot-reload) | 运行中应用可接收受支持的代码更新，但启动和路由等变化有限制 |
| 热重载扩展点 | [MetadataUpdateHandler](https://learn.microsoft.com/en-us/visualstudio/debugger/hot-reload-metadataupdatehandler) | 框架可清缓存并主动重新渲染受影响 UI |
| WebGPU 外部图像上传 | [GPUQueue.copyExternalImageToTexture](https://developer.mozilla.org/en-US/docs/Web/API/GPUQueue/copyExternalImageToTexture) | Canvas、OffscreenCanvas 和 ImageBitmap 可上传到 GPUTexture |
| WebGPU 设备丢失 | [GPUDevice.lost](https://developer.mozilla.org/en-US/docs/Web/API/GPUDevice/lost) | device 可随时丢失，旧资源必须随新 device 重建 |
| WebGPU 规范 | [W3C WebGPU](https://www.w3.org/TR/webgpu/) | API、资源和验证语义的规范基线 |
| 本地字体权限 | [Local Font Access API](https://wicg.github.io/local-font-access/) | 枚举或访问本地字体是独立且受权限控制的高级能力 |
| DirectWrite 系统字体 | [Introducing DirectWrite](https://learn.microsoft.com/en-us/windows/win32/directwrite/introducing-directwrite) | Windows 可访问系统字体 collection 并进行字形布局与位图渲染 |
| DirectWrite fallback | [DirectWrite font selection](https://learn.microsoft.com/en-us/windows/win32/directwrite/font-selection) | DirectWrite 提供字体匹配和 MapCharacters fallback |
| NativeAOT | [ASP.NET Core Native AOT](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot) | 需要修复 AOT 警告并用源生成替代无界反射 |
| P/Invoke | [P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) | LibraryImport 生成路径适合 AOT 和裁剪 |
| SvelteKit SPA | [SvelteKit single-page apps](https://svelte.dev/docs/kit/single-page-apps) | 关闭 SSR 后以 fallback 页面和客户端路由运行 SPA |

### 30.3 哪些是资料直接支持，哪些是本项目设计推导

资料直接支持：

- WebGPU 可以从 Canvas 或 OffscreenCanvas 复制图像到纹理。
- WebGPU device lost 必须处理并重建旧 device 资源。
- DirectWrite 能访问系统字体并进行字体匹配和 fallback。
- ASP.NET Core 静态资源采用构建期 manifest、指纹和压缩。
- .NET 提供 MetadataUpdateHandler、NativeAOT 分析器和源生成 P/Invoke。
- C# 提供 extension block 和 partial property。

本项目的设计推导，不是平台自动提供：

- 把 Canvas 文本 run 缓存并组织成 WebGPU UI 文本后端。
- Frame Packet 粗粒度 C# 到 JS 协议。
- CLEAN、MAYBE_DIRTY、DIRTY 响应式实现。
- H0 到 H3 热重载分级和不可变 generation。
- Server Function 跨项目契约导出。
- 单 builder DSL、主题属性载体和大写节点函数。
- Browser 和 Windows 独立视觉 golden。

这些推导必须通过本文定义的 P0 到 P4 原型和停止门验证，不能因为底层 API 存在就视为已经解决。

### 30.4 需要持续复核的时间敏感点

每次升级 .NET Preview 或目标浏览器时重新验证：

- Browser Wasm SDK 的启动方式、AOT 和 Hot Reload。
- WebGPU API、浏览器支持矩阵、secure context 和 worker 支持。
- Canvas、OffscreenCanvas 和 copyExternalImageToTexture 的行为与性能。
- NativeAOT 和 LibraryImport 警告。
- ASP.NET Static Web Assets manifest 格式。
- C# Preview 语法和 Roslyn Source Generator API。
- Dawn 或 wgpu-native 的 ABI、许可证和发布方式。

蓝图引用资料用于说明当前可行性，不把任何 Preview 或浏览器实现细节当作永久稳定 API。
