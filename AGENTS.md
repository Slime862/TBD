# 项目协作规则

## 文本与编码
- 所有文本文件读取必须显式指定 UTF-8。
- 所有文本文件写入必须显式指定 UTF-8。
- 禁止使用 PowerShell 默认编码直接写入源码、Markdown、XML、Gradle、Kotlin、Java、YAML、JSON、Properties 文件。
- 优先使用 Python 的 `Path(...).read_text(encoding="utf-8")` 和 `Path(...).write_text(text, encoding="utf-8")` 做文件读写。
- 在 shell 命令中修改中文内容时，避免直接内联中文大段文本；优先使用 Python 组装字符串后以 UTF-8 写入。
- 终端出现 `????` 时，不要直接判断文件已损坏，应优先使用编辑器或 `unicode_escape` 检查文件真实内容。

## 中文内容
- 如果不是需求需要，不要动项目里的中文，特别是中文注释。
- 新增文档和 UI 文案默认使用中文，除非对应需求明确要求其他语言。

## 实现方式
- 实现功能时，如果可通过少量修改或复用实现，就优先使用少量修改实现。
- 不要在脚本里堆大量兼容旧结构、`nil` 回退、旧 table 格式之类的保护代码。
- 可能报错的情况要打印明确错误，不要静默 `return` 导致问题被隐藏。

## Unity 资源
- 修改 YAML、添加 prefab 不方便时，优先写编辑器生成代码生成 prefab 或场景对象。
- 可以选择生成整个 prefab，或生成小 prefab 后手动放入场景再解除 prefab 绑定。
