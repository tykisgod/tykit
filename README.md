# tykit

Lightweight HTTP server inside Unity Editor. Any AI agent can control Unity via `curl` — compile, run tests, play/stop, read console, inspect GameObjects.

No SDK. No plugin API. No UI automation. Just HTTP.

> This repo is an automated mirror of [`tykisgod/quick-question/packages/com.tyk.tykit`](https://github.com/tykisgod/quick-question). Do not edit directly — changes are synced from the source on every push.

<p align="center">
  <a href="#english">English</a> |
  <a href="#中文">中文</a> |
  <a href="#日本語">日本語</a> |
  <a href="#한국어">한국어</a>
</p>

---

# English

## Install

Add one line to your Unity project's `Packages/manifest.json`:

```json
"com.tyk.tykit": "https://github.com/tykisgod/tykit.git"
```

Open Unity — tykit starts automatically. Port is stored in `Temp/tykit.json`.

**Requirements:** Unity 2021.3+, Newtonsoft JSON (auto-resolved via UPM dependency).

## Quick Start

```bash
# Get port
PORT=$(python3 -c "import json; print(json.load(open('Temp/tykit.json'))['port'])")

# Ping
curl -s http://localhost:$PORT/ping

# Send a command
curl -s -X POST http://localhost:$PORT/ \
  -d '{"command":"status"}' -H 'Content-Type: application/json'
```

## Two HTTP Channels

tykit listens on a single port but exposes two parallel command channels:

| Channel | Endpoint | Runs on |
|---|---|---|
| **Main-thread queue** | `POST /` | Unity main thread (queued via `EditorApplication.update`) |
| **Listener-thread direct** | `GET /ping`, `/health`, `/focus-unity`, `/dismiss-dialog` | Listener thread (never blocks on the main thread) |

The listener-thread endpoints (v0.5.0) let you recover from a blocked main thread — see [Main Thread Recovery](#main-thread-recovery).

## API Reference

### Diagnostics

| Command | Args | Description |
|---------|------|-------------|
| `status` | — | Editor state (isPlaying, isCompiling, activeScene) |
| `commands` | — | List all registered commands |
| `compile-status` | — | Compilation state |
| `get-compile-result` | — | Last compile result (state, errors, duration) |

### Compile and Test

| Command | Args | Description |
|---------|------|-------------|
| `compile` | — | Trigger compilation |
| `run-tests` | `mode`, `filter`, `assemblyNames` | Start EditMode/PlayMode tests |
| `get-test-result` | `runId` (optional) | Poll test results |

### Console

| Command | Args | Description |
|---------|------|-------------|
| `console` | `count`, `filter` | Read recent console entries |
| `clear-console` | — | Clear console buffer |

### Scene and Hierarchy

| Command | Args | Description |
|---------|------|-------------|
| `find` | `name` / `type` / `tag` / `parentId` / `path` / `includeInactive` | Find GameObjects (v0.4 added scoping + inactive support) |
| `select` | `id` / `ids` (multi) / `ping` | Select object(s); v0.4 added multi-select |
| `ping` | `id` / `assetPath` | Highlight without selecting (v0.4) |
| `inspect` | `id` / `name` | Inspect components, now includes `children` array (v0.3) |
| `hierarchy` | `depth` / `id` / `path` / `name` | Scene hierarchy tree, optionally subtree (v0.3) |

### GameObject Lifecycle

| Command | Args | Description |
|---------|------|-------------|
| `create` | `name`, `primitiveType`, `position` | Create GameObject |
| `instantiate` | `prefab`, `name` | Instantiate prefab |
| `destroy` | `id` | Destroy GameObject |
| `set-transform` | `id`, `position`, `rotation`, `scale` | Modify transform |
| `set-name` | `id`, `name` | Rename GameObject (v0.3) |

### Components

| Command | Args | Description |
|---------|------|-------------|
| `add-component` | `id`, `component` | Add component to GameObject |
| `component-copy` | `id`, `component` | Copy component values via `ComponentUtility` (v0.5) |
| `component-paste` | `id`, `component` / `asNew` | Paste copied component, or add as new (v0.5) |

### Properties (Serialized)

| Command | Args | Description |
|---------|------|-------------|
| `get-properties` | `id` / `structured: true` | List serialized properties; `structured` returns native JSON types (v0.3) |
| `set-property` | `id`, `component`, `property`, `value` | Set serialized property — accepts native JSON for `Vector*`/`Quaternion`/`Color`/`Rect`/`Bounds` (v0.3), `LayerMask`/`ArraySize` (v0.5) |

### Reflection (Code-Level) — v0.4

> Bypasses SerializedProperty entirely. Walks the type hierarchy for inherited private members. Finally makes runtime testing viable without scaffolding code into your project.

| Command | Args | Description |
|---------|------|-------------|
| `call-method` | `id`, `component`, `method`, `parameters` | Invoke any public/non-public method via reflection |
| `get-field` | `id`, `component`, `field` | Read code-level field or property |
| `set-field` | `id`, `component`, `field`, `value` | Write code-level field or property |

### Arrays

| Command | Args | Description |
|---------|------|-------------|
| `get-array` | `id`, `component`, `property` | Read entire serialized array/list as structured JSON (v0.4) |
| `array-size` / `array-insert` / `array-delete` | — | Read/set size, insert at index, delete element (v0.3) |
| `array-move` | `from`, `to` | Reorder via `MoveArrayElement` (v0.4) |

### Prefabs (v0.5)

| Command | Args | Description |
|---------|------|-------------|
| `prefab-apply` / `prefab-revert` | `id` | Commit or revert scene changes to source prefab asset |
| `prefab-open` / `prefab-close` | `path` / `save` | Enter / exit prefab edit mode |
| `prefab-source` | `id` | Get source prefab asset path of an instance |

### Physics Queries (v0.5)

| Command | Args | Description |
|---------|------|-------------|
| `raycast` | `origin`, `direction`, `maxDistance`, `layerMask` | Single raycast |
| `raycast-all` | same | All hits along ray |
| `overlap-sphere` | `position`, `radius`, `layerMask` | Colliders intersecting a sphere |

### Assets (v0.5)

| Command | Args | Description |
|---------|------|-------------|
| `find-assets` | `type`, `folder`, `name` | `AssetDatabase.FindAssets` wrapper |
| `create-scriptable-object` | `type`, `path` | Create + save a `ScriptableObject` instance |
| `load-asset` | `path` | Resolve asset by path |
| `refresh` | — | `AssetDatabase.Refresh()` |

### UI

| Command | Args | Description |
|---------|------|-------------|
| `set-text` | `id`, `text`, `inChildren` | Set text on `TMP_Text` / `TextMeshProUGUI` / `Text` (v0.3) |
| `button-click` | `id` | Simulate `onClick.Invoke()` (v0.5) |

### Editor Control

| Command | Args | Description |
|---------|------|-------------|
| `play` | — | Enter Play Mode (auto-saves dirty scenes first, v0.5) |
| `stop` / `pause` | — | Play Mode control |
| `save-scene` | — | Save current scene |
| `save-scene-as` | `path` | Save active scene to a new path (v0.5) |
| `set-active-scene` | `path` / `name` | Switch active scene in multi-scene setups (v0.5) |
| `open-scene` | `path` | Open scene by asset path (auto-saves dirty scenes first, v0.5) |
| `menu` | `item` | Execute menu item |
| `focus-unity` / `dismiss-dialog` | — | Windows-only main-thread variants — see Recovery section for the listener-thread versions (v0.5) |

### Prefs (v0.5)

| Command | Args | Description |
|---------|------|-------------|
| `editor-prefs` | `action: get/set/delete`, `key`, `value` | Read/write/delete `EditorPrefs` |
| `player-prefs` | same | Read/write/delete `PlayerPrefs` |

### Batch

Execute multiple commands in one HTTP round-trip — reduces 30+ calls to 1:

```bash
curl -s -X POST http://localhost:$PORT/ -d '{
  "command": "batch",
  "args": {
    "stopOnError": true,
    "commands": [
      {"command": "create", "args": {"name": "Sun", "primitiveType": "Sphere"}},
      {"command": "set-transform", "args": {"id": "$0", "scale": [3,3,3]}}
    ]
  }
}' -H 'Content-Type: application/json'
```

`$N` references the `instanceId` returned by the Nth command in the batch.

## Main Thread Recovery

(v0.5.0) When a `POST /` command times out — most commonly because Unity is showing a modal dialog or background-throttling a domain reload — the listener-thread `GET` endpoints can drag Unity back into a working state without depending on the stuck main thread:

| Endpoint | Effect | Platform |
|---|---|---|
| `GET /ping` | Listener thread pong (proves the server is alive) | All |
| `GET /health` | Returns queue depth + time since last main-thread tick + `mainThreadBlocked` heuristic | All |
| `GET /focus-unity` | `SetForegroundWindow` on Unity's main window — unsticks background-throttled operations | Windows only |
| `GET /dismiss-dialog` | Posts `WM_CLOSE` to the foreground dialog owned by Unity | Windows only |

Recovery flow:

```bash
curl -s http://localhost:$PORT/ping            # listener alive?
curl -s http://localhost:$PORT/health          # mainThreadBlocked: true/false?
curl -s http://localhost:$PORT/focus-unity     # background throttled? bring Unity to front
curl -s http://localhost:$PORT/dismiss-dialog  # modal dialog? close it
```

This is **the** differentiator vs. other Unity bridges. They all queue commands on the main thread that's already stuck and hang until they hit a timeout. tykit's listener thread stays alive throughout.

## CLI Tool

The included `Scripts~/unity-eval.sh` provides a convenient CLI:

```bash
./unity-eval.sh hierarchy                        # Scene hierarchy
./unity-eval.sh find '{"name":"Ship"}'           # Find by name
./unity-eval.sh --compile                        # Compile + wait + check
./unity-eval.sh --ping                           # Health check
./unity-eval.sh --help                           # All options
```

## How It Works

tykit uses `[InitializeOnLoad]` to start an `HttpListener` on a deterministic port (hashed from project path, range 8090-8153). Commands are received on a background thread and dispatched to the Unity main thread via `EditorApplication.update`.

- `/ping` responds directly from the listener thread (no main thread needed)
- All other commands are queued and processed on the main thread
- Port info is written to `Temp/tykit.json` (port, PID, project path)
- Server stops cleanly on domain reload and editor quit

## Use with quick-question

tykit is also bundled with [quick-question](https://github.com/tykisgod/quick-question), a Claude Code plugin for Unity development. When used with qq, tykit powers auto-compilation, test execution, and scene control behind the scenes.

## Security

tykit listens on **localhost only** with **no authentication**. Any local process can send commands. This is acceptable for development machines but not suitable for shared environments without network controls. See [SECURITY.md](SECURITY.md) for details.

## License

[MIT](LICENSE) &copy; Yukang Tian

---

# 中文

Unity Editor 内的轻量 HTTP 服务器。任何 AI agent 都能通过 `curl` 控制 Unity — 编译、运行测试、Play/Stop、读取控制台、检视 GameObject。

无需 SDK。无需插件 API。无需 UI 自动化。只有 HTTP。

> 本仓库是 [`tykisgod/quick-question/packages/com.tyk.tykit`](https://github.com/tykisgod/quick-question) 的自动镜像。请勿直接编辑 — 每次推送时自动从源仓库同步。

## 安装

在 Unity 项目的 `Packages/manifest.json` 中添加一行：

```json
"com.tyk.tykit": "https://github.com/tykisgod/tykit.git"
```

打开 Unity — tykit 自动启动。端口信息保存在 `Temp/tykit.json`。

**环境要求：** Unity 2021.3+，Newtonsoft JSON（UPM 依赖自动解析）。

## 快速开始

```bash
# 获取端口
PORT=$(python3 -c "import json; print(json.load(open('Temp/tykit.json'))['port'])")

# 心跳检查
curl -s http://localhost:$PORT/ping

# 发送命令
curl -s -X POST http://localhost:$PORT/ \
  -d '{"command":"status"}' -H 'Content-Type: application/json'
```

## 两个 HTTP 通道

tykit 在单个端口上暴露两条并行命令通道：

| 通道 | 端点 | 运行线程 |
|---|---|---|
| **主线程队列** | `POST /` | Unity 主线程（通过 `EditorApplication.update` 排队） |
| **监听线程直通** | `GET /ping`、`/health`、`/focus-unity`、`/dismiss-dialog` | 监听线程（永远不阻塞在主线程上） |

监听线程端点（v0.5.0）让你能在主线程被卡住时恢复——参见[主线程恢复](#主线程恢复)章节。

## API 参考

### 诊断

| 命令 | 参数 | 说明 |
|------|------|------|
| `status` | — | Editor 状态概览（isPlaying、isCompiling、activeScene） |
| `commands` | — | 列出所有已注册命令 |
| `compile-status` | — | 当前编译状态 |
| `get-compile-result` | — | 最近一次编译结果（错误和耗时） |

### 编译与测试

| 命令 | 参数 | 说明 |
|------|------|------|
| `compile` | — | 触发编译 |
| `run-tests` | `mode`、`filter`、`assemblyNames` | 启动 EditMode/PlayMode 测试 |
| `get-test-result` | `runId`（可选） | 查询测试结果 |

### 控制台

| 命令 | 参数 | 说明 |
|------|------|------|
| `console` | `count`、`filter` | 读取最近控制台日志 |
| `clear-console` | — | 清空控制台缓冲区 |

### 场景与层级

| 命令 | 参数 | 说明 |
|------|------|------|
| `find` | `name` / `type` / `tag` / `parentId` / `path` / `includeInactive` | 查找 GameObject（v0.4 增加子树范围与 inactive 支持） |
| `select` | `id` / `ids`（多选）/ `ping` | 在编辑器中选中对象；v0.4 增加多选 |
| `ping` | `id` / `assetPath` | 高亮但不选中（v0.4） |
| `inspect` | `id` / `name` | 检视组件，v0.3 起返回 `children` 数组 |
| `hierarchy` | `depth` / `id` / `path` / `name` | 场景层级树，可选指定子树（v0.3） |

### GameObject 生命周期

| 命令 | 参数 | 说明 |
|------|------|------|
| `create` | `name`、`primitiveType`、`position` | 创建 GameObject |
| `instantiate` | `prefab`、`name` | 实例化预制体 |
| `destroy` | `id` | 销毁 GameObject |
| `set-transform` | `id`、`position`、`rotation`、`scale` | 修改 Transform |
| `set-name` | `id`、`name` | 重命名 GameObject（v0.3） |

### 组件

| 命令 | 参数 | 说明 |
|------|------|------|
| `add-component` | `id`、`component` | 添加组件 |
| `component-copy` | `id`、`component` | 通过 `ComponentUtility` 复制组件值（v0.5） |
| `component-paste` | `id`、`component` / `asNew` | 粘贴组件，或作为新组件添加（v0.5） |

### 属性（序列化）

| 命令 | 参数 | 说明 |
|------|------|------|
| `get-properties` | `id` / `structured: true` | 列出序列化属性；`structured` 返回原生 JSON 类型（v0.3） |
| `set-property` | `id`、`component`、`property`、`value` | 设置序列化属性——v0.3 起接受 `Vector*`/`Quaternion`/`Color`/`Rect`/`Bounds` 的原生 JSON，v0.5 起支持 `LayerMask`/`ArraySize` |

### 反射（代码层）—— v0.4

> 完全绕过 SerializedProperty。沿类型层级查找继承的私有成员。让"AI 跑运行时测试"无需在项目中预埋脚手架代码。

| 命令 | 参数 | 说明 |
|------|------|------|
| `call-method` | `id`、`component`、`method`、`parameters` | 通过反射调用任意 public/non-public 方法 |
| `get-field` | `id`、`component`、`field` | 读取代码层字段或属性 |
| `set-field` | `id`、`component`、`field`、`value` | 写入代码层字段或属性 |

### 数组

| 命令 | 参数 | 说明 |
|------|------|------|
| `get-array` | `id`、`component`、`property` | 读取整个序列化数组/列表为结构化 JSON（v0.4） |
| `array-size` / `array-insert` / `array-delete` | — | 读/设大小、按索引插入、删除元素（v0.3） |
| `array-move` | `from`、`to` | 通过 `MoveArrayElement` 重排（v0.4） |

### 预制体（v0.5）

| 命令 | 参数 | 说明 |
|------|------|------|
| `prefab-apply` / `prefab-revert` | `id` | 应用或回滚场景修改到源预制体资源 |
| `prefab-open` / `prefab-close` | `path` / `save` | 进入 / 退出预制体编辑模式 |
| `prefab-source` | `id` | 获取实例的源预制体资源路径 |

### 物理查询（v0.5）

| 命令 | 参数 | 说明 |
|------|------|------|
| `raycast` | `origin`、`direction`、`maxDistance`、`layerMask` | 单次射线检测 |
| `raycast-all` | 同上 | 返回射线上所有命中 |
| `overlap-sphere` | `position`、`radius`、`layerMask` | 与球体相交的所有碰撞器 |

### 资源（v0.5）

| 命令 | 参数 | 说明 |
|------|------|------|
| `find-assets` | `type`、`folder`、`name` | `AssetDatabase.FindAssets` 包装 |
| `create-scriptable-object` | `type`、`path` | 创建并保存 `ScriptableObject` 实例为项目资源 |
| `load-asset` | `path` | 按路径解析资源 |
| `refresh` | — | `AssetDatabase.Refresh()` |

### UI

| 命令 | 参数 | 说明 |
|------|------|------|
| `set-text` | `id`、`text`、`inChildren` | 直接设置 `TMP_Text` / `TextMeshProUGUI` / `Text` 的文本（v0.3） |
| `button-click` | `id` | 通过 `onClick.Invoke()` 模拟按钮点击（v0.5） |

### Editor 控制

| 命令 | 参数 | 说明 |
|------|------|------|
| `play` | — | 进入 Play Mode（v0.5 起会先自动保存脏场景） |
| `stop` / `pause` | — | Play Mode 控制 |
| `save-scene` | — | 保存当前场景 |
| `save-scene-as` | `path` | 另存活动场景到新路径（v0.5） |
| `set-active-scene` | `path` / `name` | 在多场景设置中切换活动场景（v0.5） |
| `open-scene` | `path` | 按资源路径打开场景（v0.5 起会先自动保存脏场景） |
| `menu` | `item` | 执行菜单项 |
| `focus-unity` / `dismiss-dialog` | — | Windows 专属主线程版本——监听线程版本见 Recovery 章节（v0.5） |

### Prefs（v0.5）

| 命令 | 参数 | 说明 |
|------|------|------|
| `editor-prefs` | `action: get/set/delete`、`key`、`value` | 读/写/删除 `EditorPrefs` |
| `player-prefs` | 同上 | 读/写/删除 `PlayerPrefs` |

### 批量执行

一次 HTTP 往返执行多个命令——把 30+ 次调用降到 1 次：

```bash
curl -s -X POST http://localhost:$PORT/ -d '{
  "command": "batch",
  "args": {
    "stopOnError": true,
    "commands": [
      {"command": "create", "args": {"name": "Sun", "primitiveType": "Sphere"}},
      {"command": "set-transform", "args": {"id": "$0", "scale": [3,3,3]}}
    ]
  }
}' -H 'Content-Type: application/json'
```

`$N` 引用批量中第 N 个命令返回的 `instanceId`。

## 主线程恢复

（v0.5.0）当 `POST /` 命令超时——最常见的原因是 Unity 弹出 modal 对话框，或者后台节流的 domain reload——监听线程的 `GET` 端点可以在不依赖被卡住的主线程的情况下把 Unity 拉回工作状态：

| 端点 | 效果 | 平台 |
|---|---|---|
| `GET /ping` | 监听线程 pong（证明服务器还活着） | 全平台 |
| `GET /health` | 返回队列深度 + 距离上次主线程 tick 的时间 + `mainThreadBlocked` 启发式判断 | 全平台 |
| `GET /focus-unity` | 对 Unity 主窗口调用 `SetForegroundWindow`——解除后台节流的卡住操作 | 仅 Windows |
| `GET /dismiss-dialog` | 向 Unity 拥有的前台对话框发送 `WM_CLOSE` | 仅 Windows |

恢复流程：

```bash
curl -s http://localhost:$PORT/ping            # 监听线程还活着吗？
curl -s http://localhost:$PORT/health          # mainThreadBlocked: true/false?
curl -s http://localhost:$PORT/focus-unity     # 后台节流？把 Unity 拉到前台
curl -s http://localhost:$PORT/dismiss-dialog  # modal 对话框？关掉它
```

**这是 tykit 与其他 Unity 桥接最关键的差异**。其他桥接都把命令排进已经卡住的主线程队列，然后一直 hang 直到 timeout。tykit 的监听线程始终活着。

## CLI 工具

内置 `Scripts~/unity-eval.sh` 命令行工具：

```bash
./unity-eval.sh hierarchy                        # 场景层级
./unity-eval.sh find '{"name":"Ship"}'           # 按名称查找
./unity-eval.sh --compile                        # 编译 + 等待 + 检查
./unity-eval.sh --ping                           # 健康检查
./unity-eval.sh --help                           # 所有选项
```

## 工作原理

tykit 通过 `[InitializeOnLoad]` 启动 `HttpListener`，端口由项目路径哈希确定（范围 8090-8153）。命令在后台线程接收，通过 `EditorApplication.update` 分发到 Unity 主线程。

- `/ping` 直接在监听线程响应（不需要主线程）
- 所有其他命令排队后在主线程处理
- 端口信息写入 `Temp/tykit.json`（port, PID, project path）
- Domain reload 和 Editor 退出时自动清理

## 配合 quick-question 使用

tykit 也随 [quick-question](https://github.com/tykisgod/quick-question)（Claude Code Unity 开发插件）一起分发。配合 qq 使用时，tykit 在后台驱动自动编译、测试执行和场景控制。

## 安全

tykit 仅监听 **localhost**，**无认证**。本机任何进程都可以发送命令。这对开发机器是可接受的，但不适用于未做网络管控的共享环境。详见 [SECURITY.md](SECURITY.md)。

## 许可证

[MIT](LICENSE) &copy; Yukang Tian

---

# 日本語

Unity Editor 内部の軽量 HTTP サーバー。あらゆる AI エージェントが `curl` で Unity を制御 — コンパイル、テスト実行、Play/Stop、コンソール読取、GameObject 検査。

SDK 不要。プラグイン API 不要。UI オートメーション不要。HTTP だけ。

> このリポジトリは [`tykisgod/quick-question/packages/com.tyk.tykit`](https://github.com/tykisgod/quick-question) の自動ミラーです。直接編集しないでください — プッシュ時にソースから自動同期されます。

## インストール

Unity プロジェクトの `Packages/manifest.json` に1行追加：

```json
"com.tyk.tykit": "https://github.com/tykisgod/tykit.git"
```

Unity を開く — tykit が自動起動。ポート情報は `Temp/tykit.json` に保存。

**要件：** Unity 2021.3+、Newtonsoft JSON（UPM 依存関係で自動解決）。

## クイックスタート

```bash
# ポート取得
PORT=$(python3 -c "import json; print(json.load(open('Temp/tykit.json'))['port'])")

# ヘルスチェック
curl -s http://localhost:$PORT/ping

# コマンド送信
curl -s -X POST http://localhost:$PORT/ \
  -d '{"command":"status"}' -H 'Content-Type: application/json'
```

## API リファレンス

完全なリファレンスは英語/中文セクション参照。tykit v0.5.0 は **60+ コマンド**を提供：

| カテゴリ | 主なコマンド |
|---|---|
| **診断** | `status`、`commands`、`compile-status`、`get-compile-result` |
| **コンパイル & テスト** | `compile`、`run-tests`、`get-test-result` |
| **コンソール** | `console`、`clear-console` |
| **シーン & 階層** | `find`、`select`、`ping`、`inspect`、`hierarchy` |
| **GameObject ライフサイクル** | `create`、`instantiate`、`destroy`、`set-transform`、`set-name` |
| **コンポーネント** | `add-component`、`component-copy`、`component-paste`（v0.5） |
| **プロパティ（シリアライズ）** | `get-properties`、`set-property` |
| **リフレクション**（v0.4） | `call-method`、`get-field`、`set-field` — シリアライズプロパティをバイパスしてランタイムテストを可能に |
| **配列** | `get-array`、`array-size`、`array-insert`、`array-delete`、`array-move` |
| **プレハブ**（v0.5） | `prefab-apply`、`prefab-revert`、`prefab-open`、`prefab-close`、`prefab-source` |
| **物理クエリ**（v0.5） | `raycast`、`raycast-all`、`overlap-sphere` |
| **アセット**（v0.5） | `find-assets`、`create-scriptable-object`、`load-asset`、`refresh` |
| **UI** | `set-text`（v0.3）、`button-click`（v0.5） |
| **Editor 制御** | `play`、`stop`、`pause`、`save-scene`、`save-scene-as`、`set-active-scene`、`open-scene`、`menu` |
| **Prefs**（v0.5） | `editor-prefs`、`player-prefs` |
| **バッチ** | `batch` — 複数コマンドを 1 回の HTTP ラウンドトリップで実行 |

## 主スレッド復旧（v0.5）

Unity の主スレッドが modal ダイアログや domain reload で**ブロックされた時**、tykit のリスナースレッド `GET` エンドポイントが復旧手段を提供します。これは他の Unity ブリッジが死ぬシナリオで、tykit だけが生き残ります：

```bash
curl -s http://localhost:$PORT/ping            # リスナースレッドは生きているか？
curl -s http://localhost:$PORT/health          # mainThreadBlocked: true/false?
curl -s http://localhost:$PORT/focus-unity     # 背景スロットリング？Unity を前面に
curl -s http://localhost:$PORT/dismiss-dialog  # modal? 閉じる
```

`/focus-unity` と `/dismiss-dialog` は Windows のみ。`/ping` と `/health` は全プラットフォーム対応。

## quick-question との連携

tykit は [quick-question](https://github.com/tykisgod/quick-question)（Claude Code Unity 開発プラグイン）にもバンドルされています。qq と併用すると、自動コンパイル・テスト実行・シーン制御を裏側で担います。

## セキュリティ

tykit は **localhost のみ**でリスン、**認証なし**。詳細は [SECURITY.md](SECURITY.md) を参照。

## ライセンス

[MIT](LICENSE) &copy; Yukang Tian

---

# 한국어

Unity Editor 내부의 경량 HTTP 서버. 어떤 AI 에이전트든 `curl`로 Unity를 제어 — 컴파일, 테스트 실행, Play/Stop, 콘솔 읽기, GameObject 검사.

SDK 불필요. 플러그인 API 불필요. UI 자동화 불필요. HTTP만.

> 이 저장소는 [`tykisgod/quick-question/packages/com.tyk.tykit`](https://github.com/tykisgod/quick-question)의 자동 미러입니다. 직접 편집하지 마세요 — 푸시 시 소스에서 자동 동기화됩니다.

## 설치

Unity 프로젝트의 `Packages/manifest.json`에 한 줄 추가:

```json
"com.tyk.tykit": "https://github.com/tykisgod/tykit.git"
```

Unity 열기 — tykit이 자동 시작. 포트 정보는 `Temp/tykit.json`에 저장.

**요구 사항:** Unity 2021.3+, Newtonsoft JSON (UPM 의존성으로 자동 해결).

## 빠른 시작

```bash
# 포트 가져오기
PORT=$(python3 -c "import json; print(json.load(open('Temp/tykit.json'))['port'])")

# 헬스 체크
curl -s http://localhost:$PORT/ping

# 명령 전송
curl -s -X POST http://localhost:$PORT/ \
  -d '{"command":"status"}' -H 'Content-Type: application/json'
```

## API 레퍼런스

전체 레퍼런스는 영어/中文 섹션 참조. tykit v0.5.0은 **60+ 명령**을 제공:

| 카테고리 | 주요 명령 |
|---|---|
| **진단** | `status`, `commands`, `compile-status`, `get-compile-result` |
| **컴파일 & 테스트** | `compile`, `run-tests`, `get-test-result` |
| **콘솔** | `console`, `clear-console` |
| **씬 & 계층** | `find`, `select`, `ping`, `inspect`, `hierarchy` |
| **GameObject 라이프사이클** | `create`, `instantiate`, `destroy`, `set-transform`, `set-name` |
| **컴포넌트** | `add-component`, `component-copy`, `component-paste` (v0.5) |
| **프로퍼티 (직렬화)** | `get-properties`, `set-property` |
| **리플렉션** (v0.4) | `call-method`, `get-field`, `set-field` — 직렬화 프로퍼티를 우회해 런타임 테스트를 가능하게 |
| **배열** | `get-array`, `array-size`, `array-insert`, `array-delete`, `array-move` |
| **프리팹** (v0.5) | `prefab-apply`, `prefab-revert`, `prefab-open`, `prefab-close`, `prefab-source` |
| **물리 쿼리** (v0.5) | `raycast`, `raycast-all`, `overlap-sphere` |
| **에셋** (v0.5) | `find-assets`, `create-scriptable-object`, `load-asset`, `refresh` |
| **UI** | `set-text` (v0.3), `button-click` (v0.5) |
| **Editor 제어** | `play`, `stop`, `pause`, `save-scene`, `save-scene-as`, `set-active-scene`, `open-scene`, `menu` |
| **Prefs** (v0.5) | `editor-prefs`, `player-prefs` |
| **배치** | `batch` — 여러 명령을 1번의 HTTP 라운드트립으로 실행 |

## 메인 스레드 복구 (v0.5)

Unity 메인 스레드가 모달 다이얼로그 또는 domain reload로 **블록되었을 때**, tykit의 리스너 스레드 `GET` 엔드포인트가 복구 수단을 제공합니다. 이것은 다른 Unity 브리지가 죽는 시나리오에서 tykit만 살아남는 차이점입니다:

```bash
curl -s http://localhost:$PORT/ping            # 리스너 스레드 살아있나?
curl -s http://localhost:$PORT/health          # mainThreadBlocked: true/false?
curl -s http://localhost:$PORT/focus-unity     # 백그라운드 스로틀링? Unity를 앞으로
curl -s http://localhost:$PORT/dismiss-dialog  # 모달? 닫기
```

`/focus-unity`와 `/dismiss-dialog`는 Windows 전용. `/ping`과 `/health`는 모든 플랫폼 지원.

## quick-question과 함께 사용

tykit은 [quick-question](https://github.com/tykisgod/quick-question) (Claude Code Unity 개발 플러그인)에도 번들됩니다. qq와 함께 사용하면 자동 컴파일, 테스트 실행, 씬 제어를 백그라운드에서 처리합니다.

## 보안

tykit은 **localhost만** 수신, **인증 없음**. 자세한 내용은 [SECURITY.md](SECURITY.md) 참조.

## 라이선스

[MIT](LICENSE) &copy; Yukang Tian
