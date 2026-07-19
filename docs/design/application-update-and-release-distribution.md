# 应用更新与 GitHub Release 发布设计

## 1. 文档目的

本文定义 AssestBundleTools 的应用自更新、GitHub Release 产物矩阵和安装包策略。它覆盖启动检查、关于页手动检查、便携版 / 安装版更新、x86 / x64 / ARM64 隔离、完整性校验、进程退出与失败恢复。

本文件同时作为设计基线与实现状态记录。更新代码和 GitHub Actions 的首版链路已经落地；实现继续遵守分层边界，不在页面 code-behind 中直接拼接 GitHub API、下载文件或替换应用目录。

## 2. 结论

第一版采用以下方案：

- 版本继续使用 `v<SemVer>` Git tag 和 MinVer 生成。
- GitHub Release 是公开发布与更新源；仓库及 Release 必须允许未登录用户读取。
- 更新框架采用 Velopack，不自研文件替换协议。
- Velopack 自带的独立 `Update.exe` 负责退出后的目录替换与重启，不新增维护成本更高的自制 updater。
- 每个架构发布一个便携 ZIP 和一个标准 `Installer.msi`：x86、x64、ARM64 共 6 个用户入口文件。
- MSI 使用 Velopack 标准目录和更新布局，同时提供欢迎、MIT 许可确认、当前用户 / 所有用户安装范围、安装目录和桌面快捷方式选项；不再把无交互的一键 `Setup.exe` 作为公开安装入口。
- 便携版与安装版使用同一更新服务；运行时由 Velopack locator 判断安装模式，不根据路径或注册表做猜测。
- 应用每次正常启动都调度一次后台更新检查；关于页提供显式“检查更新”入口。
- 启动检查失败保持静默并写诊断；手动检查失败给出明确反馈。
- 下载与替换必须由用户确认。更新不得在未完成的工作区操作、编辑草稿保存或导出任务中强制结束进程。

MSI 的当前用户模式默认安装到 `%LocalAppData%`，不需要管理员权限；所有用户模式默认安装到 `Program Files` 并由 Windows 请求提升权限。这里的“无需证书”指用户不需要自行安装证书或准备开发者证书；未签名 MSI 和 EXE 仍可能触发 SmartScreen、杀毒软件或组织策略警告，无法承诺所有 Windows 环境零拦截。发布者代码签名属于后续强烈建议项，不应被描述为用户侧安装前提。

### 2.1 当前实现状态

截至 2026-07-19，以下链路已落地：

- Application 更新 DTO、安装模式 / 架构模型、更新网关和稳定错误语义；
- Infrastructure 的 Velopack 1.2.0 `GithubSource` 适配，包含检查、下载、取消、校验、应用与重启；
- 同架构跨进程检查信号量和 `%LocalAppData%/AssestBundleTools/updates` 短期检查结果缓存；缓存命中只复用展示结果，下载前会重新水化 Velopack `UpdateInfo`；
- WinUI 自定义 `Main` 的最前置 `VelopackApp.Run()`、Shell 首帧后的启动检查，以及关于页手动检查、进度、取消和待重启状态；
- 应用更新前的活动后台任务与其他进程阻断，避免直接结束仍在执行工作的进程；
- `eng/release/Publish-Architecture.ps1` 的 x86 / x64 / ARM64 publish、PE 架构校验、Velopack 打包与 SHA-256 清单；
- `.github/workflows/release.yml` 的测试分支 / 手动构建，以及 `v*` tag 草稿上传、资产名称 / API digest（缺失时下载回读 SHA-256）二次校验和自动公开流程。

本地 x64 已完成旧版 Setup、Portable、full nupkg、feed 和 checksum 的真实打包验证。2026-07-19 发布的首个稳定版本 `v0.1.0` 已通过 GitHub Actions x86 / x64 / ARM64 矩阵、22 项 Release 资产名称与 GitHub SHA-256 digest 校验并公开；该版本仍使用旧的一键 Setup。MSI 向导从后续版本开始成为正式安装入口，其本地和 CI 验收状态必须在完成后追加记录。installed / portable 从旧版本更新到新版本仍必须在形成下一正式版本后完成真实升级验收，未完成前不对外宣称该门禁已经通过。代码签名、preview 独立通道、增量包复用和官网 feed 镜像仍属于阶段 D。

## 3. 目标与非目标

### 3.1 目标

- 一个 tag 自动构建三个架构。
- 每个架构同时提供免安装和安装版本。
- 已安装用户与便携用户都能在应用内完成检查、下载、应用和重启。
- 架构、更新通道和安装模式不可串包。
- 更新失败不损坏当前可运行版本，不删除工作区、缓存、插件或用户设置。
- 发布与更新版本只依赖同一 SemVer，不再维护独立 UI 版本。

### 3.2 非目标

- 第一版不实现后台静默强制更新。
- 第一版不实现企业集中管理、组策略或按机器静默部署。
- 第一版不支持从任意第三方镜像执行更新包。
- 第一版不允许客户端内置 GitHub Token；公开更新必须依赖公开 Release。
- 第一版不自动切换 stable / preview 通道，也不允许跨架构更新。
- 第一版不承诺启动失败后的自动降级回滚；更新替换失败必须保留旧版本，应用启动失败的恢复先提供上一版本下载入口和诊断。

## 4. 版本、架构与通道

### 4.1 版本来源

- 正式 tag：`v1.2.3`。
- 预发布 tag：`v1.2.3-beta.1`。
- Release、程序集 `InformationalVersion`、Velopack `packVersion` 和更新比较统一使用去掉前导 `v` 的 SemVer。
- GitHub Actions 必须使用完整 tag 历史并校验当前提交正好带目标 tag。
- 不带 tag 的 `preview.0.<height>` 开发构建不得上传到 stable 更新通道。

### 4.2 架构映射

| 应用平台 | RID | Velopack channel | pack id |
| --- | --- | --- | --- |
| x86 | `win-x86` | `win-x86` | `AssestBundleTools.win-x86` |
| x64 | `win-x64` | `win-x64` | `AssestBundleTools.win-x64` |
| ARM64 | `win-arm64` | `win-arm64` | `AssestBundleTools.win-arm64` |

三个架构使用不同 channel 和 pack id，避免同版本资产重名、错误选择其他架构包，以及同一电脑并存测试时覆盖彼此。UI 产品名保持 `AssestBundleTools`。

stable 客户端只读取非 draft、非 prerelease Release。预发布通道后续如需开放，应新增 `preview-win-*` channel 和显式用户偏好，不复用 stable feed。

## 5. GitHub Release 产物

每个正式 Release 至少包含：

### 5.1 用户入口文件

| 架构 | 便携版 | 安装版 |
| --- | --- | --- |
| x86 | `AssestBundleTools-<version>-win-x86-Portable.zip` | `AssestBundleTools-<version>-win-x86-Installer.msi` |
| x64 | `AssestBundleTools-<version>-win-x64-Portable.zip` | `AssestBundleTools-<version>-win-x64-Installer.msi` |
| ARM64 | `AssestBundleTools-<version>-win-arm64-Portable.zip` | `AssestBundleTools-<version>-win-arm64-Installer.msi` |

Installer 使用 `Either` 语义。向导依次提供项目说明、MIT 许可确认、当前用户 / 所有用户范围、桌面快捷方式选项和独立安装目录页；当前用户模式不要求提升，所有用户模式在执行前显示 UAC 盾牌并请求提升。开始菜单快捷方式始终创建，桌面快捷方式默认勾选并允许取消。安装包继续创建稳定启动 stub、`Update.exe`、更新包目录和卸载入口，安装模式不会削弱应用内更新能力。

### 5.2 更新协议文件

每个架构还要上传 Velopack 生成的：

- `<packId>-<version>-full.nupkg`
- 可选 `<packId>-<version>-delta.nupkg`
- `releases.win-x86.json / releases.win-x64.json / releases.win-arm64.json`
- Velopack 构建资产索引
- `SHA256SUMS.txt`

更新 feed 中引用的文件名不得在上传后手工改名。用户入口文件可以复制为更清晰的带版本名称，但更新包和 feed 必须保持生成时的一致关系。

同一 Release 的 Release Notes 面向用户；更新服务只显示经过长度限制和基础清理的纯文本 / Markdown 摘要，不渲染任意 HTML 或执行链接。

## 6. 应用内更新流程

### 6.1 启动检查

1. Velopack startup hook 必须在 WinUI 正常初始化前最先执行，遇到安装 / 更新 hook 时按框架契约快速退出。
2. 正常启动完成 Shell 首帧后，通过统一后台任务 `ApplicationUpdateCheck` 发起检查，不阻塞窗口显示和工作区恢复。
3. 每个进程启动都会请求检查；同一台电脑上的多窗口 / 多工作区进程通过全局互斥和 `%LocalAppData%/AssestBundleTools/updates/state.json` 合并并发请求，其他进程复用本轮结果。
4. 启动检查无更新时不弹 toast；有更新时显示可操作通知，包含版本、摘要、下载大小和“下载并更新 / 稍后”。
5. 网络不可用、GitHub 限流或源不可用时只写结构化日志，不用黄色警告打扰正常启动。

每次启动“检测”指每个正常会话都会进入更新检查协调器；并发启动允许合并为一次网络请求。客户端不得通过嵌入 GitHub Token 提升限额。实现阶段应优先使用 Velopack `GithubSource`，并为响应缓存、超时和跨进程合并补适配；如果公开 GitHub API 限额在真实用户规模下不足，再把同一 Velopack feed 镜像到官网静态地址，不改变客户端更新协议。

### 6.2 关于页手动检查

关于页在版本信息附近增加“检查更新”按钮和状态区：

- `未检查`
- `正在检查`
- `已是最新版本`
- `发现 <version>`
- `正在下载 <progress>`
- `已下载，等待重启`
- `检查 / 下载失败`

手动检查必须立即进入协调器，不使用启动检查的静默错误策略；无更新和失败都要显示结果。检查中按钮禁用，避免并发请求。

### 6.3 下载与应用

1. 用户确认后由后台任务下载，展示字节进度和可取消状态。
2. Velopack 校验包大小与校验和；校验失败不得进入应用阶段。
3. 下载完成后请求应用重启。若存在运行中的导入、导出、保存、替换或未完成编辑保存，先走统一关闭协调，不直接终止窗口。
4. 所有当前应用进程都确认可退出后，启动 `Update.exe`，主进程退出，updater 替换对应模式的当前版本并重启稳定入口。
5. 用户选择稍后时保留已下载包，但关闭 Velopack 默认的无提示启动自动应用；下一次启动或关于页继续明确提示，由用户确认后应用。

Velopack updater 的等待超时不得作为常规“杀进程”手段。应用应在调用更新器前自行完成跨窗口协调；无法安全退出时取消本次应用并保留下载结果。

## 7. 便携版与安装版

### 7.1 安装版

- 由 `Installer.msi` 安装并维护稳定入口、`Update.exe` 和当前版本目录。
- 更新只替换框架管理的 current 内容。
- 卸载由 Velopack / Windows 应用列表承载。
- 用户设置、日志、缓存和工作区数据继续放在现有应用数据目录或工作区目录，不放入 current。

### 7.2 便携版

- Release ZIP 解压后直接运行，不要求安装或管理员权限。
- Velopack locator 标记为 portable，更新器只替换该便携目录管理的应用内容。
- 目录不可写、位于只读介质或受保护目录时禁用自动应用，保留“下载新版 / 打开 Release 页面”回退，不尝试提权修改。
- 便携目录中不得存放工作区事实数据；用户自己额外放入目录的未知文件不得被 updater 当成应用文件删除。实现前必须用真实 portable 包验证 Velopack 的保留范围。

### 7.3 模式保持

- 安装版更新后仍为安装版。
- 便携版更新后仍为便携版。
- 更新流程不把便携版迁移为安装版，也不反向迁移。
- 关于页可显示轻量的“便携版 / 安装版”状态，供诊断和用户确认；不要要求用户手工选择模式。

## 8. 分层与实现边界

### 8.1 Application

定义稳定语义：

- `ApplicationUpdateChannel / ApplicationArchitecture / ApplicationInstallMode`
- `ApplicationUpdateInfo / ApplicationUpdateProgress / ApplicationUpdateState`
- 检查、下载、请求应用、取消等 use case 和结果错误码
- 启动静默检查与手动显式检查的反馈策略

Application 不引用 Velopack、GitHub DTO、HTTP、WinUI 控件、文件替换器或进程对象。

### 8.2 Infrastructure

- 通过 Application 窄端口封装 Velopack `UpdateManager` 和 GitHub source。
- 负责 feed 查询、包下载、进度、校验、locator 模式与 channel / architecture 选择。
- 持久化跨进程检查状态和 ETag / 时间戳等技术数据。
- 将 Velopack / HTTP 异常映射为稳定错误，不把原始响应作为主提示。

### 8.3 Presentation

- 程序入口最前方执行 Velopack startup hook；该启动约束是主可执行文件边界，不下沉到普通页面。
- Shell 首帧后调度启动检查；关于页只绑定更新状态和命令。
- 使用现有后台任务、通知、诊断与本地化边界展示检查和下载状态。
- 在调用应用更新前协调所有窗口和活动操作，确认可安全退出。

### 8.4 独立 updater

首版不创建自研 updater 项目。Velopack 生成的 `Update.exe` 已承担主进程退出后替换文件的职责。只有以下事实被真实样本证明无法满足时，才重新评估独立原生 updater：

- WinUI / Windows App SDK 文件锁无法由现有 updater 稳定处理；
- 多工作区进程关闭协议无法接入；
- portable 的未知文件保留规则无法满足；
- ARM64 updater 无法稳定运行。

## 9. GitHub Actions 发布流程

workflow 由 `v*` tag push 触发，建议分为以下阶段：

1. **Validate**
   - `actions/checkout` 使用完整历史和 tags。
   - 校验 tag 为合法 SemVer、当前提交精确匹配 tag、工作树输入固定。
   - 运行全套测试和架构无关静态检查。
2. **Publish matrix**
   - 在 Windows runner 上按 `win-x86 / win-x64 / win-arm64` 矩阵执行 Release publish。
   - 显式传入同一个 `MinVerVersionOverride`，检查程序集 ProductVersion 与 tag 一致。
   - 保留现有自包含 WinUI、内置 codec 和音频处理器产物。
3. **Pack matrix**
   - 每个架构用独立 pack id、runtime、channel 执行 `vpk pack`。
   - 拉取上一正式 Release 的同架构 feed / full package后再生成可选 delta；拉取失败时仍允许生成 full package，但必须记录原因。
   - 生成 Portable ZIP、Installer.msi、full / delta 包和 feed；Velopack 同时产生的一键 Setup 不作为公开资产上传，资产索引在重命名后必须只引用实际上传文件。
4. **Verify**
   - 校验用户入口文件、feed 引用、SHA-256、PE 架构和版本完全一致。
   - x86 / x64 运行启动 smoke；ARM64 在可用的原生 Windows ARM runner 或真实设备上 smoke。没有 ARM64 运行验证时不得把结构验证描述为已实机验证。
   - 从上一版本的 portable 与 installed 样本各执行一次自动更新 smoke，验证模式保持、重启和用户数据保留。
5. **Release**
   - 所有矩阵成功后创建 draft Release，上传全部资产和校验表。
   - 二次验证 Release 资产数量、名称和 digest 后再发布；失败保持 draft，不暴露半套更新源。
   - stable Release 不标记 prerelease；预发布版本进入独立 preview 设计后才允许发布。

仓库 Actions 权限采用最小 `contents: write`，第三方 action 固定到受信任版本或 commit。发布前必须启用 tag / environment 保护、GitHub 账号强认证，并避免在 fork PR 上暴露 Release 权限。

## 10. 安全、签名与完整性

- 只允许 `laoxinH/AssestBundleTools` 的 HTTPS Release / 明确官网镜像。
- 使用 Velopack feed 和包校验；同时发布 `SHA256SUMS.txt` 并核对 GitHub Release asset digest。
- feed 只能选择当前架构、当前 channel 和高于当前版本的包。
- 下载设置合理超时、大小上限、磁盘空间预检和取消。
- 未签名版本允许作为早期公开包，但 UI / 文档必须如实说明可能出现 SmartScreen 警告。
- 代码签名一旦启用，主 EXE、Update.exe 和 MSI 必须在发布前统一签名，并在 CI 验证签名链；不能只签安装器。
- 客户端不信任 Release Notes 中的可执行地址，也不从远端 JSON 接受任意命令行。

## 11. 错误与诊断

稳定错误至少覆盖：

- `Update.Check.NetworkUnavailable`
- `Update.Check.RateLimited`
- `Update.Check.SourceUnavailable`
- `Update.Check.InvalidFeed`
- `Update.Check.ArchitectureMismatch`
- `Update.Download.InsufficientDiskSpace`
- `Update.Download.ChecksumMismatch`
- `Update.Apply.DirectoryNotWritable`
- `Update.Apply.ActiveOperations`
- `Update.Apply.OtherInstancesRunning`
- `Update.Apply.UpdaterLaunchFailed`

启动检查只对“发现更新”显示用户提示；其余写诊断。手动检查和用户发起的下载 / 应用必须显示本地化结果。原始 Velopack / GitHub 消息只进入详细诊断。

## 12. 测试与验收

### 12.1 自动化测试

- SemVer、stable / prerelease 和禁止降级比较。
- 当前 RID 到 channel / pack id 映射。
- portable / installed / unmanaged debug build 模式映射。
- 启动静默与手动显式反馈差异。
- 多进程检查 single-flight 和缓存状态损坏恢复。
- 下载取消、校验失败、磁盘不足和不可写目录。
- 关于页状态与命令绑定。
- 发布脚本资产矩阵、feed 引用、版本、架构和 hash 合约。

### 12.2 真实发布门禁

每个架构至少验证：

- 全新 MSI 当前用户安装、启动、卸载，并检查桌面快捷方式默认值；
- MSI 所有用户选项、UAC 提升提示和自定义安装目录；
- Portable 解压启动；
- 旧版到新版的安装版更新；
- 旧版到新版的便携版更新；
- 带活动工作区、多窗口和后台任务时拒绝强制应用；
- 更新后工作区、偏好、日志、缓存目录和用户插件仍在；
- 断网、下载中断、校验失败、目录只读后旧版仍可启动。

## 13. 分阶段落地

### 阶段 A：发布基线

- 添加 GitHub Actions tag workflow。
- 用 Velopack 生成三架构 Portable + Installer.msi + full package + feed。
- 建立资产矩阵、版本、架构和 hash 验证；先完成手工安装 / 更新 smoke。

状态：`v0.1.0` 的 GitHub Actions 三架构构建、PE / 版本校验、旧版 Setup / Portable / feed 发布和 Release digest 校验已完成。后续版本已将公开安装入口设计切换为 MSI。2026-07-19 已在本地 x64 完成欢迎 / 自述 / MIT 许可确认、当前用户 / 所有用户选项、默认当前用户、自定义安装目录、桌面快捷方式默认勾选与取消两条路径、开始菜单、稳定入口、`Update.exe`、应用启动和卸载清理的真实验证；所有用户模式的实际提升安装以及 x86 / ARM64 实机安装仍属于发布门禁。跨版本更新 smoke 等待下一稳定版本。

### 阶段 B：更新读取链路

- 建立 Application DTO / use case 和 Infrastructure Velopack adapter。
- 接入 startup hook、启动检查、关于页手动检查和诊断。
- 此阶段只检查并打开 Release 页面也可以作为中间可交付点，不提前伪装自动应用完成。

状态：已完成检查网关、startup hook、启动检查、关于页入口、多进程合并与诊断。

### 阶段 C：下载与自动应用

- 接入后台下载、进度、取消、校验、跨窗口关闭协调和 `Update.exe`。
- 完成 installed / portable 双路径真实更新测试。

状态：下载、取消、活动任务 / 其他实例阻断和 `Update.exe` 应用调用已完成；`v0.1.0` 已作为升级基线，双版本 installed / portable 真实升级门禁在下一稳定版本发布前后执行。

### 阶段 D：发布加固

- 增量包、官网 feed 镜像、代码签名、preview channel 和失败恢复增强。

状态：未开始，保持后续范围。

只有阶段 C 的真实 installed / portable 更新门禁全部通过后，产品文案才可以宣称“支持自动更新”。

## 14. 参考依据

- [Velopack Packaging Overview](https://docs.velopack.io/packaging/overview)
- [Velopack Integrating Overview](https://docs.velopack.io/integrating/overview)
- [Velopack Windows Installers](https://docs.velopack.io/packaging/installer)
- [Velopack Update Sources](https://docs.velopack.io/integrating/update-sources)
- [GitHub REST API: Releases](https://docs.github.com/en/rest/releases/releases)
- [GitHub REST API Best Practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api)
- [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
