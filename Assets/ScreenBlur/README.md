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
