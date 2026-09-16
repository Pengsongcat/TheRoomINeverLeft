# ScreenDiffusion — 柔化

已加入 `Assets/Settings/VR_Renderer.asset`，位于 ScreenBlur 和 GlitchBlur 之后。
这是**独立于 ScreenBlur 的另一套东西**，两者互不影响，可以同时开。

核心是 dual filtering（Kawase）金字塔：**有效半径来自级数而不是采样数**，
所以"糊得很厉害"和"糊一点点"的开销几乎一样。

有两个模式：

| | Full Screen | Layer |
|---|---|---|
| 作用范围 | 整个相机画面 | 只有指定 Layer 上的物体 |
| 其他物件 | 全部被柔化 | **完全不受影响** |
| 合成方式 | Mix / Glow / Screen 与原图混合 | 预乘 Alpha 叠加回画面 |

**当前配置：Layer 模式，GlobleBlur（第 9 层），iterations 5、spread 1.5、strength 1、Mix。**

## 参数

| 参数 | 含义 |
|---|---|
| Target | Full Screen 柔化整个画面；Layer 只柔化指定层 |
| Layer Mask | Layer 模式使用的层 |
| Iterations | 金字塔级数 1–6。每多一级有效半径翻倍，开销只增加上一级的四分之一 |
| Spread | 每级的采样偏移倍率 0.5–4。用于在整数级数之间微调 |
| Strength | Full Screen 下是与原图的混合强度；Layer 下是合成不透明度 |
| Blend Mode | Full Screen：Mix / Glow / Screen。Layer：Mix = 正常叠加，Glow = 相加发光，Screen 回退为 Mix |
| Injection Point | 默认 BeforeRenderingPostProcessing，让已有的 Bloom、色调映射作用在柔化之后 |

Strength 为 0、或 Layer 模式下 Layer Mask 为空时，整个 Feature 直接跳过，不产生任何 pass。

## Layer 模式的两个前置条件

缺一个就看不到效果，务必检查：

1. **Renderer 的 Opaque / Transparent Layer Mask 必须排除该层**，
   否则清晰的原像也会被正常绘制，画面上会同时出现清晰和模糊两份。
   当前 VR_Renderer 已排除第 7（ScreenBlur）、8（GlitchBlur）、9（GlobleBlur）层。
2. **相机的 Culling Mask 必须包含该层**，否则物体根本不会被剔除结果收录。
   本次已给场景中的 `CenterEyeAnchor` 和 `Camera` 补上第 9 层。

## 实现

1. **Full Screen**：一次全分辨率拷贝（合成 pass 需要同时读写相机颜色，单个 pass 做不到）。
   **Layer**：把该层的不透明与透明队列画进一张**清成全透明**的 HDR 目标。
2. 逐级降采样到 1/2、1/4 … 最小 8 像素，每级 5 个双线性采样。
3. 逐级升采样回来，每级 8 个双线性采样，**使用独立的目标纹理**，
   避免同一张纹理在链路里既被读又被重写。
4. 合成。最后一次到全分辨率的升采样就是那次双线性取样本身。

Layer 模式为什么不会"越糊越淡"：捕获目标清成透明，所以线性模糊 RGBA 得到的天然就是
预乘结果，不需要重新归一化，也没有被拒绝的采样点。边缘是自然淡出，而不是像
ScreenBlur 那样因为分母包含被拒绝的权重而整体变淡。

保留 XR dimension、volumeDepth、动态分辨率与 VR usage 标记，使用 `TEXTURE2D_X` 与 `Blitter`。

## 开销估算

以 iterations=6 为例，按"全屏采样次数"折算：

- 拷贝／捕获 1 + 合成 2 = 3
- 降采样链面积之和约为全屏的 1/3，每级 5 个采样 → 约 1.7
- 升采样链面积之和约为全屏的 1/3，每级 8 个采样 → 约 2.7

合计**约 7 次全屏采样等效**。作为对比，ScreenBlur 半径 100 时是
`ceil(100×3)×2+1 = 601` 个采样点、横纵两遍，约 **1200 次**。量级差约 170 倍。

**这是静态采样计数估算，不是实测。** 见下方限制。

## 已验证

用 `CenterEyeAnchor` 的位姿建临时相机，512×512、开启后处理。

**Full Screen 模式**，指标为全图平均梯度模长（越低越糊）：

| 设置 | 残留锐度 |
|---|---|
| 关闭 | 100 % |
| Mix strength 1, iterations 1 / 2 / 3 | 96.9 % / 91.5 % / 83.7 % |
| iterations 4 / 5 / 6 | 72.5 % / 54.6 % / 34.0 % |
| iterations 4, strength 0.25 / 0.50 / 0.75 | 93.5 % / 86.6 % / 79.4 % |

**Layer 模式**（GlobleBlur，iterations 5、spread 1.5），开关对照的逐像素差异：

- 画面发生变化的像素：3408 / 262144，占 **1.3 %**
- 其余 **98.7 %** 的像素逐位相同，即场景其他物件完全未受影响
- 1920×1080 截图目视确认：只有第 9 层的物体糊开，人物、球体、植物、窗、幕布保持清晰

C# 与 Shader 编译通过，Shader 无错误消息，未见网格状 artifact。

## 限制

- **头显 GPU 帧时间、Android 构建均未验证。** 上面的开销是静态采样计数估算，
  桌面编辑器的检查不能视为 VR 验收。
- **Layer 模式没有深度遮挡。** 模糊后的结果整块叠加在注入点之前已绘制的所有内容之上，
  所以物理上位于它前面的物体不会遮住它。这是为了让模糊能向空白处自由扩散——
  ScreenBlur 的深度拒绝正是把扩散范围钉死的原因。需要遮挡的话要另加一条深度金字塔。
- Full Screen 的 Screen 模式对 HDR 输入先做了 saturate，超过 1 的部分会被压掉；
  高动态范围场景建议用 Mix 或 Glow。
- 升采样链使用独立纹理，比原地复用多占一点显存（各级面积之和约为全屏的 1/3）。
- 全屏低对比画面在 VR 中会削弱深度线索，长时间佩戴的舒适度需要实测。
