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

**当前配置：Layer 模式，GlobleBlur（第 9 层）；模糊强度与扩散参数以 VR_Renderer Inspector 为准。Occluder Layer Mask 已设为 Ground（第 10 层）。**

## 参数

| 参数 | 含义 |
|---|---|
| Target | Full Screen 柔化整个画面；Layer 只柔化指定层 |
| Layer Mask | Layer 模式使用的层 |
| Occluder Layer Mask | 能遮挡模糊的层；当前为 Ground。Nothing 恢复原来的置顶叠加 |
| Occlusion Depth Bias | 遮挡深度容差（米），默认 0.005 |
| Iterations | 金字塔级数 1–20。每多一级有效半径翻倍，开销只增加上一级的四分之一 |
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
- **Layer 模式可选指定层遮挡。** Ground 使用自己的 `DepthOnly` 材质通道捕获深度，
  保留不透明和 Alpha Clip 轮廓；透明队列不参与遮挡。相机 Culling Mask 必须包含 Ground。
  模糊前剔除隐藏源像素，合成时再次按原始物体深度裁切，其他层仍保持置顶叠加。
- 遮挡采取保守边界：Ground 覆盖的像素若没有原始模糊物体深度，扩散光晕也会裁掉。
  因此物体前景轮廓外落在 Ground 上的光晕不会保留；这不是逐采样深度金字塔。
  Ground 以外区域仍可自由扩散。关闭遮挡可以恢复完全原始效果。
- 开启遮挡增加一次 Ground 深度绘制、一次全分辨率源过滤和一张过滤颜色缓冲。
  2026-09-22 桌面 256×256 正交相机对照验证：红色 GlobleBlur 位于 Ground 后方时中心红色从 1 降为 0；
  位于 Ground 前方、或遮挡物改为 Default 时中心红色均为 1。C# / Shader 编译通过，无 Console 错误。
  这些检查不包含 Quest 双眼与 GPU 性能验证。
- Full Screen 的 Screen 模式对 HDR 输入先做了 saturate，超过 1 的部分会被压掉；
  高动态范围场景建议用 Mix 或 Glow。
- 升采样链使用独立纹理，比原地复用多占一点显存（各级面积之和约为全屏的 1/3）。
- 全屏低对比画面在 VR 中会削弱深度线索，长时间佩戴的舒适度需要实测。

## 单物体控制：Globle Blur Override

在 GlobleBlur 图层的物体上 Add Component → TRINL → Globle Blur Override。
新增组件自动复制 VR_Renderer 当前参数，也可点击“从 Renderer 全局复制参数”。
可独立控制模糊级数、扩散范围、合成强度、混合模式、保留峰值颜色和遮挡容差。
Ground 遮挡层及渲染注入时机由全局 Feature 统一配置，Full Screen 模式忽略组件。

- 包含子物体默认开启；最近的启用组件拥有该 Renderer，子级组件优先。
- 停用组件恢复父级覆盖或全局设置；合成强度为 0 则隐藏本组，不回退全局。
- 无组件的对象保持全局参数；全局强度为 0 时，非零强度的组件仍可显示。
- 支持 MeshRenderer 和 SkinnedMeshRenderer；不修改原始材质或物体 Layer。
- 每个独立组增加捕获、所有权遮罩和模糊合成，多个物体建议共用父级组件。
  分组按启用顺序叠加，组之间扩散区域可能重叠；不提供半透明物体的逐层深度分解。

桌面 256×256 渲染对照通过：单个物体改变强度时另一物体保持不变；强度为 0 隐藏；
停用恢复全局；全局强度为 0 时独立组件仍显示；Ground 可遮挡独立组。
C# 与两个 Shader 编译通过，无 Console 错误。Quest 双眼及 GPU 性能未验证。

### 所有权遮罩修正

所有权绘制共享 Layer 捕获的深度附件，以只读深度 + ZTest LEqual 判定可见面。
不再使用采样深度线性化后按毫米容差比较的方式；该方式在实际透视场景中曾把同一物体
误拆为全局组与独立组，低模糊时形成月牙状边缘。原先居中正交方块测试未覆盖这一情况。
已用 Camera_sceneEffectUse、SkyGradientBlurObj 进行 800×450 离屏对照，隔离其他 GlobleBlur
物体并设相同参数 iterations=2、spread=0.78、strength=0.097，开启/关闭 Override 的天空区域
（380×210 像素）差异为 0。Quest 双眼尚未实测。

最大模糊级数现为 20（全局和 Override 同步）；实际级数受最小 8×8 金字塔尺寸限制，达到该尺寸后自动停止，因此 20 不代表一定执行 20 级或比 7 更糊。已有参数不会自动改变。
