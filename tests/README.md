# Tests

当前已建立第三轮最小回归骨架：

- `AssestBundleTools.Domain.Tests`
- `AssestBundleTools.Application.Tests`
- `AssestBundleTools.Infrastructure.Tests`
- `AssestBundleTools.Localization.Tests`
- `AssestBundleTools.Presentation.Tests`

当前覆盖重点：

- `AppError / Result` 基础行为
- 工作区目录相关 `Application` Handler 的关键路径（当前通过零依赖控制台 runner 执行）
- 工作区资源包移除时的元数据修正与缓存失效链路
- 工作区导入来源登记时的路径去重、更新计数、类型统计与排除列表修正
- 工作区导入处理时的缓存复用命中、解密失败、Reader 拒绝、读取失败以及陈旧 bundle / preview 缓存清理与 `SourceRemoved` 诊断
- 资源预览查询时的缓存命中、Mesh / Material / AnimationClip / Font / GameObject / MonoBehaviour / AnimatorController 结构化载荷水化、AnimationClip binding / 曲线摘要展示、GameObject 自适应预览摘要卡片、组件引用信息卡与已解析资源跳转展示、MonoBehaviour 自适应预览摘要卡片、脚本 / 字段分组 / 序列化容器 / 字段 / 引用信息卡与已解析资源跳转展示、AnimatorController 自适应预览摘要卡片、状态机容器 / 参数 / AnimationClip 引用信息卡与已解析资源跳转展示、`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建、音频摘要在新转码器可用时的升级重建，以及解密失败 / 生成失败摘要回退链路
- Spine 资源组第一版 Application 读模型中的 `.atlas / .skel / .skel.bytes / .json` 组识别、跨工作区 `Texture2D` page 候选匹配、atlas 预览文本 page / region 解析、page 到 Texture2D 组成资产映射、JSON skeleton 的 bone / slot / skin / animation 轻量计数摘要、默认 skin region attachment 与基础 timeline 的实验性动画预览 DTO、runtime-neutral sampled frames / draw commands、`V21 / V34 / V35 / V36 / V37 / V38 / V40 / V41 / V42` runtime adapter 版本矩阵、无匹配 adapter 的结构化诊断、缺失 atlas / texture page / atlas 预览不可用、atlas page 未匹配 Texture2D、Texture2D 预览不可用、page 尺寸与图片尺寸不一致诊断，`.skel / .skel.bytes`、非默认 skin、mesh / weighted vertices、deform、draw order、slot 颜色、event、约束和 Bezier 曲线等实验动画预览未支持特性的结构化诊断，以及 Presentation 资产窗格 `资源 / Spine 组` 视图切换、浏览状态持久化、主窗口右侧组文档去重打开、组文档轻量恢复、统一资源文档标签组内普通资产与 Spine 组混排 / 重排 / 移出、独立窗口轻量快照恢复、详情回填、atlas page 选择切换、Texture2D region overlay、skeleton 摘要和实验性动画预览投影
- 编辑草稿恢复候选解析中的工作区范围校验、轻量引用标准化、可恢复 / 缺失 / 来源不匹配 / 过期 / 引用无效诊断分类，工作区变更暂存中的 `editDrafts[]` 描述读取，以及 Presentation 恢复弹窗的草稿候选预检摘要投影和可恢复标记筛选
- 资源编辑窗口模式中的打开冲突解析：主窗口和独立资源编辑窗口已存在同一资源文档标签时，优先解析到可激活的已有宿主和目标标签，避免重复创建窗口或标签；独立窗口单标签拖出策略会阻止创建等价新窗口
- 替换链路中的候选类型解析、来源路径校验、缺失资源失败、类型不匹配、工作区候选空集失败、单资源文件/工作区替换、撤销替换，以及批量替换预览/执行摘要
- 保存链路中的保存前预览空态/资源包分组聚合，以及保存执行的缺失工作区、无候选失败和执行摘要
- 工作区目录相关 `Infrastructure` 持久化关键路径（当前也通过零依赖控制台 runner 执行）
- 缓存型工作区运行时依赖图中的反向引用、依赖闭包、Mesh 贴图关联，以及外部 `PPtr` 占位到当前工作区已导入资产的跨 Bundle 解析
- 缓存型工作区运行时资产列表中的动态类型 Facet、隐藏图节点排除、以及 `Unknown + Unity 类型名` 的资产级筛选
- 预览缓存描述文件 / 载荷文件的保存回读、删除清理，以及缺失 payload / 非法 payload path 下的容错加载与清理
- `zh-CN / en-US` 资源键一致性与 `ResourceKeys.cs` 覆盖率
- `Presentation -> Infrastructure` 组合根边界检查
- `Presentation` 本地化调用不得直接写资源键字面量
- 工作区进程占用链路中的 `pending` 预登记 / 晋升 / 清理行为
- 工作区加载失败后的页面级恢复与空态回滚链路

测试项目职责边界：

- `AssestBundleTools.Domain.Tests`
  - 只覆盖领域规则、值对象、实体和稳定领域错误/契约
- `AssestBundleTools.Application.Tests`
  - 覆盖 Handler / Use Case / `Result` 语义 / DTO 契约
  - 默认通过 fake / stub / in-memory double 驱动应用层端口
  - 不把 WinUI 或基础设施实现细节当作测试前提
- `AssestBundleTools.Infrastructure.Tests`
  - 覆盖持久化、序列化、目录布局和适配器行为
  - 可以使用受控临时目录 IO
  - 不承担页面级状态或窗口语义验证
- `AssestBundleTools.Localization.Tests`
  - 覆盖资源键完整性、`ResourceKeys`、架构 guardrail 静态检查
- `AssestBundleTools.Presentation.Tests`
  - 覆盖 ViewModel、协调器、窗口/导航/回滚语义，以及受控展示层边界检查
  - 不默认升级为“真实 Reader + 真实缓存 + 真实外部工具”的全链路集成测试

测试编写时机约束：

- 已稳定、已文档化的行为契约，以及已经确认的回归缺陷，应在同一变更中补齐或更新自动化测试。
- 对骨架、占位或过渡态流程，只测试当前已经实现且文档已承认的行为，不提前在测试中定义未来语义。
- 当前替换链路的测试边界先停留在候选解析、兼容性、摘要、已定义失败和当前命令结果。
- 当前保存链路的测试边界先停留在保存前预览、资源包聚合、执行骨架结果和当前已定义失败。
- 真实写包、最终持久化失败分类、取消/回滚、不可替换等更深语义，等对应实现和文档契约落地后再继续补测试。

默认依赖与副作用限制：

- 不依赖网络访问
- 不依赖真实用户目录、真实工作区目录或机器全局状态
- 不依赖不可控外部工具安装状态或机器特定环境
- 不依赖脆弱的休眠等待、竞态时序或当前系统语言文案
- 需要文件系统时，优先使用受控临时目录并在测试内自行清理

命令行验证备注：

- `AssestBundleTools.Application.Tests` 和 `AssestBundleTools.Infrastructure.Tests` 当前都是零依赖控制台 runner。
  在受限终端或沙箱环境里执行 `dotnet run` / `dotnet test` 时，若默认用户目录不可写，先设置可写的 `DOTNET_CLI_HOME`，并建议同时设置 `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`。
- `AssestBundleTools.Presentation.Tests` 项目固定为 `x86 / win-x86`。
  若用 `MSBuild` 或 `dotnet test` 单独验证它，请显式使用 `Platform=x86`，否则容易因为引用链先产出 `x64` DLL 而触发 `MSB3270`。
- `AssestBundleTools.Infrastructure.Tests` 应通过 `ProjectReference` 引用 `src/AssestBundleTools.Infrastructure`，不要固定 `HintPath` 到 `src/*/bin/Debug/...`。
  固定路径在 `Platform=x86` 构建时可能引用旧 DLL，导致测试编译提示新 DTO 字段不存在；这类错误优先检查项目引用方式，而不是先怀疑生产代码。
- `AssestBundleTools.Infrastructure.Tests` 和 `Presentation.Tests` 都会带起同一组上游项目。
  因此这两类验证不要并行跑；并行时容易争用 `src/*/obj` 或 `src/*/bin`，导致 `CS2012`、`MSB3021`、`MSB3027`。
- `AssestBundleTools.Application.Tests` 也会在解析引用前构建 `src/AssestBundleTools.Application`。
  不要把它和解决方案级 `MSBuild` 同时跑；这次曾复现 `CS2012`，具体表现为无法写入 `src/AssestBundleTools.Application/obj/Debug/net8.0/AssestBundleTools.Application.dll`。
  串行重跑后测试通过时，应把前一次视为验证命令争用，而不是优先怀疑业务代码。
- `AssestBundleTools.Presentation.Tests` 在当前 WinUI / x86 测试链路下，`dotnet test` 可能出现退出码 `1` 但没有测试正文的静默失败。
  若项目已经用 `MSBuild /p:Platform=x86` 构建成功，建议用 VS `vstest.console.exe` 直接执行 `bin/x86/.../AssestBundleTools.Presentation.Tests.dll`，
  并带上 `/Platform:x86 /InIsolation`；只有当输出里出现明确断言、加载或编译错误时，再按真实测试失败处理。
- 如果 `Presentation.Tests` 为了避开 WinUI 输出锁而构建到独立 `OutDir`，运行 `vstest` 前需要检查该 `OutDir` 根部是否含有 `hostfxr.dll / hostpolicy.dll`。
  这两个 app-local host 文件会让 x86 `testhost` 把临时输出目录误判为 `.NET location`，随后报找不到 `Microsoft.NETCore.App 8.0.0 (x86)`。
  只删除临时 `OutDir` 根部的 `hostfxr.dll / hostpolicy.dll` 后，再用：
  `vstest.console.exe <OutDir>\AssestBundleTools.Presentation.Tests.dll /Platform:x86 /InIsolation /TestAdapterPath:<OutDir>`。
- 如果只是为了避开运行中进程或 DLL 文件锁，优先保留默认 `obj`，只在解决方案或项目构建时重定向 `OutDir`。
  不建议把 `BaseIntermediateOutputPath` 当成通用解法；这次验证中它曾引出 `NETSDK1004` 和 `CS0579` 之类与真实代码无关的噪声错误。

断言风格约束：

- 优先断言稳定契约和可观察结果，例如 `Result`、错误码、消息键、结构化参数、DTO 字段、摘要和页面级状态变化
- 避免把私有实现细节、偶发调用次数、临时日志句子或尚未文档化的完整本地化整句写成刚性断言
- 只有当“精确文本本身”就是正式契约时，才断言完整用户文案

后续仍待补齐：

- 更完整的工作区激活，以及替换/保存链路在真实写包与最终持久化语义落地后的深层回归
- 结构化错误与资源键的更细粒度自动校验
