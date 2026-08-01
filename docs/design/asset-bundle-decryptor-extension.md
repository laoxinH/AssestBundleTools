# AssestBundleTools AssetBundle codec 扩展契约

## 1. 文档职责

本文约束 AB 导入前的识别/标准化，以及导出后的来源格式回写。外部插件 manifest、参数占位符和 result JSON 的唯一对外定义见[插件开发与安装指南](../plugin-guide.md)。

配套规范：

- [代码架构规范](code-architecture-guidelines.md)
- [工作区管理](workspace-management.md)
- [导出与保存重建](workspace-export-save-pipeline.md)
- [插件管理页](plugin-management-page.md)

## 2. 核心原则

### Codec 与 Reader 分离

`IAssetBundleReader` 只读取标准、可解析的 Unity 资产。游戏前缀、加密或压缩变体由 codec 在 Reader 前处理，不允许把游戏专用逻辑塞进 Reader 或 ViewModel。

### 标准化结果属于缓存

Decrypt 输出进入工作区 `cache/bundles` 标准化 artifact。它可失效、可清理、可重建，不是工作区元数据事实，也不能覆盖用户源文件。

### Encrypt 是可选回写能力

Writer 始终先生成标准 AB。只有来源 codec 明确支持 Encrypt、版本匹配且执行成功时，才把标准 AB 回写为游戏格式。失败必须让所属 bundle 导出失败，不能静默输出错误格式。

### 按 AB 保存实际 codec

工作区只保存用户选择的策略，例如“自动识别”。每个 bundle index/artifact 独立保存实际命中的 codec id、版本和受控参数，供缓存失效和原样导出使用。

## 3. 分层职责

### Application

- 定义 codec descriptor、Probe/Decrypt/Encrypt 请求和结果。
- 选择自动识别、指定 codec 或直通策略。
- 管理逐 bundle artifact 身份和导出回写请求。
- 返回稳定错误和诊断语义，不依赖进程、JSON 或文件选择器。

### Infrastructure

- 提供直通、外部进程和内置 codec 适配器。
- 解析 manifest、执行 CLI、校验 result 和输出路径。
- 保存标准化 artifact、codec 元数据和缓存指纹。
- 实现 Writer 后的 Encrypt 与原子输出提升。

### Presentation

- 在工作区向导、插件管理和诊断中心展示策略与状态。
- 本地化结构化错误，原始 stdout/stderr 只作为诊断 detail。
- 不直接 Probe、Decrypt、Encrypt 或读取 manifest。

## 4. 导入识别流程

### 自动识别

对每个候选文件独立执行：

1. 先做低成本常见非 AB 文件筛除，但扩展名不能作为唯一依据。
2. 依稳定顺序调用支持自动识别的 codec Probe。
3. 只接受有结构证据的命中；文件名和扩展名不能产生高置信度。
4. 选择命中的 codec，执行 Decrypt 并把输出交给 Unity Reader 验证。
5. 没有 codec 命中时，回退到直通并由 Reader 验证标准 Unity 资产。
6. 所有路径都不匹配时排除文件，不创建 bundle index。

自动模式在工作区重开后仍保持自动策略；实际 codec 记录在各 bundle artifact，不把第一个命中项固化给整个工作区。

### 指定 codec

指定模式只使用所选 codec 作为识别和解密手段。Probe 不匹配、Decrypt 失败或 Reader 拒绝时排除/报错，不尝试其他 codec 或直通。

### 稳定识别缓存

识别缓存至少绑定来源指纹、codec id/version、parser/tool 版本和标准化 artifact 身份。瞬时 Probe/进程失败不能缓存为永久“不匹配”。来源变化、codec 变化或 artifact 失效时必须重新识别。

## 5. Artifact 与参数

标准化 artifact manifest 保存：

- 实际 codec id 和版本。
- 来源指纹和 bundle cache key。
- 生成器/parser 版本。
- codec 返回的有界非敏感参数，例如 CrossCore 原始外层前缀。

禁止把密钥、令牌或无界外部输出写进 artifact manifest。需要秘密信息的 codec 应使用用户受控 profile 或系统凭据存储，不得依赖导出清单泄露参数。

## 6. 导出回写

勾选“按照原加密导出”时，每个 AB 独立执行：

1. 从当前 bundle artifact 读取来源 codec id、版本和参数。
2. Writer 按选择的压缩策略生成标准 AB 临时文件。
3. Registry 按 id 解析当前 codec，并校验版本与 Encrypt 能力。
4. Encrypt 在 operation 级短路径工作目录运行。
5. 校验输出存在且位于允许目录后，原子提升为最终文件。
6. 导出 manifest 在该 bundle 项记录实际 `outputCodecId / outputCodecVersion`。

未记录来源 codec、直通 codec 或不支持 Encrypt 的 codec 输出标准 AB，`outputCodecId` 为空。参数用于实际 Encrypt，但当前导出 manifest 不公开 codec 参数。

## 7. 内置 codec

### Arknights LZ4AK

- 以 UnityFS 结构、blocks-info 和数据块实际解码为 Probe 证据。
- Decrypt 生成标准 UnityFS；Encrypt 生成游戏要求的 LZ4AK 变体。
- 导出压缩选项描述标准 AB writer 的输入格式；来源 codec 回写发生在 writer 之后。
- 必须保留合成回环与可用真实样本回环，不能只验证文件头。

### CrossCore prefixed-header

- 持久化 id 保持 `builtin.crosscore-duplicated-header`。
- duplicated-header v1 与 prefixed-header v2 都通过第二个 UnityFS 的结构和声明长度识别，不能要求两段头完全相等。
- Decrypt 从验证后的内层 UnityFS 起始位置输出标准 AB。
- artifact 保存原始外层前缀；Encrypt 必须原样拼回该前缀，使真实样本完成 SHA-256 回环。
- 旧样本与新样本使用同一结构验证路径，不能按文件名选择版本。

内置 CLI 随目标架构发布到独立 codec 目录。它们可以自包含运行时，但不得依赖用户机器另装对应 .NET runtime。

## 8. 进程与路径约束

- 插件进程使用 operation 级隔离工作目录，避免多 bundle 并发冲突。
- 调用方传入的工作目录已经是短路径，适配器不得重复追加长 workspace/codec/bundle 标识。
- result 声明的输出只能等于调用方目标或位于本次隔离目录。
- 超时、取消、非零退出码、result 缺失、输出缺失和版本不匹配都是结构化失败。
- stdout/stderr 不作为成功语义，也不直接作为用户主提示。

## 9. 测试门禁

- 自动模式覆盖同一工作区内不同 codec 与标准 Unity AB 混合导入。
- 指定 codec 覆盖不匹配时不回退。
- 未变化来源命中识别/artifact 缓存，来源或 codec 变化触发失效。
- 外部进程覆盖 Probe、Decrypt、可选 Encrypt、取消、超时和输出越界。
- 每个内置 codec 至少有合成确定性回环和可公开真实样本回环。
- Writer -> Encrypt -> Decrypt -> Reader 覆盖原压缩和显式压缩选择。
- 导出 manifest 按 bundle 记录实际输出 codec 身份。

## 10. 非目标

- 在 Reader 内猜测游戏格式。
- 把解密明文当作不可清理的长期事实。
- 在工作区元数据中存储密钥。
- 通过扩展名直接认定 AB。
- 在 codec 文档中维护第二套插件 manifest/CLI 协议。
