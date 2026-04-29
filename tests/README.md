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
- 资源预览查询时的缓存命中、`SourceFingerprint / Identity / GeneratorVersion / AudioTranscoder` 失效重建、音频摘要在新转码器可用时的升级重建，以及解密失败 / 生成失败摘要回退链路
- 替换链路中的候选类型解析、来源路径校验、缺失资源失败、类型不匹配、工作区候选空集失败、单资源文件/工作区替换、撤销替换，以及批量替换预览/执行摘要
- 保存链路中的保存前预览空态/资源包分组聚合，以及保存执行的缺失工作区、无候选失败和执行摘要
- 工作区目录相关 `Infrastructure` 持久化关键路径（当前也通过零依赖控制台 runner 执行）
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

断言风格约束：

- 优先断言稳定契约和可观察结果，例如 `Result`、错误码、消息键、结构化参数、DTO 字段、摘要和页面级状态变化
- 避免把私有实现细节、偶发调用次数、临时日志句子或尚未文档化的完整本地化整句写成刚性断言
- 只有当“精确文本本身”就是正式契约时，才断言完整用户文案

后续仍待补齐：

- 更完整的工作区激活，以及替换/保存链路在真实写包与最终持久化语义落地后的深层回归
- 结构化错误与资源键的更细粒度自动校验
