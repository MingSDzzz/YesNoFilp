# YES / NO 决策徽章（YesNoFilp）

一个隐私优先的 Android YES/NO 决策应用。输入一个问题，按住蓄力并松开，即可投掷双面徽章获得结果。

当前版本：`1.4.5`（`versionCode 16`）

[下载最新 APK](https://github.com/MingSDzzz/YesNoFilp/releases/download/v1.4.5/YesNoFilp-v1.4.5.apk) · [查看版本说明](https://github.com/MingSDzzz/YesNoFilp/releases/tag/v1.4.5)

## 使用演示

<p align="center">
  <a href="https://github.com/MingSDzzz/YesNoFilp/releases/download/v1.3.8/YesNoFilp-v1.3.8-usage-guide.mp4">
    <img src="docs/usage-demo.gif" width="320" alt="YES NO 决策徽章使用演示">
  </a>
</p>

主页会自动播放上方 15 秒操作预览。点击预览或[这里](https://github.com/MingSDzzz/YesNoFilp/releases/download/v1.3.8/YesNoFilp-v1.3.8-usage-guide.mp4)观看完整中文演示。

## 主要功能

- 问题可以填写或留空；按住蓄力，松开后投掷带有 YES/NO 两面的动态徽章。
- 蓄力 3 秒达到满力，继续按住不会自动投掷；投掷结束前不能重复操作。
- 支持真实触摸压力；设备不支持时，使用按住时间、触摸面积和松开动作计算力度。
- 内置可直接使用的默认徽章；支持创建多个自定义徽章并上传、裁切 YES 和 NO 两面图片。
- 支持修改徽章名称、拖动排序、切换当前徽章，以及查看使用次数和结果比例。
- 每个徽章可设置 0%–100% 的 YES 基础概率，默认 50%；0% 必定 NO，100% 必定 YES。
- 支持单次决定、3 局 2 胜和 5 局 3 胜，多局模式会同时展示多枚徽章。
- 投掷结束后选择是否保存；历史记录支持备注、筛选和单独删除。
- 已保存记录支持带版本号的 JSON 导入与导出，导入前可预览并选择合并或替换。
- 支持上传并裁切竖屏背景图；设置页可分别调节背景图片和界面底板的不透明度。
- 提供日间与夜间主题，适配 Android 竖屏、刘海屏和安全区域。

## 基本使用

1. 在“投掷”页选择徽章，可按需输入问题。
2. 按住投掷按钮蓄力，松开后等待徽章落地。
3. 在结果弹窗中填写可选备注，并选择保存或不保存。
4. 在“徽章”页创建、编辑、切换或排序徽章。
5. 在“记录”页查看历史，在“设置”页导入、导出数据或操作日志。

## 隐私与本地数据

- 当前问题、未保存结果和操作日志默认只存在于内存中。
- 只有用户明确保存的结果才会进入历史记录。
- 历史和徽章图片副本保存在 `Application.persistentDataPath`。
- 上传的徽章图片会复制到应用目录，删除手机中的原始图片不会影响已创建徽章。
- 应用不会申请与核心功能无关的 Android 权限。

## 开发与构建

项目使用 Unity `2022.3.62f2c1`，提供以下编辑器菜单：

- `Tools/Decision Disc/Setup Android`
- `Tools/Decision Disc/Build APK`

构建脚本会检查 Android Build Support、SDK、NDK 和 OpenJDK，并生成带版本号的 APK。正式 APK 使用本地项目签名；签名文件和密码配置均被 Git 忽略，不包含在公开仓库中。
