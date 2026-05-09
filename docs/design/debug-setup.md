# AssestBundleTools VS 调试配置说明

## 1. 文档目的

本文档用于固定当前阶段推荐的 Visual Studio 调试方式，目标如下：

- 保证 WinUI 界面可以直接启动做初步断点调试
- 明确推荐使用的启动配置，避免误用打包调试模式
- 让团队成员打开解决方案后能快速进入一致的调试路径

## 2. 当前推荐方案

当前阶段推荐使用以下组合：

- 启动项目：`AssestBundleTools.Presentation.WinUI`
- 解决方案配置：`Debug`
- 平台：`x64`
- 启动配置：`AssestBundleTools (Unpackaged Debug)`

推荐原因：

- `Unpackaged` 启动更轻量，适合页面骨架、导航、假数据链路和断点调试
- `x64` 更适合作为日常开发默认平台，后续接入真实 AB 读取库时也更稳妥
- `Package Debug` 仅在需要验证打包行为、清单能力或安装态差异时再使用

## 3. 已落地到仓库的配置

当前仓库已经补充了以下调试相关配置：

- `src/AssestBundleTools.Presentation.WinUI/Properties/launchSettings.json`
- `Directory.Build.props`

其中：

- `launchSettings.json` 提供了 `Unpackaged Debug` 和 `Package Debug` 两个启动入口
- 两个调试入口默认都会设置 `ABT_DISABLE_SYSTEM_BACKDROP=1`，避免系统背景材质在调试环境中影响启动稳定性
- `Directory.Build.props` 显式固定了 `Debug` 构建下的符号与优化配置，便于断点调试

## 4. 在 VS 中第一次使用时的设置

需要说明的是：Visual Studio 的“启动项目”本身通常属于本地 IDE 状态，不适合直接强行写入仓库。

因此第一次打开解决方案时，请在 VS 中手动确认一次：

1. 右键 `AssestBundleTools.Presentation.WinUI`
2. 选择“设为启动项目”
3. 顶部配置切换到 `Debug | x64`
4. 启动配置下拉框选择 `AssestBundleTools (Unpackaged Debug)`
5. 按 `F5` 启动调试

完成一次后，VS 通常会记住你的本地选择。

## 5. 命令行编译约定

当前阶段若需要在终端里快速验证“解决方案是否可编译”，优先使用 Visual Studio 自带的 `MSBuild`，而不是直接依赖 `dotnet build`。

推荐原因：

- 当前 WinUI 解决方案在 VS `MSBuild` 链路下更稳定
- 可规避部分首次运行初始化、Roslyn 共享编译与并行复制带来的噪声问题
- 更接近日常在 Visual Studio 中实际使用的构建行为

当前已验证可复用的命令如下：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe' 'AssestBundleTools.sln' /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1 /nr:false /nologo
```

如果 WinUI 应用当前仍在运行，默认输出目录中的 DLL 可能被占用。
这时建议改用单独的验证输出目录，避免 `MSB3021 / MSB3027`：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe' 'AssestBundleTools.sln' /t:Build /p:Configuration=Debug /p:Platform=x64 /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1 /nr:false /nologo /p:OutDir=c:\Users\thixi\source\repos\AssestBundleTools\AssestBundleTools\artifacts\validation\tab-context-menu\
```

参数说明：

- `UseSharedCompilation=false`：关闭 Roslyn 共享编译进程，降低环境差异导致的偶发问题
- `BuildInParallel=false` 和 `/m:1`：收敛并行构建，便于稳定复现真实错误
- `/nr:false`：关闭节点复用，减少后续增量编译时的残留干扰

如果本机 Visual Studio 安装路径不同，可先用 `vswhere` 定位 `MSBuild.exe`：

```powershell
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild 'AssestBundleTools.sln' /t:Build /p:Configuration=Debug /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1 /nr:false /nologo
```

补充说明：

- 当前命令用于“日常命令行编译验证”，默认不额外强制平台参数
- 如果需要专门验证 `Debug | x64`，建议先停止正在运行的应用或结束 VS 调试会话，否则 WinUI 输出目录可能被锁定，触发 `MSB3021 / MSB3027`
- 如果不方便停止当前运行实例，可直接改用上面的 `OutDir` 方案，把验证产物输出到独立目录
- 当前环境下可能仍会出现 `MSB3774`（缺少 `Microsoft.UniversalCRT.Debug`）警告，但它不影响这条验证命令完成构建

### 5.1 最近遇到的编译坑与处理方式

- `AssestBundleTools.Presentation.Tests` 项目固定为 `x86 / win-x86`。
  如果误用 `/p:Platform=x64` 去编它，容易触发 `MSB3270`，提示测试项目的 `x86` 与被引用 DLL 的 `AMD64` 架构不匹配。
  这类展示层测试请改用 `/p:Platform=x86`，或直接使用项目自身默认平台。
- 为了避开文件锁，优先只重定向 `OutDir`，不要顺手再加 `BaseIntermediateOutputPath`。
  本仓库在跨项目验证时若同时改 `BaseIntermediateOutputPath`，曾出现 `NETSDK1004`（缺少 `project.assets.json`）以及 `CS0579`（重复程序集特性）这类由 restore / obj 隔离不完整带来的噪声。
  当前更稳妥的做法仍是保留默认 `obj`，只在必要时改 `OutDir`。
- 多个测试项目不要并行构建或并行跑各自的前置构建。
  这次实际遇到过 `CS2012`、`MSB3021`、`MSB3027`，根因是不同命令同时写入 `src/*/obj` 或复制 `src/*/bin`。
  继续使用 `BuildInParallel=false`、`/m:1`、`/nr:false`，并让 `Infrastructure` / `Presentation` 验证串行执行，会明显稳定很多。
- 不要把解决方案 `MSBuild` 和 `AssestBundleTools.Application.Tests` 的 `dotnet run` 并行执行。
  当前 `Application.Tests` 会在解析引用前构建 `src/AssestBundleTools.Application`，如果同时有另一条 `MSBuild` 正在写同一个 `obj\Debug\net8.0\AssestBundleTools.Application.dll`，
  会触发 `CS2012`（文件正被另一进程使用）。
  处理方式是等待上一条构建完全结束后再串行跑测试；这类错误通常不是业务代码失败。
- 当前环境下 `dotnet test tests/AssestBundleTools.Presentation.Tests/... -p:Platform=x86` 曾出现退出码 `1` 但没有正文输出的静默失败。
  如果 `MSBuild` 已能构建 `Presentation.Tests`，而 `dotnet test` 没给出断言或编译错误，优先改用 Visual Studio 的 `vstest.console.exe` 跑已生成的测试 DLL：

```powershell
$vstest = 'C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
& $vstest 'tests\AssestBundleTools.Presentation.Tests\bin\x86\Debug\net8.0-windows10.0.19041.0\win-x86\AssestBundleTools.Presentation.Tests.dll' /Platform:x86 /InIsolation
```

  如果 VS 安装路径不同，仍可先通过 `vswhere` 找到安装目录再拼出 `Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`。
- 如果为了避开正在运行的 WinUI 应用锁文件而给 `Presentation.Tests` 构建设置了独立 `OutDir`，该目录可能会带出 app-local `hostfxr.dll / hostpolicy.dll`。
  此时 x86 `testhost.x86.exe` 会把该 `OutDir` 误当成 `.NET location`，并提示找不到 `Microsoft.NETCore.App 8.0.0 (x86)`，即使本机已经安装了 x86 .NET 8 运行时。
  处理方式是在执行 `vstest.console.exe` 前删除该验证输出目录根部的这两个 host 文件，再指定测试适配器路径：

```powershell
$outDir = 'D:\project\AssestBundleTools\artifacts\validation\presentation-tests\'
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild 'tests\AssestBundleTools.Presentation.Tests\AssestBundleTools.Presentation.Tests.csproj' /t:Build /p:Configuration=Debug /p:Platform=x86 /p:UseSharedCompilation=false /p:BuildInParallel=false /m:1 /nr:false /nologo /p:OutDir=$outDir

Remove-Item -LiteralPath (Join-Path $outDir 'hostfxr.dll'), (Join-Path $outDir 'hostpolicy.dll') -ErrorAction SilentlyContinue

$vsInstall = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath | Select-Object -First 1
$vstest = Join-Path $vsInstall 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
$testDll = Join-Path $outDir 'AssestBundleTools.Presentation.Tests.dll'
& $vstest $testDll /Platform:x86 /InIsolation "/TestAdapterPath:$outDir"
```

  不要删除输出目录子目录或 NuGet 包缓存中的 host 文件；只清理这次临时验证 `OutDir` 根部的两项。
- 测试项目不要用固定 `HintPath` 指向 `src/*/bin/Debug/...` 的旧程序集。
  在带 `/p:Platform=x86` 的构建中，真实产物会落到 `bin\x86\Debug\...`，固定 `HintPath` 可能让测试项目悄悄引用过期 DLL，表现为“源码里明明有新 DTO 字段，测试编译却提示字段不存在”。
  处理方式是优先使用 `ProjectReference`，让测试项目跟随当前源码和平台一起构建。

## 6. 什么时候切换到 x86

以下情况再考虑切换到 `Debug | x86`：

- 需要验证特定第三方库仅支持 32 位
- 需要复现仅在 x86 输出下出现的问题
- 需要和历史工具链保持一致
- 需要构建或执行 `AssestBundleTools.Presentation.Tests`

如果没有这些前提，当前默认继续使用 `x64`。

## 7. 当前阶段的调试建议

优先验证以下内容：

- 应用能否正常启动到主界面
- Shell 导航是否正常
- Bundle 浏览页是否能显示示例数据
- 资产列表、详情区、日志区是否联动
- 本地化资源是否正常加载

## 8. 后续可选增强

后续如果需要进一步减少手动配置，可以再补：

- 共享的解决方案启动配置 `.slnLaunch`
- 更细分的诊断环境变量
- 调试专用工作区启动参数

当前阶段先保持最小、稳定、易用的调试方案即可。

## 9. Spine D3D Smoke 验证

Spine 高复杂 mesh 播放的 D3D11 后端当前仍是受控 smoke，不作为默认路径。需要验证 `SwapChainPanel` / D3D11 设备、swap chain、render target、Texture2D 预览载荷上传和顶点 / 索引批次提交时，可在启动前设置：

```powershell
$env:ABT_SPINE_D3D11_SMOKE = '1'
```

允许值为 `1`、`true` 或 `d3d11`。未设置时继续使用 Win2D 回退后端；该 smoke 当前会解码 Texture2D 预览载荷、上传为 D3D shader resource、采样真实贴图，并通过可复用动态 vertex / index buffer 提交批次。播放控件卸载时会释放后端资源，resize 会短防抖合并，Texture2D 解码和上传带 generation 保护；遇到 DXGI device removed / reset / hung / internal error 会先释放并重建一次，仍失败才切回 Win2D 后端。

需要记录真实样本基线时，可额外打开 D3D 诊断输出：

```powershell
$env:ABT_SPINE_D3D11_DIAGNOSTICS = '1'
```

该诊断只写入调试输出，记录 native `SwapChainPanel` 绑定 readiness / strategy、device / resize、batch renderer 预热、Texture2D decode、GPU texture upload、runtime frame generation、节流后的 frame batch / vertex / index / elapsed 数据、`uploadMs / projectMs / clipMs / drawMs / presentMs` 分段耗时、播放 / 暂停 / animation / skin / setup pose / step 等交互事件，以及 fallback 或 device lost 恢复异常类型；不在 UI 中显示原始 D3D 异常。当前 D3D 后端会在 GPU 上传成功后释放 CPU 侧 BGRA 解码页，并在渲染上传动态 buffer 时用池化数组直接做 clip-space 投影，因此 `clipMs` 不再对应一份长期常驻的 clip-space batch 缓存。实时播放主线不再默认预热整段 sampled frame batch，旧 batch 预热只保留在兼容或诊断语境中。

真实样本基线建议至少记录以下操作的连续日志：首帧贴图 decode / upload，正常播放 10 秒，窗口缩放，切 animation，切 skin，暂停 / 恢复，逐帧，进度拖动，重开同一 Spine 组。当前样本记录保存在 [`spine-d3d11-performance-baseline.md`](spine-d3d11-performance-baseline.md)：`char_2026_yu_nian#12.ab` 已验证当前 runtime playback session + D3D 当前帧路径达到 `60 FPS` 交互观感；默认切换前还需要补齐更多设备、内存占用、高刷新目标和设备恢复路径验证。
