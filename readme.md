# 🤖 AI Renamer - 智能文件重命名工具

## 🎯 产品定位

**"智能重命名"** —— 在资源管理器右键菜单里增加一项，点击后 3 秒内弹出 3～5 个 AI 推荐文件名，用户点选即完成重命名；全程本地缓存、零 GUI 常驻进程、可离线跑。

## ⚡ MVP 功能清单
- **📦 安装**：双击 `.reg` + 一个 5 MB 的 single-exe 即完成注册表写入与右键菜单出现
- **🖱️ 触发**：单文件右键 → "AI 智能重命名"
- **🔍 识别**：
  - 📸 图片/PDF → 本地 rapid OCR（PaddleOCR-mobile，8 MB 模型）
  - 📄 Office/TXT → 直接文本抽取（FreeSpire.Doc + ExcelDataReader + Zip+XML 直读 DOCX/PPTX，纯托管）
- **📝 摘要**：
  - 🏠 本地 TextRank 抽 20 个关键词 → 拼接成 256 token 以内 prompt
  - 🌐 若本地置信度<0.6 或用户 Shift+右键，再走线上大模型（DeepSeek / 火山）做"关键词 → 文件名"
- **💡 推荐**：弹一个最轻量 Win32 原生菜单（不是 WinForms/WPF），显示 3～5 条候选；用户上下键+回车即可
- **✏️ 命名**：回车后直接 File.Move；如重名自动加 `(_1)`
- **🗑️ 卸载**：双击 `uninstall.reg` 即可清理，不驻留服务/托盘
## 🚀 非功能约束（MVP 阶段）

- ⚡ **性能**：冷启动到出现候选 ≤ 3s（本地 OCR+本地摘要≤1s，网络请求≤2s）
- 💾 **内存**：峰值 < 150 MB（OCR 模型按需加载，用完即卸）
- 📦 **体积**：安装包 < 20 MB；单 exe 可放在 U 盘随走随用
- 🖥️ **兼容**：必须支持 Win10 1903+ x64，无需 .NET 6 运行时（用 .NET 8 NativeAOT 静态编译）
- 🛡️ **容错**：任何一步失败，自动降级到"纯本地关键词+日期"命名，保证可用

## 🛠️ 技术选型（MVP）
### 💻 **宿主语言：C# + .NET 8**
- ✨ 可用 NativeAOT 编译成单 exe，启动 150ms 级，内存占用远低于 Python
- 📚 社区库全：Windows 右键菜单、OCR、PDF、Office 解析都有现成包

### 👁️ **OCR 引擎：PaddleOCR-json**
- 📦 C++ 封装，带 8MB 轻量模型
- 🚀 首次运行解压缩到 `%Temp%`，用完可删；识别 1 页 A4 < 300ms

### 📄 **文本抽取**
- 📕 **PDF**: UglyToad.PdfPig（MIT，纯托管）
- 📒 **DOC**: FreeSpire.Doc（Community 版，支持 Word 97-2003）
- 📘 **DOCX**: Zip+XML 直读（System.IO.Compression + XmlReader）
- 📗 **XLSX**: ExcelDataReader  
- 📙 **PPTX**: Zip+XML 直读（System.IO.Compression + XmlReader）

### 🧠 **本地摘要**
- 🔍 用 TextRank 的 C# 移植（GitHub 有 200 行代码实现），抽 Top20 词
- 📊 置信度 = 最大权重 / 平均权重，>0.6 即认为"够代表"

### 🌐 **远端大模型**
- 🤖 **DeepSeek-Coder** 在线 API（价格 1 元/100k tokens，速度 50~80 token/s）
- 🌋 **火山引擎** "Skylark"系列也可，用统一 HttpClient 封装，方便切换
- 💬 **提示词模板**（256 token 以内）：
  > "下面是一段文档内容的关键词列表：{keywords}。请给出 3~5 个简洁的中文文件名（≤30 字符），不要特殊符号，用空格或下划线分隔，返回一行一个，不要解释。"

### 🖼️ **交互 UI**
- 🚫 不弹传统窗口，用 CppShellExt 模板生成 IContextMenu3，直接在资源管理器弹原生菜单
- 📋 如需多行候选，调用 Win32 的 TrackPopupList（Vista 后支持），纯 Win32 API，零托管句柄

### ⚙️ **配置 & 缓存**
- 💾 单文件 `%LOCALAPPDATA%\AIRename\cache.json`，缓存最近 1000 条文件的 SHA256 → 推荐结果，下次秒出
- 🔑 用户可放 `api_key.json`，如不存在则仅用本地模式
## 📁 目录结构与编译脚本

```
/AIRename
├─ src/
│  ├─ AIRename.csproj          # <PublishAot> 配置
│  ├─ Program.cs               # 入口，NativeAOT 的 Main
│  ├─ ShellExtension/*.cs      # IContextMenu 实现
│  ├─ Ocr/*.cs                 # PaddleOCR-json 调用封装
│  ├─ TextExtract/*.cs         # 各格式 reader
│  ├─ Summarize/TextRank.cs    # 本地摘要算法
│  └─ LLM/DeepSeekClient.cs    # 大模型 API 客户端
├─ assets/
│  ├─ PaddleOCR-json.zip       # 8MB OCR 模型
│  ├─ reg/install.reg          # 注册表安装脚本
│  └─ reg/uninstall.reg        # 注册表卸载脚本
└─ build.ps1                   # 构建脚本
```

### 🔨 **构建命令**
- `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishAot=true`
- 把 exe+assets 一起压缩成 `AIRename_v1.0.zip`

## 📦 安装方式
### 🔧 **环境要求**
- 🖥️ Windows 10 1903+ x64
- 🔓 无需管理员权限（写入 `HKCU`），无需 .NET 运行时（NativeAOT 单 exe）

### 📥 **方式 A：Release 包安装（推荐）**
1. 📦 在 GitHub Releases 下载 `AIRename v1.0` 的压缩包并解压
2. 📂 将 `AIRename.exe` 复制到 `%LOCALAPPDATA%\AIRename\AIRename.exe`（路径必须一致）
3. 🖱️ 双击 `assets\reg\install.reg` 导入注册表，新增右键菜单
4. ✅ **验证**：在资源管理器中右键任意文件或目录，应出现"AI 智能重命名"

### 🔨 **方式 B：本地构建安装（开发/离线）**
1. 📥 安装 `.NET 8 SDK`（仅构建时需要）
2. 💻 在项目根目录用 PowerShell 运行 `.\build.ps1`，脚本会：
   - 🚀 发布 NativeAOT 单文件到 `publish\win-x64\AIRename.exe`
   - 📋 自动复制到 `%LOCALAPPDATA%\AIRename\AIRename.exe`
3. 🖱️ 完成后，双击 `assets\reg\install.reg` 注册右键菜单
4. 🔍 **诊断**：`.\build.ps1 -Diagnose` 检查 `dotnet`、目标 exe 是否存在及注册表状态

### 🗑️ **卸载**
- 🖱️ 双击 `assets\reg\uninstall.reg` 清理右键菜单
- 🧹 **可选清理**：删除 `%LOCALAPPDATA%\AIRename\AIRename.exe` 与 `%LOCALAPPDATA%\AIRename\cache.json`（缓存文件）

### 🔄 **升级**
- 🔄 直接替换 `%LOCALAPPDATA%\AIRename\AIRename.exe` 即可，菜单键名不变，无需重复导入 `.reg`

### ⚠️ **注意事项**
- 📍 `.reg` 的命令固定指向 `%LOCALAPPDATA%\AIRename\AIRename.exe`，若 exe 未复制到该路径，点击菜单将无效果
- 👤 当前注册为"当前用户"范围（`HKCU`），企业环境可用组策略分发
- 🚫 文件名需过滤 Windows 保留字符 `< > : " / \ | ? *` 及尾部空格/句点

## ✅ MVP 验证 Checklist

- [x] 🖱️ 右键菜单出现
- [ ] ⚡ 第一次冷启动 3s 内出候选
- [ ] 🔌 断网情况下仍能给"关键词+日期"命名
- [ ] 🧹 卸载后注册表无残留
- [ ] 📄 单文件最大 20MB PDF 能正常识别
- [ ] 📦 安装包体积 < 20MB

## 🚀 后续可迭代方向（V1→V2）
### 🧠 **本地小模型**
- 🔬 把 60MB 中文 MiniLM 蒸馏成 20MB，onnxruntime 本地推理，完全离线

### 📚 **批量重命名**
- ⚡ 支持选中 50 个文件并发处理，用 Channel+Semaphore 控制并发 4

### 🔍 **增量索引**
- ⏰ 后台计划任务定时扫指定目录，提前把 OCR/摘要算好，右键秒开

### 🔌 **插件化**
- 🧩 用 MEF 动态加载自定义命名规则（日期、项目编号、正则）

### ☁️ **云缓存**
- 🌐 同一文件 SHA256 全局共享，公司内网搭建私有缓存服务，减少重复 tokens

## ⚠️ 常见坑提示
- 🔄 **NativeAOT 反射限制**：OCR 调用的 C++ 进程启动用 CreateProcess，不要用 Process.Start 带反射
- 🖥️ **右键菜单 64/32 位双注册**：在 x64 系统里，32 位资源管理器也能弹出，需提供 x86 小 stub 转发
- 🚫 **文件名非法字符**：Windows 保留 `< > : " / \ | ? *`，以及尾部空格/句点，需提前过滤
- 📁 **长路径**：Win10 默认关闭长路径支持，manifest 里加 `<longPathAware>`
- 🌍 **OCR 多语言**：如用户目录里有日文/韩文，需下 20MB 多语言模型，MVP 可先提示"仅支持中英"

## ⏱️ 时间估算
| 周次 | 任务 | 目标 |
|------|------|------|
| 📅 **Week 1** | 🏗️ 搭建 NativeAOT 空项目 + 右键菜单注册 | → 能弹 HelloWorld |
| 📅 **Week 2** | 📝 接入 TextExtract + TextRank | → 本地能出关键词 |
| 📅 **Week 3** | 👁️ 接入 PaddleOCR-json | → 图片 PDF 也能跑 |
| 📅 **Week 4** | 🤖 对接 DeepSeek API + 缓存 | → 完整链路跑通 |
| 📅 **Week 5** | 📦 打包/测试/写 README | → 发布 v1.0 |

---

> 💡 **提示**：本项目采用 MVP 快速迭代模式，优先保证核心功能可用，后续版本逐步完善体验和性能。
### ⚙️ **配置 & 环境变量**
- `AIRENAME_MAX_CHARS`：文本抽取最大字符数（默认 `2000`）。运行时读取。
