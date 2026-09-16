# 单物体 Glitch Blur

## 高斯模糊

Global 与 Override 都有 Gaussian Blur 区域。“模糊范围（像素）”控制标准差，
“模糊混合强度”控制原拖影与模糊拖影的混合。范围或强度为 0 时跳过额外计算。
范围以相机输出像素计，自动适配降采样和圆柱捕获分辨率。
高斯模糊在拖影生成后横向、纵向各执行一次，再应用原有 Vertical Softness。
每个本地覆盖组单独处理，沿用深度遮挡检查，不模糊整个场景背景。
“从 Renderer 全局复制参数”也会复制这两个新增属性。

勾选“强度随玩家距离变化”后，固定混合强度改由距离响应控制：
- `gaussianResponseDistance`：响应距离，默认 5 米。
- `gaussianNearStrength`：趋近 0 米时的强度，默认 0.6。
- `gaussianFarStrength`：达到或超过响应距离的强度，默认 1。
- `gaussianDistanceResponse`：开关；关闭后使用原 `gaussianStrength`。

以上都是 public 字段；Global 和 Override 各自保存，并支持从 Global 复制。
以 Main Camera 为玩家位置，按捕获到的可见拖影表面的三维距离进行 smoothstep
插值；它不是水平距离，也不是整个物体统一按 Transform 中心计算。
例如默认配置下 2.5 米对应约 0.8，5 米及以外为 1。

在物体上 Add Component → TRINL → Glitch Blur Override。
物体实际显示的 Renderer 仍需位于全局 Feature 配置的 GlitchBlur Layer。

- 编辑器首次添加时复制 VR_Renderer 当前参数，随后独立保存和调整。
- “从 Renderer 全局复制参数”可再次同步一次；之后不会跟随全局修改。
- 可单独调节平面/圆柱模式、近远距离和半径、长度、透明度、色带及采样质量。
- “包含子物体”控制作用范围；最近的已启用子组件优先。
- 停用组件恢复父级覆盖或全局设置。Opacity=0 隐藏该组，不等于停用组件。
- SpatialGhosts 生成的普通网格残影继承源 Renderer 的本地设置。
- 未挂组件的物体使用 Renderer 全局参数。无需创建额外 Layer。

对象编号按每次绘制绑定，以防不同物体的遮罩材质复用编号。
遮罩支持 MeshRenderer、SkinnedMeshRenderer、常规基础贴图及 Alpha Cutoff。
自定义顶点动画、体积雾、复杂透明叠层的遮罩不保证与源着色器完全一致。
每个独立覆盖组增加捕获与合成开销；大量物体及 VR 需检查 GPU 帧时间。

验证入口：Tools → TRINL → Validate Glitch Object Overrides。
检查两个物体的参数隔离、停用恢复、平面与圆柱混用及 SpatialGhosts 本地透明度。
结果与截图位于 Library/GlitchOverrideValidation；当前验证范围为桌面编辑器渲染。
