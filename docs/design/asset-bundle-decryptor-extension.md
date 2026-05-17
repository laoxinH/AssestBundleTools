# AssestBundleTools 资源包解密扩展设计

## 1. 文档目的

本文档用于约束“真实 AB 导入前的解密扩展层”设计，目标如下：

- 不把某个游戏或某种 AB 的解密逻辑硬编码进主导入流程
- 让真实 Reader 始终只面对“已可读取的资源包文件”
- 允许后续通过内置实现或外部进程扩展解密能力
- 保持 Clean Architecture 边界清晰，避免 UI、Application、Infrastructure 职责混乱

本文档与以下设计配套使用：

- [code-architecture-guidelines.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/code-architecture-guidelines.md)
- [workspace-management.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/workspace-management.md)
- [project-build-roadmap.md](C:/Users/thixi/source/repos/AssestBundleTools/AssestBundleTools/docs/design/project-build-roadmap.md)

## 2. 设计目标

### 2.1 解密器和 Reader 必须分离

真实 `IAssetBundleReader` 的职责应该始终是：

- 读取标准资源包文件
- 生成 Bundle 树、资产列表和详情
- 驱动索引缓存和预览缓存写入

它不应该额外承担：

- 判断某个来源是否加密
- 识别某个游戏的私有头格式
- 调外部脚本
- 管理解密中间文件

因此真实导入链路建议固定为：

1. 工作区导入入口收集文件来源
2. 解密器选择与执行
3. 真实 `IAssetBundleReader` 读取已可访问文件
4. 写入 `BundleIndexCacheStore / PreviewCacheStore`

### 2.2 不把 Python 作为架构概念

后续某些项目可能确实更适合通过 Python 解密，但主程序不应把“Python 脚本”本身设计成一级抽象。

主程序只应该识别：

- 内置解密器
- 外部进程型解密器

外部进程可以是：

- `python`
- `exe`
- `.NET tool`
- 其他可执行入口

这样主程序只依赖统一协议，而不依赖某种特定语言运行时。

### 2.3 解密结果属于暂存或缓存，不属于元数据

解密后的可读文件、脚本输出结果和运行时临时文件，不应写入 `WorkspaceMetadataDto`。

推荐位置：

- 临时工作文件：`staging/decrypt/...`
- 可复用标准化资源包缓存：`cache/bundles/<workspaceKey>/<bundleCacheKey>/...`

当前阶段已经明确后续演进为“两层缓存”：

- `staging/decrypt` 只承载单次解密执行的临时文件、外部进程结果文件和诊断材料。
- `cache/bundles` 承载已解密、已标准化、可直接被 Reader / Preview / Extraction 读取的可重建物理 artifact。

补充约束：

- 工作区“默认使用哪个解密器”属于轻量恢复配置，应写入 `WorkspaceMetadataDto.AssetBundleDecryptorId`
- 真正的解密输出文件与探测结果仍应留在 `staging / cache` 层，不回写工作区元数据
- `cache/bundles` 中的标准化 artifact 不是用户源文件副本，也不是撤销 / 恢复事实来源；源文件指纹、解密器身份、解密器版本或标准化版本变化时必须重建

## 3. 推荐分层

### 3.1 Application

Application 层只定义稳定端口与数据结构，例如：

- `IAssetBundleDecryptor`
- `IAssetBundleDecryptorRegistry`
- `AssetBundleDecryptorDescriptorDto`
- `AssetBundleDecryptorProbeRequestDto`
- `AssetBundleDecryptionRequestDto`
- `AssetBundleDecryptionResultDto`

Application 不负责：

- 扫描某个目录的 manifest
- 启动外部进程
- 读写 JSON 清单
- 拼接 Python 命令行

### 3.2 Infrastructure

Infrastructure 负责所有具体实现细节，例如：

- `PassThroughAssetBundleDecryptor`
- `ExternalProcessAssetBundleDecryptor`
- `FileSystemAssetBundleDecryptorRegistry`
- 解密器 manifest 解析
- 外部进程协议执行
- 中间文件目录与结果文件处理

### 3.3 Presentation

Presentation 只负责：

- 在工作区级别让用户查看或选择可用解密器
- 在新建工作区向导中选择默认解密器，并在打开已有工作区时只读展示当前配置
- 展示解密失败、识别失败和导入结果
- 在“设置与诊断”页承载解密器目录管理、发现结果与诊断信息

Presentation 不负责：

- 直接运行脚本
- 自己挑选某个解密器并绕过 Application
- 自己维护解密临时文件

## 4. 当前推荐机制

## 4.1 先做“解密器扩展点”，不先做通用插件市场

当前阶段不建议直接引入大而全的插件系统。

先收敛成一个足够稳定的专项扩展点：

- `PassThrough` 内置回退解密器
- `ExternalProcess` 外部进程解密器

这样可以先满足真实 AB 导入的核心诉求，同时避免把插件市场、安装器、签名校验等范围一起拉进来。

## 4.2 外部进程协议

外部进程型解密器统一遵循下面的约束：

- 主程序通过 manifest 发现它
- 主程序通过命令行参数把 `input/output/result/workspace` 等路径传给它
- 外部进程把执行结果写入 `result` JSON 文件
- 主程序读取结果文件并生成 `AssetBundleDecryptionResultDto`

推荐参数占位符：

- `{inputPath}`
- `{outputPath}`
- `{resultPath}`
- `{workspaceKey}`
- `{workspaceDataRootPath}`
- `{manifestDirectory}`

这样即使外部进程是 Python，也可以通过：

- `executablePath = "python"`
- 参数里引用 `{manifestDirectory}\\decrypt.py`

来稳定运行，而不是要求主程序理解 Python 项目结构。

## 4.3 Manifest 结构

当前推荐 manifest 字段如下：

```json
{
  "id": "game.example.decryptor",
  "displayName": "Example Game Decryptor",
  "version": "1.0.0",
  "description": "Decrypts encrypted asset bundles for Example Game.",
  "supportsAutoDetection": true,
  "isFallback": false,
  "executablePath": "python",
  "workingDirectoryPath": ".",
  "timeoutMs": 15000,
  "probeArguments": [
    "{manifestDirectory}\\decrypt.py",
    "probe",
    "--input",
    "{inputPath}",
    "--result",
    "{resultPath}"
  ],
  "decryptArguments": [
    "{manifestDirectory}\\decrypt.py",
    "decrypt",
    "--input",
    "{inputPath}",
    "--output",
    "{outputPath}",
    "--result",
    "{resultPath}"
  ]
}
```

补充约束：

- `executablePath` 可以是 PATH 上的命令，也可以是相对 manifest 的可执行文件路径
- `decryptArguments` 必填
- `probeArguments` 只有在支持自动识别时才需要提供
- 当前阶段 manifest 读取失败或字段不完整时，主程序应跳过该项，而不是阻断整个应用启动

## 5. 默认发现位置

当前推荐默认发现目录：

```text
%LocalAppData%/AssestBundleTools/decryptors/
<app-base-directory>/decryptors/
```

这样兼顾两类场景：

- 用户自行安装或复制解密器
- 开发阶段把解密器和应用放在同一目录附近调试

## 6. 与工作区缓存的关系

真实导入链路当前按下面的顺序演进：

1. 工作区入口记录导入来源
2. 根据用户配置或自动识别选择解密器
3. 解密到 `staging/decrypt/...`
4. 将可复用结果晋升或写入 `cache/bundles/...` 标准化资源包缓存
5. 真实 `IAssetBundleReader` 读取标准化资源包 artifact
6. 把解析索引写入 `cache/bundle-index/...`
7. 在工作台真正请求时按需生成预览，并把预览派生内容写入 `cache/previews/...`

缓存失效时除了原始 AB 指纹外，还应纳入：

- `decryptorId`
- `decryptorVersion`
- `normalizerVersion`

否则同一个源文件在切换解密器实现后，旧缓存可能继续误命中。

## 7. 当前落地范围

当前代码已落以下稳定子集：

- Application 层已定义 `IAssetBundleDecryptor / IAssetBundleDecryptorRegistry`
- 已定义解密器描述、探测请求、解密请求与结果 DTO
- 已补齐解密器目录查询、保存和删除的应用层命令 / 查询处理器
- Infrastructure 已提供 `PassThroughAssetBundleDecryptor`
- Infrastructure 已提供 `ExternalProcessAssetBundleDecryptor`
- Infrastructure 已提供基于 manifest 的 `FileSystemAssetBundleDecryptorRegistry`
- Infrastructure 已提供可重载的 `ReloadableAssetBundleDecryptorRegistry`，保证设置页刷新后导入链路仍可读取最新解密器定义
- Infrastructure 已提供 `FileSystemAssetBundleDecryptorCatalogStore`，统一承载内置只读项和用户可管理外部进程解密器定义
- `App` 组合根已统一装配 `AssetBundleDecryptorRegistry / AssetBundleDecryptorCatalogService`，并在目录变更后重载注册表
- 工作区元数据已支持持久化 `AssetBundleDecryptorId`
- `WorkspaceContextService` 已补齐工作区解密器的注册、恢复与导航回填
- 新建工作区向导已支持选择已有解密器，打开已有工作区时会只读展示元数据中的解密器配置
- “设置与诊断”页已提供解密器管理区，支持新增、编辑、删除、刷新
- 工作台顶部状态标签已显示“当前解密器”，替代原先的冗余“当前选中”提示
- 工作区导入链路已正式按 `AssetBundleDecryptorId` 选择解密器执行，并把解密后文件送入 `IAssetBundleReader`
- 当前真实导入结果已写入 `cache/bundle-index/...`，再由缓存型工作区会话驱动资源包列表恢复
- Infrastructure 当前已接入 `AssetsTools.NET` 真实 Reader，并在读取失败时回退到启发式 Reader，避免真实导入阶段把资源包列表回退成不可用状态
- 当前 Reader 已可从解密后文件中读取序列化资产、资产名和 `m_Container` 路径，供工作台恢复资产列表与基础详情摘要
- 资源包索引缓存当前已持久化 `decryptorId / decryptorVersion`，并参与导入刷新时的缓存复用判定
- 当前导入刷新已按 `decryptorId + decryptorVersion + sourceFingerprint + parserVersion + toolVersion` 执行命中复用与失效重建
- 当前已落地 `cache/bundles` 首版标准化资源包物理缓存：导入、预览、提取和 Spine 详情通过 Application artifact resolver 优先取得同一可读 bundle 路径；同一 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 的 artifact 生成阶段会 single-flight 串行化，外部解密器派生输出会从 `staging/decrypt` 晋升为 `normalized.bundle`，PassThrough 场景只写 manifest 并指向原始可读文件
- 当解密器切换、标准化版本变化或来源移除导致缓存失效时，当前实现会清理对应资源包的物理 artifact、索引缓存与预览缓存；若只是 parser / tool 版本变化并且 artifact 身份仍有效，会保留标准化物理缓存以供重建索引复用
- 工作台资源详情当前已可在真实读取链路上按需生成预览，并将结果写入 `cache/previews/...`
- 当前预览链路会在预览缓存缺失时先复用 `cache/bundles` manifest；若 artifact 缺失或失效，再经过工作区已选解密器，确保加密资源包也能生成最新预览
- 当前预览类型覆盖如下：
  - 图像预览：`Texture2D`、`Sprite`
  - 文本预览：`TextAsset`、`Shader`
  - 音频预览：`AudioClip`
  - 模型预览：`Mesh`
  - 半可视化摘要预览：`Material`、`AnimationClip`
  - 结构化摘要预览：`GameObject`、`MonoBehaviour`、`AnimatorController`、`Font`、`SpriteAtlas`
  - 通用摘要回退：其余可读取资源类型

当前明确暂不实现：

- 自动识别结果的完整 UI 诊断与冲突决策流程
- 把解密结果作为不可重建的长期明文副本管理；当前只实现可失效、可清理、可重建的 `cache/bundles` 标准化 artifact
- 更通用的插件安装、签名与市场机制

## 8. 下一步建议

建议按下面顺序继续推进：

1. 明确工作区级“自动检测 / 指定解密器 / 仅直读”策略，并定义冲突处理规则
2. 基于当前已回写的 `lastAccessedAtUtc` 为 `cache/bundles` 补齐容量上限和显式清理入口
3. 补齐更完整的失败诊断与自动识别结果表达
4. 将真实导出 / 写包入口也改为优先消费标准化 artifact，而不是重新走源文件解密
5. 最后再评估是否需要更通用的插件安装与签名机制
