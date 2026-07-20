# AssestBundleTools 插件开发与安装指南

本文面向两类读者：

- 插件使用者：安装别人提供的插件，并在导入或导出时选择它。
- 插件开发者：实现与 AssestBundleTools 当前协议兼容的外部 CLI。

AssestBundleTools 当前支持三类外部插件。它们都是独立进程，不会被加载到主程序进程中，也没有专用语言限制；C#、C/C++、Rust、Go、Python 等均可实现，只要最终能够按本文约定接收命令行参数和读写文件。

> 插件拥有与当前用户相同的文件访问权限。当前版本不提供插件沙箱、签名验证或在线市场，请只安装可信来源的插件，并在处理游戏文件前保留备份。

## 1. 三类插件的区别

| 类型 | manifest `kind` | 执行时机 | 执行粒度 | 主程序是否等待 | 结果契约 |
| --- | --- | --- | --- | --- | --- |
| AssetBundle codec | 无 `kind` 字段 | AB 导入前；可选在 AB 导出后回写游戏格式 | 每个 AB 单独 Probe / Decrypt / Encrypt | 是 | result JSON + 输出文件 + 退出码 0 |
| 导出资源处理器 | `resourceProcessor` | 修改内容写回 AB 前 | 每个符合条件的资产单独执行 | 是 | result JSON + 可选输出文件 + 退出码 0 |
| 导出后插件 | `postExport` | AB 与 `_abt-bundle-export-manifest.json` 写出后 | 每次导出最多执行一次 | 否 | 仅要求进程成功启动 |

选择类型时可以按下面的原则判断：

- 游戏 AB 有专用加密、前缀或压缩变体：开发 AssetBundle codec。
- 写回 AB 前需要把 PNG、音频或 Spine 文件转换成 writer 可消费的载荷：开发导出资源处理器。
- 需要根据整批导出结果制作 ZIP、安装包、模组目录或启动自己的 UI：开发导出后插件。

## 2. 用户安装与移除

### 2.1 推荐安装目录

用户管理的三类插件分别从以下目录发现：

```text
%LocalAppData%\AssestBundleTools\decryptors\
%LocalAppData%\AssestBundleTools\resource-processors\
%LocalAppData%\AssestBundleTools\post-export-plugins\
```

应用安装目录下也有对应的只读发现位置：

```text
<app-directory>\decryptors\
<app-directory>\resource-processors\
<app-directory>\post-export-plugins\
```

普通用户应优先使用 `%LocalAppData%`。应用目录适合随正式发行包部署内置插件，升级或重新安装时其中的手工文件可能被替换。

### 2.2 安装插件包

当前版本没有 ZIP 插件包的一键安装器。插件作者应提供一个压缩包，用户把压缩包内容直接解压到对应发现目录，然后在“插件管理”页面点击刷新。

推荐的分发结构如下：

```text
game.example.codec.json
game.example.codec\
  game-example-codec.exe
  required-library.dll
  LICENSE.txt
  README.txt
```

安装到 codec 目录后的结构应为：

```text
%LocalAppData%\AssestBundleTools\decryptors\game.example.codec.json
%LocalAppData%\AssestBundleTools\decryptors\game.example.codec\game-example-codec.exe
```

注意：应用只扫描发现目录第一层的 `*.json`。manifest 可以引用子目录中的程序，但不能把 manifest 本身再套一层目录，否则插件不会出现。

### 2.3 通过界面注册

也可以先把可执行文件放到稳定目录，再在应用中注册：

1. 打开“插件管理”。
2. 点击插件目录右上角的新增按钮。
3. 选择“导入解密器”“导出资源处理器”或“导出后插件”。
4. 填写插件 ID、名称、可执行路径和参数。
5. 参数编辑框每行表示一个独立命令行参数，不要把整条命令写在一行。
6. 保存后点击刷新，检查状态和详情中的 manifest 路径。

界面只保存 manifest，不会复制可执行文件或运行时依赖。相对路径必须能够从 manifest 所在目录解析到真实文件。

### 2.4 使用插件

- AssetBundle codec：新建工作区时选择“自动识别”，或明确选择指定 codec。自动模式会对每个候选文件分别 Probe。
- 导出资源处理器：在“保存为新包”窗口中，为 `Texture2D / Audio / Atlas / Skel` 对应槽选择处理器。
- 导出后插件：在同一导出窗口的压缩策略下方选择。默认值为“无导出后插件”。

资源处理器和导出后插件的选择会随当前工作区导出设置保存。插件被移除或不可用时会回退到“无插件”，不会静默改用其他插件。

### 2.5 移除与排错

用户管理项可以在插件管理页删除；应用目录中的只读项只能由发行包维护者移除。手工安装的插件也可以删除对应 manifest 和工具目录，然后点击刷新。

插件没有出现时依次检查：

1. manifest 是否直接位于正确发现目录的第一层。
2. JSON 是否有效，必填字段是否为空。
3. `id` 是否与其他插件重复。用户目录中的同 ID 外部项会优先于应用目录项，但不能覆盖内置只读项。
4. 可执行路径是否存在，当前 Windows 架构是否能够运行该程序。
5. 参数是否保持“每个数组元素一个参数”，路径参数是否被错误地手工加了引号。
6. 查看“设置与诊断 -> 日志中心”中的 Probe、进程启动、结果文件和写包诊断。

## 3. 通用开发约定

### 3.1 ID、版本与兼容性

- `id` 是持久化身份，发布后不要因显示名称变化而修改。推荐使用 `vendor.game.feature` 形式，例如 `acme.example.codec`。
- `version` 推荐使用 SemVer，例如 `1.2.0`。
- manifest 和 result JSON 使用 UTF-8；字段名示例采用 camelCase。
- 消费主程序生成的 JSON 时必须忽略未知字段，以便后续 schema 增加信息。
- 不要在 manifest 中写开发机绝对路径。将程序和依赖放在插件目录内，使用相对路径分发。

### 3.2 参数数组

所有参数数组都会通过 `ProcessStartInfo.ArgumentList` 逐项传递，不经过 shell 拼接。因此：

```json
{
  "processArguments": [
    "--input",
    "{inputPath}",
    "--mode",
    "fast"
  ]
}
```

不要写成：

```json
{
  "processArguments": [
    "--input \"{inputPath}\" --mode fast"
  ]
}
```

即使路径中包含空格，也不要给单独的路径参数额外包一层引号；参数列表会保留它的完整边界。

### 3.3 并发和临时文件

导入和导出都可能并发处理多个 AB 或资产。插件不得使用当前目录下固定名称的共享临时文件。优先直接使用主程序提供的 `{outputPath}` 和 `{resultPath}`；确需其他临时文件时，应放在它们所在的隔离目录内并使用唯一名称。

除非工具确实要求固定工作目录，否则建议不设置 `workingDirectoryPath`。固定工作目录更容易造成并发冲突，也可能放大 Windows 长路径问题。

### 3.4 退出码与日志

- codec 和资源处理器只有在进程退出码为 `0` 时才读取 result JSON。
- result JSON 是主要机器协议；stdout / stderr 只用于诊断，不要要求主程序从控制台文本判断成功。
- 错误消息应简短，不要输出密钥、用户隐私或完整二进制数据。
- 导出后插件没有 result JSON 契约，主程序也不会捕获其 stdout / stderr。

## 4. AssetBundle Codec

### 4.1 作用与要求

Codec 负责把游戏专用 AB 转换为标准、可由 Unity reader 读取的 bundle；如果声明 Encrypt，还负责把重建后的标准 bundle 精确回写为游戏需要的格式。

一个 codec 最多提供三个命令：

- Probe：识别输入是否属于该格式，供自动模式或指定 codec 的导入筛选使用。
- Decrypt：生成标准 bundle。
- Encrypt：可选，导出时恢复来源加密、前缀或变体。

Probe 必须依据文件内容和结构特征，不能仅凭扩展名或文件名返回高置信度。自动模式会为每个候选 AB 分别运行所有可自动识别的 codec，选择 `isMatch=true` 且置信度最高的一项，最后才回退到标准 Unity 资源识别。

### 4.2 Manifest 示例

```json
{
  "schemaVersion": 1,
  "id": "game.example.codec",
  "displayName": "Example Game Codec",
  "version": "1.0.0",
  "description": "Recognizes, decrypts and re-encrypts Example Game bundles.",
  "supportsAutoDetection": true,
  "isFallback": false,
  "executablePath": ".\\game.example.codec\\game-example-codec.exe",
  "workingDirectoryPath": null,
  "timeoutMs": 15000,
  "probeArguments": [
    "probe",
    "--input", "{inputPath}",
    "--result", "{resultPath}"
  ],
  "decryptArguments": [
    "decrypt",
    "--input", "{inputPath}",
    "--output", "{outputPath}",
    "--result", "{resultPath}"
  ],
  "encryptArguments": [
    "encrypt",
    "--input", "{inputPath}",
    "--output", "{outputPath}",
    "--result", "{resultPath}",
    "--header", "{parameter:game.headerBase64}"
  ]
}
```

字段规则：

- `decryptArguments` 必填。
- `supportsAutoDetection=true` 时应提供 `probeArguments`；通过界面保存时两者必须一致。
- `encryptArguments` 可选；为空表示导出时不能“按照原加密导出”。
- `isFallback` 对普通第三方 codec 应设为 `false`。标准 Unity 直通回退由应用内置实现承担。
- `timeoutMs` 必须大于 `0`，同时作用于 Probe、Decrypt 和 Encrypt。
- codec 的 `executablePath` 可以是绝对路径、相对 manifest 的路径，或 PATH 上的纯命令名。插件目录内的程序建议显式写成 `.\\tool.exe` 或包含子目录的相对路径。

支持的普通占位符：

| 占位符 | 含义 |
| --- | --- |
| `{inputPath}` | Probe/Decrypt 的游戏源文件；Encrypt 的标准重建 bundle |
| `{outputPath}` | Decrypt/Encrypt 应生成的目标文件 |
| `{resultPath}` | 插件必须写入的 result JSON |
| `{workspaceKey}` | 当前工作区持久化键；Probe 时可能为空 |
| `{workspaceDataRootPath}` | 当前工作区数据目录；Probe 时可能为空 |
| `{manifestDirectory}` | 当前插件 manifest 所在目录 |

Encrypt 还支持 `{parameter:key}`。它会展开为 Decrypt 时返回并由工作区 artifact 保存的 `codecParameters[key]`；不存在时展开为空字符串。

### 4.3 Probe 结果

插件应把下面的 JSON 写入 `{resultPath}`：

```json
{
  "schemaVersion": 1,
  "isMatch": true,
  "confidence": 95,
  "formatId": "game.example.bundle-v1",
  "evidence": [
    "header.magic",
    "block-table.valid"
  ],
  "message": "Optional short diagnostic"
}
```

- `confidence` 会被限制在 `0-100`。
- `formatId` 和 `evidence` 应使用稳定、非本地化的标识，方便诊断和缓存分析。
- 不匹配是正常结果：写入 `isMatch=false` 并以退出码 `0` 结束。
- Probe 不应修改输入，也不应生成完整解密文件。

### 4.4 Decrypt 与 Encrypt 结果

两个命令共用 result 结构：

```json
{
  "schemaVersion": 1,
  "isSuccess": true,
  "outputPath": "D:\\workspace\\staging\\output.bundle",
  "message": "Optional short diagnostic",
  "codecParameters": {
    "game.headerBase64": "BASE64_VALUE"
  }
}
```

- `outputPath` 可以省略，此时主程序使用传入的 `{outputPath}`。
- Decrypt 输出必须是可读取的标准 Unity bundle；仅仅复制无法解析的加密输入仍会被 Reader 拒绝。
- 输出必须等于 `{outputPath}`，或位于本次隔离工作目录内。
- `codecParameters` 主要由 Decrypt 返回，用于精确重建来源格式。值必须是字符串。
- Encrypt 成功后应生成游戏真正可识别的最终 AB，而不是只生成普通 Unity LZ4/LZMA 文件。

对于需要原样回写的格式，必须至少验证：

```text
游戏原包 -> Decrypt -> Encrypt -> 与游戏原包逐字节一致
```

如果格式包含允许变化的校验值或时间字段，则必须用结构化测试证明输出仍被游戏读取，并在插件说明中明确差异；不得默默把 v2 格式降级成 v1 或另一种“看起来能解密”的格式。

## 5. 导出资源处理器

### 5.1 作用与当前槽位

资源处理器在 AB writer 运行前处理单个替换载荷。当前稳定资源类型槽为：

- `Texture2D`
- `Audio`
- `Atlas`
- `Skel`

当前第一版主要对存在文件替换载荷、且用户在导出窗口选择了该处理器的资产创建 work item。它不是通用的“遍历整个 AB”插件，也不应该自行修改最终 AB。

### 5.2 Manifest 示例

```json
{
  "id": "game.example.texture-processor",
  "displayName": "Example Texture Processor",
  "version": "1.0.0",
  "description": "Converts replacement images before bundle writing.",
  "kind": "resourceProcessor",
  "resourceKinds": [ "Texture2D" ],
  "executablePath": "game.example.texture-processor\\texture-processor.exe",
  "workingDirectoryPath": null,
  "timeoutMs": 30000,
  "processArguments": [
    "--input", "{inputPath}",
    "--output", "{outputPath}",
    "--result", "{resultPath}",
    "--resource-kind", "{resourceKind}",
    "--asset-name", "{assetName}"
  ]
}
```

字段规则：

- `kind` 必须为 `resourceProcessor`。
- `resourceKinds` 至少包含一个支持的槽位。
- `timeoutMs` 必须大于 `0`。
- `processArguments` 至少包含实际执行需要的参数，推荐始终传递 `{inputPath}`、`{outputPath}` 和 `{resultPath}`。
- 资源处理器的相对 `executablePath` 相对 manifest 目录解析；当前不把纯文件名当作 PATH 命令。推荐分发真实 `.exe`，或填写可移植运行时的明确路径。

支持的占位符：

| 占位符 | 含义 |
| --- | --- |
| `{inputPath}` | 当前资产的原始替换载荷 |
| `{outputPath}` | 默认处理输出路径，通常没有扩展名 |
| `{resultPath}` | result JSON 路径 |
| `{resourceKind}` | `Texture2D / Audio / Atlas / Skel` |
| `{assetName}` | 当前资产名称 |
| `{bundleName}` | 当前目标 AB 显示名称 |
| `{workspaceKey}` | 当前工作区持久化键 |
| `{workspaceDataRootPath}` | 当前工作区数据目录 |
| `{manifestDirectory}` | 插件 manifest 所在目录 |

### 5.3 Result JSON

```json
{
  "schemaVersion": 1,
  "status": "succeeded",
  "message": "Converted to the target payload format.",
  "outputPath": "output",
  "contentType": "image/png",
  "diagnostics": [
    {
      "code": "Texture.Converted",
      "severity": "info",
      "message": "Optional diagnostic detail"
    }
  ]
}
```

`status` 接受：

- `succeeded` 或 `success`：处理成功，输出文件必须存在。
- `skipped` 或 `skip`：不使用处理器输出，writer 继续使用原始有效替换载荷。
- `failed` 或 `failure`：处理失败，当前资源包不会继续写出。

输出规则：

- `outputPath` 省略时使用 `{outputPath}`。
- 成功输出必须位于本次 staging 目录内；返回任意外部路径会被拒绝。
- 如果工具必须添加扩展名，可以生成 `{outputPath}.png` 等文件，并在 result 中返回该文件的绝对路径或相对 staging 的路径。
- `contentType` 当前主要用于结果描述，不能代替真实输出内容。
- 想返回结构化失败时，建议写入 `status=failed` 的 result 并以退出码 `0` 结束；非零退出码表示进程级执行失败。
- 成功退出时写入 stderr 的内容仍会作为 warning 诊断记录。

## 6. 导出后插件

### 6.1 作用与生命周期

导出后插件消费整批 AB 导出清单。适合：

- 把本次成功输出的 AB 压缩为 ZIP。
- 按游戏目录结构复制或部署文件。
- 生成模组描述、校验文件或安装包。
- 启动由插件自己维护的 GUI，让用户继续选择处理方式。

主程序只负责展开参数并启动进程。进程创建成功后立即完成交接：不等待退出、不读取退出码、不捕获 stdout/stderr，也不判断 ZIP、加密或部署是否成功。插件必须自行展示进度、错误和最终结果。

### 6.2 Manifest 示例

```json
{
  "id": "game.example.post-export",
  "displayName": "Example Mod Packager",
  "version": "1.0.0",
  "description": "Packages successfully exported bundles.",
  "kind": "postExport",
  "executablePath": "game.example.post-export\\mod-packager.exe",
  "workingDirectoryPath": null,
  "processArguments": [
    "--manifest", "{manifestPath}",
    "--output-directory", "{outputDirectory}",
    "--workspace", "{workspaceId}"
  ]
}
```

字段规则：

- `kind` 必须为 `postExport`。
- `processArguments` 中至少一个参数必须包含 `{manifestPath}`。
- 可执行文件必须真实存在，插件才会在导出下拉框中显示为可用。
- 相对可执行路径和相对工作目录均相对插件 manifest 解析。
- 未设置 `workingDirectoryPath` 时，进程工作目录是本次 AB 输出目录。
- 当前不支持 PATH 纯命令或 `.cmd/.bat` 直接作为入口；建议分发可直接启动的 `.exe`。

支持的占位符：

| 占位符 | 含义 |
| --- | --- |
| `{manifestPath}` | `_abt-bundle-export-manifest.json` 的绝对路径 |
| `{outputDirectory}` | 本次 AB 输出目录 |
| `{workspaceId}` | 当前工作区 ID |
| `{workspaceKey}` | 当前工作区持久化键，可能为空 |
| `{manifestDirectory}` | 导出清单所在目录，通常等于输出目录 |

未知的 `{name}` 形式占位符会导致启动被拒绝。

### 6.3 导出清单消费

当前清单 `schemaVersion` 为 `2`。插件应先检查版本，并忽略自己不认识的新增字段。简化结构如下：

```json
{
  "schemaVersion": 2,
  "workspaceId": "workspace-id",
  "workspaceKey": "workspace-key",
  "workspaceDisplayName": "Example Workspace",
  "outputDirectory": "D:\\Exports",
  "exportedAtUtc": "2026-07-20T12:00:00+00:00",
  "bundles": [
    {
      "bundleId": "bundle-id",
      "bundleDisplayName": "characters.ab",
      "relativePath": "characters.ab",
      "outputPath": "D:\\Exports\\characters.ab",
      "status": "Succeeded",
      "writtenFileMd5": "...",
      "writtenFileSha256": "...",
      "compressionMode": "PreserveOriginal",
      "writtenCompression": "LZ4",
      "outputCodecId": "game.example.codec",
      "writtenAssets": [
        {
          "assetId": "asset-id",
          "assetName": "hero_texture",
          "assetType": "Texture2D",
          "serializedFileName": "CAB-...",
          "pathId": 123,
          "sourceKind": "file",
          "sourceFileName": "hero.png",
          "processorId": "game.example.texture-processor",
          "processorStatus": "Succeeded"
        }
      ]
    }
  ],
  "processorSelections": []
}
```

消费建议：

- 只处理 `status == "Succeeded"` 的 bundle。
- 同机处理可以使用 `outputPath`；希望清单移动后仍可用时优先结合 `outputDirectory + relativePath`。
- 在处理前可使用 `writtenFileMd5` 或 `writtenFileSha256` 校验文件未被替换。
- `writtenAssets` 只包含实际成功写入该输出 AB 的资产，可用于第三方插件生成变更列表。
- 不要修改 `sourceArtifactPath` 指向的工作区缓存或原始来源。
- 插件自己的结果、日志和 UI 应写到自己控制的位置；主程序不会扫描或解释这些产物。

## 7. 发布插件包

建议每个插件包至少包含：

```text
<plugin-id>.json
<plugin-id>\
  <entry-executable>.exe
  <runtime dependencies>
  README.txt
  LICENSE.txt
  THIRD-PARTY-NOTICES.txt
```

发布说明应写明：

- 插件类型和支持的游戏/格式版本。
- 支持的 Windows 架构以及是否需要额外运行时。
- 是否支持自动识别和原样 Encrypt 回写。
- 资源处理器支持的 `resourceKinds` 和输入/输出格式。
- 导出后插件可能创建、覆盖或上传哪些文件。
- 开源许可证、第三方依赖许可证和下载来源。
- 已验证的 AssestBundleTools 版本与最小测试样本。

不要把密钥、私人服务器地址、开发机绝对路径或受版权限制的游戏资源放入公开插件包。

## 8. 交付前测试清单

### Codec

- 对匹配、不匹配、截断和普通图片等样本验证 Probe，避免误报。
- 验证包含空格、中文和长路径的输入。
- 并发运行至少两个 Decrypt，不共享临时文件。
- 验证 Decrypt 输出能被标准 Unity reader 读取。
- 提供 Encrypt 时验证来源格式元数据完整，并完成原包 round-trip。
- 验证超时、非零退出码、result 缺失和输出缺失时能返回清晰错误。

### 资源处理器

- 对每个声明的 `resourceKind` 使用真实替换载荷测试。
- 验证 `succeeded / skipped / failed` 三种状态。
- 验证输出始终位于 staging，且不会修改输入文件。
- 并发处理多个同名资产时不发生覆盖。
- 用最终导出的 AB 在目标游戏中验证资源确实可读。

### 导出后插件

- 测试清单包含成功、部分成功和失败 bundle 的情况。
- 只处理成功 bundle，并处理路径中的空格、中文和 `#`。
- 主程序启动插件后立即返回，不等待插件退出，因此插件不能依赖父进程继续持有资源。
- GUI 插件自行显示进度和错误；CLI 插件自行保存日志。
- ZIP、部署或二次加密失败时，不要破坏原始导出 AB。

## 9. 进一步参考

- [AssetBundle codec 架构与识别细节](design/asset-bundle-decryptor-extension.md)
- [插件管理页与三类插件边界](design/plugin-management-page.md)
- [真实导出、资源处理器与 AB manifest 契约](design/workspace-export-save-pipeline.md)
- [用户图文使用指南](user-guide.md)
