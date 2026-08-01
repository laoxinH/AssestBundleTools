# AssestBundleTools 文档

本目录按读者和职责组织。不要从文件名猜测权威性，先从这里选择入口。

## 用户文档

- [图文使用指南](user-guide.md)：工作区、导入、预览、替换、Spine、批量操作和导出。
- [版本说明](releases/README.md)：发布说明的命名、维护和消费规则。

## 插件开发

- [插件开发与安装指南](plugin-guide.md)：三类插件的唯一对外协议，包括 manifest、CLI、安装包和交付检查。

插件管理页、进程执行和工作区集成的内部边界属于设计文档，不应在对外指南之外再次定义一套协议。

## 项目设计

- [设计文档索引](design/README.md)：稳定规范、功能专题和维护规则。
- [当前路线图](design/project-build-roadmap.md)：仅记录当前能力基线、活跃工作流和明确延后项。
- [当前偏差登记](design/implementation-deviation-register.md)：仅记录尚未关闭的架构偏差和体量风险。

## 目录约定

- `assets/`：被用户文档实际引用的图片和动画。
- `design/`：面向维护者的稳定契约和专题设计。
- `releases/`：与 Git tag 对应的不可变发布说明。

已完成的实施流水、旧截图和过期阶段计划不在当前文档中长期保留；需要追溯时使用 Git 历史。发布说明例外，它们是 GitHub Release 和应用内更新日志的正式输入。
