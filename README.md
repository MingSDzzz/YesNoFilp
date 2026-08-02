# YES / NO 决策徽章（YesNoFilp）

一个使用 Unity `2022.3.62f2c1` 从零制作、隐私优先的 Android 个人决策应用。

当前 Android 版本：`1.2.0`（`versionCode 3`）。

## 主要功能

- 四个 Android 竖屏页面：投掷、徽章管理、历史记录和设置。
- 输入本次要决定的问题，按住按钮蓄力，松开后投掷带有 YES/NO 两面的动态圆盘。
- Android 设备支持触摸压力时读取真实 `pressure`；不支持时使用按住时间、触摸面积和松开速度模拟力度。
- **公平 50/50 模式**：力度会参与随机熵，但不会偏向 YES 或 NO。
- **力度影响概率模式**：每个徽章可以独立设置 0%–100% 的 YES 基础概率，默认 50%；力度会在基础概率上调整，但 0% 始终必定 NO，100% 始终必定 YES。
- 首页显示当前徽章，可进入徽章页快速切换。
- 创建徽章时先输入名称，随后在列表中分别上传或更新 YES 面与 NO 面。
- 自定义徽章必须补齐 YES/NO 两面才能用于投掷，并支持改名、预览、切换、修改概率和删除。
- 投掷动画会交替展示当前徽章真实的 YES 面与 NO 面。
- 当前问题和未保存结果默认只存在内存中，不会自动写入本地文件。
- 历史页同时显示本次使用的内存记录和用户明确保存的永久记录。
- 已保存记录包含问题、YES/NO、力度、实际 YES 概率、力度来源、模式、时间、可选备注和徽章信息。
- 支持删除历史、导出带版本号的 JSON，以及通过 Android 系统文件选择器导入、预览、合并或替换。
- 设置页支持预览和导出用户操作日志。日志默认只在内存中，只有用户点击导出时才写入文件。
- UI 适配 Android 竖屏、安全区域和刘海屏。

## 隐私与本地数据

运行数据保存在 `Application.persistentDataPath` 下：

- `history-v1.json`：只有用户明确点击“保存本次记录”后才写入的历史记录。
- `badges-v1.json`：徽章名称、选中状态和概率等元数据。
- `Badges/<badge-id>/yes.*`：YES 面图片的应用内部副本。
- `Badges/<badge-id>/no.*`：NO 面图片的应用内部副本。

用户选择徽章图片后，应用会把图片复制到自己的持久化目录。因此即使原始图片从手机相册或文件夹中删除，应用内的徽章仍然可以继续使用。

当前问题、未保存的投掷结果和操作日志不会自动持久化。

## 随机概率规则

### 公平 50/50

- YES 与 NO 始终各占 50%。
- 力度只混入随机输入，不会改变两面的概率。

### 力度影响概率

- 每个徽章具有独立的 YES 基础概率，范围为 0%–100%，默认 50%。
- 力度会围绕基础概率调整最终 YES 概率。
- 基础概率为 0% 时必定得到 NO。
- 基础概率为 100% 时必定得到 YES。

## Unity 编辑器菜单

- `Tools/Decision Disc/Setup Android`：设置竖屏方向、包名、最低 Android API、IL2CPP/ARM64，并创建运行场景。
- `Tools/Decision Disc/Build APK`：检查 Android Build Support、SDK、NDK 和 OpenJDK，然后构建 `Builds/YesNoFilp.apk`。

如果构建组件缺失，命令会明确列出缺少的组件。只有目标 APK 确实存在且文件大小大于零时，才会报告构建成功。

## Android 签名

Android 构建使用项目专用的本地签名文件：

- 签名配置：`.signing/signing.local.json`
- 签名密钥：`.signing/YesNoFilp.keystore`

整个 `.signing` 目录已被 Git 忽略，因为其中包含升级签名所需的私密信息。请安全备份该目录；未来 APK 必须继续使用同一签名，才能覆盖安装和保留应用数据。

旧的默认调试签名版本无法被项目专用签名版本直接覆盖，需要先卸载旧版本。使用项目专用签名安装后，后续版本可以正常覆盖升级。

## 命令行验证与构建

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe' `
  -batchmode -quit -projectPath . `
  -executeMethod DecisionDisc.Editor.DecisionDiscBuild.ValidateProject `
  -logFile validate.log

& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe' `
  -batchmode -quit -projectPath . `
  -executeMethod DecisionDisc.Editor.DecisionDiscBuild.BuildApk `
  -logFile build-android.log
```

## JSON 格式

历史导出文件包含：

- `format`：文件类型标识。
- `version`：整数格式版本。
- `exportedAtUtc`：导出时间。
- `records`：已保存记录数组。

当前支持导入版本 `1`。应用会在修改任何本地数据之前检查文件、显示预览，并要求用户选择“合并”或“替换”；不支持的版本会被拒绝。

## 项目约束

- 本项目完全独立，不读取、复制或修改 `external-local-project` 项目的任何内容。
- Unity 版本固定为 `2022.3.62f2c1`。
- 不提交 `Library`、`Logs`、`UserSettings`、APK、签名密钥或其他生成产物。
