# ScreenBlur — Cube 基础版

已安装在 `Assets/Settings/VR_Renderer.asset` 的 **ScreenBlur (Gaussian)**。

- Layer Mask：ScreenBlur（第 7 层）。Camera Culling Mask 仍需包含此层。
- Radius：范围 0–100，高斯 sigma，单位为渲染像素，默认 4；0 为无模糊。核覆盖正负 3 sigma。100 时每个方向有 601 个采样位置，开销较高，尤其是 VR 双眼；尚未实现降采样优化。
- Opacity：合成不透明度，默认 0.85；1 完全显示，0 完全隐藏。
- Depth Bias：遮挡比较容差，单位为相机空间距离，默认 0.005。

保留对象原始 URP/Lit 材质及光照。主渲染器的 Opaque Layer Mask 和 Prepass Layer Mask 排除 ScreenBlur，由 Render Graph 单独绘制该层的不透明对象到透明清底的 HDR 颜色和深度缓冲。横向模糊输出预乘颜色与最近贡献深度，纵向模糊同时以 `One / OneMinusSrcAlpha` 合成到场景。深度采样拒绝被前景遮挡的颜色，并限制模糊向前景扩散。

主画面保留 4× MSAA，模糊缓冲单采样。继承相机纹理维度、眼睛切片与动态分辨率标记，使用 `TEXTURE2D_X`、stereo eye 初始化及 Blitter。实现采用 Unity Render Graph 的 Renderer List 工作流，参见 [Unity 官方说明](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-draw-objects-in-a-pass.html)。

## 已验证

- Unity 编辑器 C# 与 Shader 编译通过。
- 当前场景 Cube 边缘模糊，其他层保持清晰，原 illuminiMat / URP Lit 未修改。
- 临时前景板完全遮挡和与 Cube 相交两种位置的画面检查；临时物体已移除。
- 当前活动 VR_RPAsset 的 MSAA 仍为 4。

## 当前范围

这是单个不透明 Cube 的基础版，尚未进行头显双眼、设备构建或 GPU 性能验证。较大 Radius 会增加采样开销；未实现降采样优化。多个不同深度模糊物体重叠时，横向最近深度是近似处理。透明材质不纳入此模糊层绘制，普通透明物体仍在此效果之后绘制，复杂透明排序暂不支持。效果不把 Cube 写回场景深度，后续依赖场景深度的效果不会看到它；原有阴影投射仍保留。

## 关闭或回退

需要恢复普通渲染时，关闭或移除 Renderer Feature，并在 VR_Renderer 的 **Opaque Layer Mask 和 Prepass Layer Mask** 中重新勾选 ScreenBlur。只关闭 Feature 而不恢复层过滤会使该层的不透明对象消失。

## 表面噪波（2026-09-23）

在 VR_Renderer 的 ScreenBlur Feature 中设置全局噪波。Screen Blur Override 默认继承这组设置；勾选“独立控制噪波”后可为该覆盖组独立控制所有噪波参数。

- Enable Noise：噪波开关。Noise Strength 为 0 时也关闭采样效果。
- Noise Placement / Before Blur：先对独立渲染的原材质颜色加噪波，再横纵模糊；噪波会一起柔化。
- Noise Placement / After Blur：完成模糊后再调制 RGB，保留较清晰噪波；仍在透明合成及后处理之前。
- Noise Size：模型最长局部尺寸的比例，0.2 约为沿最长轴 5 个噪波单元；越大颗粒越大。无需按 TinyFlower 的 100 倍缩放换算。
- Noise Strength：0–1，围绕原亮度做乘法调制，不修改 alpha。
- Noise Texture：可指定灰度贴图（建议关闭 sRGB）；留空使用内置三维程序噪波。自定义贴图采用局部三平面投影。
- Animate Noise / Noise Velocity：开关流动及三轴速度，速度单位是模型归一化尺寸每秒。

建议对比：Radius 8、Noise Size 0.12、Noise Strength 0.7、Animate Noise 关闭，切换 Before Blur / After Blur。此为试调建议，没有覆盖你当前 Radius 或场景设置。

实现复用原物体所有权绘制，同时输出表面噪波；模糊后模式在软轮廓之外延续最近的有效噪波样本，属于边缘延伸近似。大半径、物体重叠或深度断层处需要观察是否出现拉伸。透明多层仍使用现有的代表前表面深度，不能区分所有透明层。

增加表面绘制、缓冲和逐相机 Renderer 查找，有额外 CPU/GPU 成本。没有改变原材质资源。刚体平移/旋转跟随局部坐标；骨骼变形、静态合批、头显双眼和性能尚未验证。

验证：相关 C# 已使用当前工程的 Unity/URP 程序集单独编译通过。当前会话未连接 Unity 控制工具，尚未完成 Unity Shader 编译、两种模式截图或头显实测。返回 Unity 待导入完成后，在上述下拉框直接比较；必要时使用 Assets > Refresh。
### 逐物体噪波覆盖

在 Screen Blur Override 中勾选“独立控制噪波”：
- “启用噪波”可以只关闭这个物体的噪波，也可以在全局噪波关闭时单独开启。
- 每组可使用不同的 Before Blur / After Blur、贴图、大小、强度、流动和速度。
- 不勾选时实时继承 Renderer 全局噪波。灰色字段仅为保存的本地参数。
- “包含子物体”沿用现有覆盖组规则；最近的启用 Override 拥有该 Renderer。子组件未独立控制噪波时继承 Feature 全局值，而非父组件的噪波字段。
- “从 Renderer 全局复制参数”同时复制噪波数值，不改变“独立控制噪波”开关。
### 相机颗粒 / 局部过曝

新增独立的 Camera Grain 模块，使用屏幕渲染像素坐标生成动态颗粒，位于横纵模糊和表面噪波之后、场景透明合成之前。无需噪波贴图。

全局默认试用值：Enable Camera Grain 开启、Strength 0.18、Size 1.5 像素、Color 0.15、Speed 24 次/秒、Exposure +1 EV、Clip Highlights 开启。没有改变场景物体、原材质或相机的全局曝光设置。

Screen Blur Override 中勾选“独立控制相机颗粒”，即可单独开启/关闭并调整所有这些参数；不勾选则继承 Renderer 全局值。它和“独立控制噪波”是两个独立模块。要只观察 ISO 颗粒，可将该物体的表面噪波关闭。

- 颗粒强度为 0、曝光为 0、关闭高光截白，可作为启用模块时的无处理基准；直接关闭模块则完全跳过。
- Size 按实际渲染像素计，Render Scale 改变会影响最终显示尺寸。
- Speed=0 固定颗粒；非零时按指定频率更新。编辑器中观察动态需要动画刷新或 Play Mode。
- 曝光只增加本组颜色，+1 EV 为两倍亮度；高光截白模拟高亮区域丢失细节。
- 不改变 alpha；颗粒重新乘覆盖率，随模糊软边缘与 Opacity 一起淡出。零覆盖像素不加噪点。
- 如果相机另有 Bloom/Tonemapping，后续处理仍可能改变颗粒或将亮部泛光扩散到周围。此模块不是在最终相机画面上执行全屏颗粒。
- 这是摄影风格的模拟，并非真实传感器 ISO 标定。VR 双眼、时间抗锯齿、后处理及实际性能尚未实测。

本次相关运行代码与 Inspector 使用现有 Unity/URP 程序集编译通过；没有连接 Unity 控制工具，Shader 编译和画面对比仍待编辑器验证。