# AssestBundleTools 资源包解密与回写扩展设计

## 1. 文档目的

本文档用于约束“真实 AB 导入前的解密扩展层”和“真实 AB 导出后的来源格式回写层”设计，目标如下：

- 不把某个游戏或某种 AB 的解密逻辑硬编码进主导入流程
- 让真实 Reader 始终只面对“已可读取的资源包文件”
- 允许通过内置 CLI 或外部进程扩展识别、解密和可选加密能力
- 让导出 writer 始终先生成标准可读 AB，再由来源 codec 按需回写游戏私有格式
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

### 2.4 “加密”是可选的来源格式回写能力

游戏私有 AB 不一定使用密码学加密。例如明日方舟 LZ4AK 实际是 UnityFS 压缩块编码变体：sequence token 的高低半字节互换，match offset 使用大端序，并可能使用压缩类型 `4 / 5` 标记。

因此本扩展点保留产品和现有代码中的“解密器”命名，但按以下语义处理：

- `Probe`：判断来源是否属于该游戏 / 格式，并返回置信度和可选结构化证据。
- `Decrypt`：把私有来源转换为 Reader 可读取的标准 bundle。
- `Encrypt`：可选能力，把 writer 生成的标准 bundle 回写为该游戏可消费的私有格式。

`Encrypt` 不得与导出资源处理器混用。资源处理器修改单个资产载荷，bundle encryptor 在对象级写包和标准压缩完成后处理整个 AB 文件。

兼容规则：

- 现有只实现 `IAssetBundleDecryptor` 的内置实现、测试替身和外部插件继续有效。
- 需要回写能力的实现额外实现 `IAssetBundleEncryptor`；不把新方法强加到所有旧实现。
- `AssetBundleDecryptorDescriptorDto.SupportsEncryption` 只表示能力声明；真实执行还必须校验接口、插件版本和命令配置一致。
- `PassThrough` 不声明加密能力，标准来源导出后保持标准 bundle。

## 3. 推荐分层

### 3.1 Application

Application 层只定义稳定端口与数据结构，例如：

- `IAssetBundleDecryptor`
- `IAssetBundleEncryptor`
- `IAssetBundleDecryptorRegistry`
- `AssetBundleDecryptorDescriptorDto`
- `AssetBundleDecryptorProbeRequestDto`
- `AssetBundleDecryptionRequestDto`
- `AssetBundleDecryptionResultDto`
- `AssetBundleEncryptionRequestDto`
- `AssetBundleEncryptionResultDto`

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

- 在工作区级别让用户选择“自动识别”策略或显式固定解密器
- 在新建工作区向导中选择默认解密器，并在打开已有工作区时只读展示当前配置
- 展示解密失败、识别失败和导入结果
- 在“插件管理”页承载解密器目录管理、发现结果与诊断信息

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
- 主程序读取结果文件并生成 Probe、Decrypt 或 Encrypt 对应的应用层 DTO

推荐参数占位符：

- `{inputPath}`
- `{outputPath}`
- `{resultPath}`
- `{workspaceKey}`
- `{workspaceDataRootPath}`
- `{manifestDirectory}`

命令能力使用同一组占位符和同一套进程超时 / 取消规则：

- `probeArguments`：可选；存在时参与自动识别。
- `decryptArguments`：必填；把来源转换为标准 bundle。
- `encryptArguments`：可选；把标准 bundle 回写为游戏格式。

`probe` result JSON：

```json
{
  "schemaVersion": 1,
  "isMatch": true,
  "confidence": 100,
  "formatId": "game.example.bundle-v1",
  "evidence": [ "unityfs.custom-block-type" ],
  "message": "optional diagnostic fallback"
}
```

其中 `formatId / evidence` 是可选稳定语义，不是用户界面文案。主程序按 `isMatch + confidence` 选择解密器，并保留证据用于诊断；插件不得仅凭文件扩展名返回高置信度。

`decrypt / encrypt` result JSON 共用以下最小结构：

```json
{
  "schemaVersion": 1,
  "isSuccess": true,
  "outputPath": "D:/.../output.bundle",
  "message": "optional diagnostic fallback"
}
```

规则：

- 退出码、result JSON 和输出文件必须同时成功；stdout / stderr 只作为诊断 detail。
- `outputPath` 省略时使用调用方传入的 `{outputPath}`。
- 输出必须等于调用方给定路径或位于本次隔离 working directory 内，不能借 result JSON 越权返回任意文件。
- result JSON 缺失、解析失败、输出缺失、超时和取消均返回结构化失败。

这样即使外部进程是 Python，也可以通过：

- `executablePath = "python"`
- 参数里引用 `{manifestDirectory}\\decrypt.py`

来稳定运行，而不是要求主程序理解 Python 项目结构。

## 4.3 Manifest 结构

当前推荐 manifest 字段如下。`schemaVersion` 缺失时按首版兼容格式读取；`encryptArguments` 缺失或为空表示只支持导入解密：

```json
{
  "schemaVersion": 1,
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
  ],
  "encryptArguments": [
    "{manifestDirectory}\\decrypt.py",
    "encrypt",
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

- `executablePath` 可以是 PATH 上的命令，也可以是相对 manifest 的可执行文件路径；为避免纯文件名歧义，不含目录分隔符的值（如 `python`）按 PATH 命令解析，插件目录内的可执行文件必须写成 `./tool`、`.\tool.exe` 或其他包含目录分隔符的相对路径，绝对路径保持原样
- `decryptArguments` 必填
- `probeArguments` 只有在支持自动识别时才需要提供
- `encryptArguments` 可选；提供后插件必须保证 `decrypt -> encrypt -> decrypt` 的 bundle 内容回环一致性
- 当前阶段 manifest 读取失败或字段不完整时，主程序应跳过该项，而不是阻断整个应用启动

为兼容现有插件，`supportsAutoDetection` 暂时保留；保存新定义时它必须与 `probeArguments` 是否存在保持一致。后续若升级 manifest schema，可改为从命令是否存在推导能力，但本阶段不为删除一个布尔字段引入破坏性迁移。

## 4.4 内置 CrossCore duplicated-header v1 格式

交错战线（CrossCore）当前已验证样本使用 duplicated-header v1：私有文件由标准 UnityFS 的前 83 字节副本与完整标准 UnityFS 顺序拼接而成。因此内置 codec 固定使用以下稳定契约：

- codec id 为 `builtin.crosscore-duplicated-header`，format id 为 `crosscore.unityfs.duplicated-header-v1`。
- `Probe` 只在偏移 `0` 与 `83` 都存在完整 `UnityFS\0` 签名，且前 83 字节与偏移 83 开始的 83 字节逐字节相同时返回高置信度匹配；不得仅凭文件名、扩展名或“出现两个 UnityFS 字符串”命中。
- `Decrypt` 校验同一结构后删除前 83 字节，输出从第二个 UnityFS 开始的完整标准 AB。
- `Encrypt` 要求输入为未重复前缀的标准 UnityFS，把输入前 83 字节复制到输出开头，再写入完整输入；对已验证真实样本应满足 `Decrypt -> Encrypt` 后与原文件逐字节一致。
- CLI 继续复用 schema 1 的 `probe / decrypt / encrypt` 命令和 result JSON，不为该格式增加专用参数或把前缀写入工作区元数据。
- 附件旧实现中的 `header_len = 336` 与当前真实样本不一致，且其 encode 分支仍执行删除而不是重建前缀，因此不属于本格式版本契约。未来确认存在其他前缀结构时，应新增 format 版本或可验证的推导规则，并补对应真实样本回归。

内置 codec CLI 保持自包含单文件进程，但发布时必须启用 IL trimming 与单文件压缩，并用 `System.Text.Json` 源生成上下文输出协议 JSON。framework-dependent CLI 不能直接复用 WinUI 自包含输出目录中平铺的运行时，仍会要求机器全局安装匹配的 .NET Runtime；因此当前不以牺牲可执行稳定性换取更小的 apphost。若内置 codec 数量继续增长，应把多个算法收敛到一个共享自包含 CLI host，只携带一次运行时，而不是把 codec 入口并回 WinUI 进程。

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
2. 根据工作区策略为每个 AB 独立选择实际解密器
3. 解密到 `staging/decrypt/...`
4. 将可复用结果晋升或写入 `cache/bundles/...` 标准化资源包缓存
5. 真实 `IAssetBundleReader` 读取标准化资源包 artifact
6. 把解析索引写入 `cache/bundle-index/...`
7. 在工作台真正请求时按需生成预览，并把预览派生内容写入 `cache/previews/...`

自动识别与持久化规则：

- `builtin.auto-detect` 是持久化的工作区策略，不是一次性迁移状态；不得在识别后把工作区元数据改成某个具体 codec 或 `PassThrough`。
- 每个待导入 AB 都独立执行所有支持自动识别的 codec Probe，并按该文件上的 `isMatch + confidence + 稳定顺序` 选择实际 codec。
- 某个 AB 的所有 codec 都成功 Probe 且不匹配时，该文件使用 `PassThrough`；其他 AB 的识别结果不受影响。
- 某个 AB 存在 Probe 执行失败且没有其他匹配时，本轮仍可对该文件尝试 `PassThrough`，但诊断必须保留失败证据，后续刷新仍会重新 Probe。
- 实际 codec 的 id / version 只写入该 AB 的标准化 artifact 与 bundle index，继续参与缓存失效、预览重建和导出回写。

缓存失效时除了原始 AB 指纹外，还应纳入：

- `decryptorId`
- `decryptorVersion`
- `normalizerVersion`

否则同一个源文件在切换解密器实现后，旧缓存可能继续误命中。

## 6.1 与真实导出的关系

真实导出固定采用以下次序：

```text
标准化 artifact
  -> AssetsTools 对象级修改和标准 AB 写包
  -> 来源 decryptor 可选 Encrypt
  -> 输出校验 / hash
  -> 原子提升到最终路径
```

约束：

- 保存计划从 bundle index / artifact 身份透传实际 `decryptorId + decryptorVersion`，不重新猜测输出 codec。
- 只有当前 registry 中同 id、同 version 的实现明确支持 `IAssetBundleEncryptor` 时才执行回写。
- 声明支持但实现 / 命令缺失、版本不一致或 encrypt 失败时，该 bundle 导出失败；不能静默输出看似成功但游戏不可读的标准 AB。
- 不支持 Encrypt 的旧解密器仍可导出标准 AB，并在 manifest 中不记录输出 codec。
- Encrypt 输入是 writer 的临时标准 AB，输出也是临时文件；成功校验后才替换最终目标。
- `_abt-bundle-export-manifest.json` 应记录 `outputCodecId / outputCodecVersion`，让外部工具区分“标准 AB”与“已回写游戏格式 AB”。

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
- 当前解密器管理区已准备从“设置与诊断”迁移到侧边栏一级“插件管理”页；该页负责新增、编辑、删除、刷新和诊断，设置页后续只保留缓存、日志和偏好类诊断功能。页面职责见 [plugin-management-page.md](plugin-management-page.md)
- 工作台顶部状态标签已显示“当前解密器”，替代原先的冗余“当前选中”提示
- 工作区导入链路已正式按 `AssetBundleDecryptorId` 选择解密器执行，并把解密后文件送入 `IAssetBundleReader`
- 当前真实导入结果已写入 `cache/bundle-index/...`，再由缓存型工作区会话驱动资源包列表恢复
- Infrastructure 当前已接入 `AssetsTools.NET` 真实 Reader，并在读取失败时回退到启发式 Reader，避免真实导入阶段把资源包列表回退成不可用状态
- 当前 Reader 已可从解密后文件中读取序列化资产、资产名和 `m_Container` 路径，供工作台恢复资产列表与基础详情摘要
- 资源包索引缓存当前已持久化 `decryptorId / decryptorVersion`，并参与导入刷新时的缓存复用判定
- 当前导入刷新已按 `decryptorId + decryptorVersion + sourceFingerprint + parserVersion + toolVersion` 执行命中复用与失效重建
- 当前已落地 `cache/bundles` 首版标准化资源包物理缓存：导入、预览、提取和 Spine 详情通过 Application artifact resolver 优先取得同一可读 bundle 路径；同一 `workspaceKey + workspaceDataRootPath + bundleCacheKey` 的 artifact 生成阶段会 single-flight 串行化，外部解密器派生输出和 PassThrough 原始可读包都会晋升为 `normalized.bundle`，manifest 不再继续指向用户源文件
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

本轮已完成：

- 内置只读 `builtin.arknights-lz4ak` CLI，提供真实 UnityFS Probe、LZ4AK 解密和标准 AB 逆向回写；应用构建时按目标平台发布为经裁剪和压缩的自包含单文件并放入独立 `codecs/arknights` 目录，不能与 WinUI 自包含运行时共用 framework-dependent apphost。
- 内置只读 `builtin.crosscore-duplicated-header` CLI，按 duplicated-header v1 的双 UnityFS / 83 字节重复前缀证据自动识别，提供流式解密和标准 AB 前缀回写；按目标平台发布到独立 `codecs/crosscore` 目录。真实样本已覆盖 Probe、AssetsTools 读回，以及 `Decrypt -> Encrypt` 后原文件 SHA-256 完全一致。
- Probe 以 UnityFS header、blocks-info 和数据 block 的压缩类型 / 实际解码结果为证据，不依赖文件名。
- 外部进程 manifest 增加可选 `encryptArguments`，Application 增加窄 `IAssetBundleEncryptor` 端口。
- SaveRebuild 在标准 writer 成功后按 artifact 的来源 decryptor 自动执行可选回写，并把 codec 身份写入导出结果与 manifest。

当前明确暂不实现：

- 自动识别结果的完整 UI 诊断与冲突决策流程
- 把解密结果作为不可重建的长期明文副本管理；当前只实现可失效、可清理、可重建的 `cache/bundles` 标准化 artifact
- 更通用的插件安装、签名与市场机制

## 8. 下一步建议

建议按下面顺序继续推进：

1. 为自动识别冲突补用户可见诊断与显式决策入口；当前仍按命中数、置信度和稳定次序选择。
2. 为有密钥或多 profile 的 codec 评估受控参数 / profile 持久化，禁止把密钥直接写入 artifact manifest。
3. 基于当前 usage / LRU prune 和设置页显式清理入口，为 `cache/bundles` 补齐更细清理诊断。
4. 最后再评估是否需要更通用的插件安装、签名和权限隔离机制。
