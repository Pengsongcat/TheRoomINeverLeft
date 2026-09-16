# 朝上表面响应

Global 与 Glitch Blur Override 都有“朝上表面响应 · 世界 Y 轴”。
开启后使用模型世界空间法线与 (0,1,0) 的点积制作遮罩；不是模型高度，
也不依赖场景真实灯光。颜色仍由原材质捕获，高斯模糊及距离响应在后续阶段执行。

公开参数：
- blendBaseStretch：叠加基础拉伸开关，默认关闭，Global 与 Override 均可调整。
- baseStretchBlend：基础拉伸融合强度，默认 0.5；0 为朝向加权效果，1 为完全不使用朝向权重的基础效果，中间为两者颜色及透明度的线性混合。
  两套效果分别计算形状，不仅插值拉伸参数。圆柱、线长随机和点状叠加沿用同一组设置。
  混合在高斯模糊前完成；关闭朝向响应或开启遮罩预览时不参与。混合中间值需要额外计算基础拖影，尚未验证头显性能。
- upwardResponse：启用。
- upwardThreshold：识别阈值，默认 0.35；朝上为 1，侧面为 0，朝下为 -1。
- upwardSoftness：阈值两侧的平滑过渡宽度，默认 0.3。
- upwardLength / otherLength：朝上及其他区域拉伸倍率，默认 1.5 / 0.2。
- upwardContribution / otherContribution：拖影贡献，默认 1 / 0.15。
  减少贡献时保留更多原像，而不是让整个表面消失。
- upwardTwoSided：薄片背面的法线朝向修正，默认启用。
- upwardPreview：白色为强影响、黑色为弱影响。预览跳过拖影和模糊。

建议先开启预览调整阈值与过渡，再关闭预览调长度和贡献。
Override 参数独立保存；“从 Renderer 全局复制参数”包含这组参数。
SpatialGhosts 的矩阵及圆柱形变会参与法线捕获；圆柱模糊本身按源表面的遮罩展开。

遮罩使用网格法线（不包含材质法线贴图）。常规 MeshRenderer、SkinnedMeshRenderer
和 Alpha Cutoff 可用；自定义 Shader 顶点动画、体积材质需要专门适配。
透明交叠继续使用单层代表性深度。开启全局响应会额外收集该 Layer 的网格并绘制遮罩，
大场景与头显需要单独测量性能。细薄叶片的精度也受现有捕获分辨率影响。

验证入口：Tools / TRINL / Validate Glitch Upward Surfaces。
测试顶部/侧面遮罩值、低权重原像保留、平面及圆柱变化、局部覆盖与停用恢复。
桌面结果在 Library/GlitchUpwardValidation；未验证 Quest 双眼和设备帧时间。
