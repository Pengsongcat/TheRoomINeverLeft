# 单物体 Screen Blur

与 `GlitchBlur/LocalOverrides.md` 同一套机制：全局 Renderer Feature 提供默认参数，
挂在物体上的 **Screen Blur Override** 组件单独覆盖该物体。

在物体上 Add Component → TRINL → Screen Blur Override。
物体实际显示的 Renderer 仍需位于全局 Feature 配置的 ScreenBlur Layer（第 7 层）。

| 参数 | 含义 |
|---|---|
| 包含子物体 | 作用范围；关闭后只影响本物体自己的 Renderer |
| 模糊范围（像素） | 高斯 sigma，相机渲染像素单位，0 为无模糊。核覆盖正负 3 sigma |
| 合成不透明度 | 1 完全显示，0 隐藏该组（不等于停用组件） |
| 遮挡容差（米） | 深度比较容差，相机空间距离 |

- 编辑器首次添加时复制 VR_Renderer 当前参数，随后独立保存和调整。
- "从 Renderer 全局复制参数"可再次同步一次；之后不会跟随全局修改。
- 最近的已启用组件优先；停用组件恢复父级覆盖或全局设置。
- 未挂组件的物体使用 Feature 全局参数。无需创建额外 Layer。

## 实现

沿用 GlitchBlur 的分组方案，便于两套效果对照阅读。

1. 全局设置是 0 号组，第 i 个启用的 Override 是 i+1 号组。
2. "Object Ownership" pass 把每个被覆盖 Renderer 的组号写进一张 R16 的 ID 缓冲。
   该缓冲清 0，所以没有覆盖的物体天然留在全局组。
   组号按每次绘制通过 `_ScreenBlurMaskObjectId` 全局量绑定，缓存的遮罩材质可跨组复用。
3. 每个组各自 enqueue 一个 pass，横向高斯在采样时用 `BelongsToGroup` 丢弃不属于本组的像素。
4. 遮罩使用与源材质相同的基础贴图和 Alpha Cutoff，并做深度相等检查（按米比较，
   容差 `depth * 1e-5`），只有真正赢得该像素深度测试的表面才拥有它。

遮罩支持 MeshRenderer 与 SkinnedMeshRenderer。自定义顶点动画的遮罩不保证与源着色器完全一致。

## 已验证

在当前场景 `Cube` 与 `Cube (1)`（均在第 7 层）上做的自动检查，512×512、仅渲染第 7 层、
纯黑背景、临时平行光，指标为窗口内亮度 RMS 差异：

- 只改 `Cube` 的半径：`Cube` 窗口 RMS 1.98406，`Cube (1)` 窗口 0.05897。
- 只改 `Cube (1)` 的半径：`Cube` 窗口 RMS 0.00023，`Cube (1)` 窗口 1.94610。
- 半径 60 对比全局 20.3：峰值亮度 5.305 → 1.360，能量扩散半径 23.84 → 42.34。
- C# 与 Shader 编译通过，Shader 无错误消息。

`Cube (1)` 窗口那 0.059 不是参数泄漏，是半径 60 的模糊在屏幕上实际扩散到了邻近窗口
（两窗口中心相距 198 像素，sigma 60 的核支持约 180 像素）。反向那一组为 0.00023，
说明参数本身没有串组。

## 限制

- 与 GlitchBlur 相同，**每个独立覆盖组都会完整重跑一遍捕获、遮罩与合成**，
  组数越多开销越高。`maskDraws` 每帧重新分配。
- 大半径仍未实现降采样优化。当前 VR 配置 renderScale 1.6、双眼、MSAA 4，
  Inspector 在半径超过 24 时会给出采样数提示。**头显 GPU 帧时间尚未验证。**
- 本次验证来自桌面编辑器相机，不能视为 VR 验收；Android 构建未测。
- ID 缓冲为 R16_SFloat，组号超过 2048 会失去精度（实际用量远低于此）。
- 透明物体只使用代表性前表面深度，交叠透明物体的归属不是逐层精确解。
