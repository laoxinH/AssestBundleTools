# AssestBundleTools 插件管理页设计

## 1. 文档目的

本文定义主窗口侧边栏一级页面“插件管理”的职责、信息架构、界面草图，以及它与工作区导入解密器和工作区导出资源处理器的整合边界。

当前这里的“插件”只表示本工具可发现、可配置、可执行的 CLI 扩展点，不表示完整插件市场、在线安装器或脚本运行时托管环境。第一阶段目标是把已经落地的导入解密 CLI 管理从“设置与诊断”迁移出来，并为后续导出时的 `Texture2D / Audio / Atlas / Skel` 等资源处理 CLI 插件预留统一目录、配置和选择模型。

## 2. 范围

### 2.1 纳入范围

- 导入解密插件：现有 `IAssetBundleDecryptor / IAssetBundleDecryptorRegistry`、外部进程解密器 manifest、用户可管理解密器目录项。
- 导出资源处理插件：保存为新包 / 工作区导出时按资源类型选择的 CLI 预处理器。
- 插件发现、列表、搜索、按类型筛选、查看详情、新增、编辑、删除、刷新、复制诊断信息。
- 插件可用性诊断：manifest 是否可读、必填字段是否完整、可执行文件是否可解析、是否只读、是否支持自动识别或指定资源类型。
- 与新建工作区、打开工作区和工作区导出的选择逻辑衔接。

### 2.2 不纳入范围

- 在线插件市场、自动下载、签名校验、权限沙箱。
- 主程序内嵌 Python / Node / Lua 等运行时。
- Presentation 直接执行插件进程或自行解析资源包。
- 真实导出写包流程的最终落地；本页只定义资源处理器目录和导出窗口消费方式。

## 3. 信息架构

侧边栏一级导航新增“插件管理”，与“工作台”和“设置与诊断”并列。旧批处理页已移除，不再占用一级导航。

推荐导航顺序：

1. 控制台 / 工作台
2. 插件管理
3. 设置与诊断

页面采用“左侧插件目录 + 右侧详情/配置”的工作型布局，而不是设置页式长表单。插件数量可能增长，目录需要支持搜索、类型筛选和状态筛选；详情区只展示当前选中插件，编辑入口使用对话框或右侧内联编辑面板。

插件类型分为两类：

- `导入解密`：作用于 AB 导入前的源文件转换，输出标准化 bundle artifact。
- `导出处理`：作用于工作区导出 / 保存为新包前的资源载荷预处理，按资源类型分槽选择。

导出处理器第一阶段内置资源类型槽：

- `Texture2D`
- `Audio`
- `Atlas`
- `Skel`

后续新增 `Mesh / Material / TextAsset` 等槽时，只扩展资源处理器类型目录，不改变页面结构。

## 4. 页面草图

### 4.1 宽屏布局

```text
--------------------------------------------------------------------------------+
| 插件管理                                                                       |
| 管理导入解密插件和导出资源处理插件。                                           |
|                                                                                |
| [搜索插件或命令路径___________________] [全部类型 v] [全部状态 v]      [刷新] |
|                                                                                |
| +--------------------------------------+ +-----------------------------------+ |
| | 插件目录                             | | 纹理 PNG 预处理器                | |
| | [新增插件 v] [打开插件目录]          | | 导出处理 / Texture2D        可用 | |
| |                                      | | id: export.texture.png-tool       | |
| | 导入解密                             | | version 1.2.0                     | |
| |  ┌ 自动识别策略              内置 ┐  | | manifest: ...\processors\png.json | |
| |  └ 直通解密器                内置 ┘  | | executable: tools\png-opt.exe      | |
| |  ┌ Example Game Decryptor     可用 ┐ | |                                   | |
| |  └ 支持自动识别 / 15s              | | [编辑] [验证] [复制诊断] [删除]    | |
| |                                      | |                                   | |
| | 导出处理                             | | 适用范围                          | |
| |  ┌ Texture2D PNG Processor   可用 ┐ | | 资源类型：Texture2D               | |
| |  └ Texture2D / CLI                 | | 导出阶段：应用修改后，写包前       | |
| |  ┌ Audio WAV Normalizer      警告 ┐ | | 默认选择：未设为默认               | |
| |  └ Audio / CLI                     | |                                   | |
| |  ┌ Spine Atlas Formatter     可用 ┐ | | CLI 协议                          | |
| |  └ Atlas / CLI                     | | input/output/result/workspace...   | |
| +--------------------------------------+ |                                   | |
|                                          | 最近诊断                          | |
|                                          | 未执行验证。                      | |
|                                          +-----------------------------------+ |
+--------------------------------------------------------------------------------+
```

设计要点：

- 顶部搜索和筛选是页面级工具条，使用 WinUI `TextBox / ComboBox / Button`，不做营销式 hero。
- 左侧目录按插件类型轻量分组，组标题使用小号二级文本和细分隔线，不使用大面积嵌套卡片。
- 每个插件行保持紧凑：名称、类型/适用资源、副标题、状态徽标。行选中后右侧显示详情。
- 右侧详情区不是重复列表卡片，而是当前插件的事实面板、操作区和诊断区。
- 只读内置项隐藏或禁用“删除”，编辑按钮进入只读详情或复制为用户插件。

### 4.2 窄宽布局

```text
插件管理
[搜索插件________________] [类型 v]

[插件目录]
  自动识别策略     导入解密 / 内置
  Example Game     导入解密 / 可用
  PNG Processor    Texture2D / 可用

--------------------------------
PNG Processor
Texture2D / CLI / 可用
[编辑] [验证] [复制诊断]

manifest ...
executable ...
最近诊断 ...
```

窄宽时目录和详情上下堆叠，顶部筛选换行；主操作仍在当前插件标题下方可见。

## 5. 页面功能

### 5.1 插件目录

目录必须支持：

- 搜索：匹配插件名称、id、描述、manifest 路径、可执行路径。
- 类型筛选：全部、导入解密、导出处理、导出处理下的具体资源类型。
- 状态筛选：全部、可用、警告、不可用、只读、用户管理。
- 刷新：重新读取 catalog 并触发对应 registry reload。
- 新增插件：选择插件类型后打开对应编辑器。
- 打开插件目录：打开用户插件根目录，便于复制 manifest 和工具文件。

目录项状态规则：

- `内置`：主程序提供，不能删除，通常不可编辑。
- `只读`：来自应用目录或受保护 manifest，可查看和复制，但不能直接保存。
- `用户管理`：来自用户插件目录，可编辑和删除。
- `可用`：manifest 解析通过，必填字段完整，路径可解析。
- `警告`：manifest 可读但存在非阻断问题，例如可执行文件尚未验证、probe 参数缺失但声明支持自动识别。
- `不可用`：manifest 缺失必填字段、命令路径无法解析或类型声明无法识别。

### 5.2 导入解密插件详情

详情字段沿用现有解密器目录能力：

- id、显示名称、版本、描述。
- manifest 路径、可执行路径、工作目录。
- timeout、probe 参数、decrypt 参数。
- 是否支持自动识别、是否 fallback、是否只读。
- 最近一次验证诊断。

操作：

- 新增 / 编辑：复用现有 `AssetBundleDecryptorEditorDialogContent` 的字段模型，但迁移到插件管理页命名空间或共享插件编辑器目录。
- 删除：只允许用户管理项，删除后刷新解密器 registry。
- 验证：第一阶段可只做本地 manifest / 路径 / 参数占位符检查；后续再补对样本文件的 probe。
- 复制诊断：复制结构化摘要，便于用户排查 CLI 配置。

### 5.3 导出资源处理插件详情

导出处理插件字段建议如下：

- id、显示名称、版本、描述。
- `resourceKinds`：支持的资源类型槽，如 `Texture2D`、`Audio`、`Atlas`、`Skel`。
- manifest 路径、可执行路径、工作目录、timeout。
- process 参数。
- 输入 / 输出格式声明，例如 `png -> png`、`wav -> wav`、`atlas -> atlas`。
- 是否可作为默认处理器。
- 最近一次验证诊断。

第一阶段 manifest 建议：

```json
{
  "id": "export.texture.png-processor",
  "displayName": "Texture PNG Processor",
  "version": "1.0.0",
  "description": "Processes Texture2D PNG payloads before bundle export.",
  "kind": "resourceProcessor",
  "resourceKinds": [ "Texture2D" ],
  "executablePath": "tools/png-processor.exe",
  "workingDirectoryPath": ".",
  "timeoutMs": 15000,
  "processArguments": [
    "--input", "{inputPath}",
    "--output", "{outputPath}",
    "--result", "{resultPath}",
    "--resource-kind", "{resourceKind}",
    "--workspace", "{workspaceDataRootPath}"
  ]
}
```

推荐占位符：

- `{inputPath}`
- `{outputPath}`
- `{resultPath}`
- `{resourceKind}`
- `{assetName}`
- `{bundleName}`
- `{workspaceKey}`
- `{workspaceDataRootPath}`
- `{manifestDirectory}`

插件执行结果继续通过 result JSON 返回，避免主程序解析 stdout / stderr 作为主要语义。

### 5.4 默认选择

插件管理页只管理全局目录和可选默认值，不直接修改某个工作区的既有事实。

- 导入解密默认值仍属于工作区创建 / 打开流程。新建工作区向导继续提供解密器选择，下拉项来自插件管理页维护的解密器 registry。
- 打开已有工作区时继续只读展示工作区元数据中的解密器配置；如果对应插件已删除或不可用，应显示缺失诊断，但不静默改写工作区元数据。
- 导出资源处理器默认值属于应用偏好或导出窗口临时选择。导出窗口按资源类型槽读取已注册处理器，并默认选中“无处理器”或用户偏好中的默认项。

## 6. 与导入流程的整合

现有导入链路保持不变：

```text
工作区选择解密器
  -> 导入来源登记
  -> Application 解析工作区解密器策略
  -> IAssetBundleDecryptorRegistry 解析真实解密器
  -> Infrastructure 执行外部进程或直通
  -> cache/bundles 标准化 artifact
  -> Reader / Index / Preview
```

迁移后职责变化：

- 插件管理页负责解密器目录管理、编辑、删除、刷新和诊断。
- 新建工作区向导只负责选择当前工作区使用的解密策略，不再承载管理入口。
- 设置与诊断页不再承载解密器管理区，只保留缓存、日志、偏好、活动工作区摘要等诊断功能。
- 目录变更后必须调用已有 registry reload 机制，确保导入链路读取最新插件定义。

## 7. 与导出流程的整合

工作区导出窗口的资源包范围选择继续保持在导出窗口中；资源处理插件选择继续按资源类型槽展示，但数据来源改为插件管理页维护的导出处理器 catalog。

导出窗口中的每一行资源类型槽应遵循：

- 始终提供“无处理器”。
- 只列出支持该资源类型的可用导出处理插件。
- 不在导出窗口新增、编辑、删除插件；只提供跳转到插件管理页的轻量入口。
- 插件不可用时不进入可选列表；如果用户偏好指向不可用插件，回退“无处理器”并给出非阻断提示。

导出执行第一阶段计划：

```text
WorkspaceExportDialogViewModel 选择资源包与每类处理器
  -> Save / Export command 携带 ProcessorSelection[]
  -> Application 构建保存计划并声明需要处理的资源载荷
  -> Infrastructure 对每个资源类型调用对应 CLI 处理器
  -> result JSON 回填成功 / 失败 / 输出路径
  -> 保存写包继续消费处理后的载荷
```

资源处理器的失败语义必须结构化：

- 单个资源处理失败应归类为导出失败项，不应表现为主程序未预期异常。
- CLI 原始 stdout / stderr 只作为诊断明细，不作为主要用户提示。
- 取消导出时需要取消尚未启动的处理任务，并尽力终止当前外部进程。

## 8. 架构边界

### 8.1 Application

Application 应定义稳定目录和执行契约：

- 现有解密器 catalog / registry 继续保留。
- 新增导出资源处理器目录 DTO、查询、保存、删除和执行端口。
- 保存 / 导出命令接收资源处理器选择 DTO，而不是 Presentation ViewModel。
- 返回结构化诊断和 `Result / AppError`。

### 8.2 Infrastructure

Infrastructure 负责：

- 读取插件 manifest。
- 管理用户插件目录。
- 执行外部进程。
- 解析 result JSON。
- 捕获 stdout / stderr 作为诊断明细。

### 8.3 Presentation

Presentation 负责：

- 插件管理页展示和编辑。
- 调用 Application 服务刷新、保存、删除。
- 将插件状态、诊断和错误资源化展示。
- 在工作区向导和导出窗口中提供选择 UI。

Presentation 不得：

- 直接运行 CLI。
- 直接读写插件 manifest JSON。
- 绕过 Application 修改工作区元数据或导出计划。

## 9. 迁移步骤

建议按以下顺序落地：

1. 新增 `PluginManagementPage` 一级导航、页面 ViewModel 和资源文本。
2. 将设置页中的解密器目录 UI、编辑对话框和 presenter 迁移或抽成 `ViewModels/PluginManagement` 与 `Views/PluginManagement` 所有。
3. 设置与诊断页移除解密器管理区，只保留诊断与偏好。
4. 新增导出资源处理器 catalog 的 Application DTO / 查询骨架，先返回内置“无处理器”和占位 CLI 项。
5. 工作区导出窗口改为从 catalog 按资源类型加载处理器，而不是在 ViewModel 中硬编码静态选项。
6. 补齐导出处理器 manifest 存储、编辑器和验证。
7. 后续真实写包阶段接入处理器执行链路。

## 10. 测试建议

- Shell 导航契约：插件管理页作为一级导航存在，旧批处理页不恢复。
- 设置页契约：不再包含解密器目录管理区。
- 插件目录 ViewModel：搜索、类型筛选、状态筛选、只读项禁用删除。
- 解密器迁移回归：新增 / 编辑 / 删除后 registry reload，工作区向导能看到最新解密器。
- 导出窗口回归：每个资源类型槽只显示匹配资源类型的处理器，并始终包含“无处理器”。
- 本地化 guard：插件管理页所有用户可见文本必须进入资源文件。

## 11. 当前实现状态

已完成：

- Shell 新增“插件管理”一级页面，页面采用“目录 + 详情/诊断”的两栏工作型布局。
- 设置与诊断页已移除解密器管理区；解密器新增、编辑、删除和刷新迁移到插件管理页。
- 插件管理页支持搜索、类型筛选、状态筛选、打开插件位置、验证占位和复制诊断。
- Application 新增导出资源处理器 catalog DTO / 查询骨架，首阶段提供 `Texture2D / Audio / Atlas / Skel` 的只读 CLI 占位目录项。
- 工作区导出窗口已改为从资源处理器 catalog 按资源类型槽加载可用处理器，并保留“无处理器”选项。
- 已补 Shell / 设置页迁移 / 导出窗口 catalog 消费 / Application catalog 查询的回归测试与本地化资源。

仍待后续阶段：

- 资源处理器 manifest 的真实文件系统存储、编辑器、删除和更完整验证。
- 导出处理器默认值偏好与导出窗口跳转插件管理页入口。
- 保存 / 导出真实写包链路中的资源处理器选择 DTO、CLI 执行、result JSON 解析和失败回填。
