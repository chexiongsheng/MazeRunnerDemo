项目附带了一个 AI 自主走迷宫的 Demo，展示 Agent 通过截屏观察 + 工具调用来探索 3D 迷宫。
也接入了Puerts助手，包括Agent版和MCP版。

## 准备工作

### 克隆仓库

本项目包含 Git submodule，克隆时请使用 `--recursive` 参数以同时拉取所有子模块：

```bash
git clone --recursive https://github.com/chexiongsheng/MazeRunnerDemo
```

如果已经克隆但忘记加 `--recursive`，可以补充初始化子模块：

```bash
git submodule update --init --recursive
```

### 获取 PuerTS 插件二进制

项目依赖的 [PuerTS](https://github.com/Tencent/puerts) submodule 中**不包含**编译好的二进制插件，需要手动从 PuerTS 的 GitHub Releases 页面下载并解压。

以 `Unity_v3.0.1` 为例，下载地址：
> https://github.com/Tencent/puerts/releases/tag/Unity_v3.0.1

需要下载以下三个插件包，并将各自的 `Plugins` 目录内容放置到对应的 UPM 包路径下：

| 插件包 | 说明 | 放置路径 |
|--------|------|----------|
| **PuerTS-Core** | PuerTS 核心插件 | `puerts/unity/upms/core/Plugins/` |
| **PuerTS-V8** | V8 引擎后端插件 | `puerts/unity/upms/v8/Plugins/` |
| **PuerTS-Nodejs** | Node.js 后端插件 | `puerts/unity/upms/nodejs/Plugins/` |

> ⚠️ **注意**：不要放到 `Assets/Plugins/`，应放到各 UPM 包自身的 `Plugins` 目录下。

## Agent版本Puerts助手

点击菜单"Puerts Agent/New Chat Window"

## MCP版本Puerts助手

点击菜单"Puerts/MCP Server"

在IDE配置

```json
{
  "mcpServers": {
    "puerts-unity-editor-assistant": {
      "url": "http://127.0.0.1:3100/sse"
    }
  }
}
```

## Demo: AI 迷宫探索

### 1. 生成迷宫场景

菜单栏 **Tools → Maze Runner → Generate Maze Scene**，打开迷宫生成器窗口：

| 参数 | 说明 | 默认值 | 范围 |
|------|------|--------|------|
| Maze Width | 迷宫宽度（格数） | 8 | 4–16 |
| Maze Height | 迷宫高度（格数） | 8 | 4–16 |
| Cell Size | 每格大小（米） | 2.0 | 1.5–4.0 |
| Wall Height | 墙壁高度（米） | 1.2 | 0.5–5.0 |
| Wall Thickness | 墙壁厚度（米） | 0.2 | 0.1–0.5 |

点击 **Generate Maze Scene** 按钮，将自动生成一个完整的迷宫场景，包含：
- 使用递归回溯算法生成的随机迷宫
- 带 CharacterController 的玩家角色（起点在左下角）
- 红色目标标记（终点在右上角）
- 俯视相机
- MazeDemoManager 和 MazeAgentUI 组件

场景保存在 `Assets/Scenes/MazeDemo.unity`。

### 2. 配置 API Key

生成场景后，在 Hierarchy 中选中 **MazeDemoManager** 对象，在 Inspector 面板中配置以下字段：

| 字段 | 说明 | 示例 |
|------|------|------|
| **Api Key** | LLM 服务的 API Key（必填） | `sk-xxxxxxxx` |
| **Base URL** | API 端点地址（兼容 OpenAI 格式，留空使用默认值） | `https://dashscope.aliyuncs.com/compatible-mode/v1` |
| **Model** | 模型名称（留空使用默认值） | `qwen-plus` |
| **Max Steps** | 最大工具调用步数，0 = 无限制 | `0` |

### 3. 构建迷宫 Demo 的 TypeScript

迷宫 Demo 有独立的 TypeScript 工程 `TsMazeRunner/`，需要单独构建：

```bash
cd TsMazeRunner
npm install
npm run build
```

构建产物输出到 `Assets/Resources/maze-runner/builtins/`。

### 4. 运行 Demo

1. 打开 `Assets/Scenes/MazeDemo.unity` 场景
2. 确认 MazeDemoManager 上已配置好 API Key
3. 点击 Unity 的 **Play** 按钮进入运行模式
4. 点击屏幕右上角的 **▶ Start Exploration** 按钮启动 AI 探索
5. AI 将自动截屏观察环境、规划路径、调用移动接口来走迷宫
6. 到达终点后会自动宣布成功

运行时还可以使用以下控制按钮：
- **⏹ Stop** — 中止当前探索
- **🔄 Reset** — 重置迷宫和对话历史
