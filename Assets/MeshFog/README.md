# 模型形状体积雾 — MeshFog

当前场景已加入 **MeshFog Example - Statue**，可复用 Prefab 为 `Assets/MeshFog/Generated/MeshFogStatue.prefab`。已有 ScreenBlur 效果保留，雾不依赖 ScreenBlur Layer。

## 使用

1. 选中含 MeshFilter 的静态封闭模型，添加 **Rendering > Mesh Fog** 组件。
2. Source 指向该物体或其子物体上的单个 MeshFilter。不会自动合并多个 Mesh，也不支持 SkinnedMeshRenderer。
3. 设置 Bake Resolution（16–96，示例 48）和 Bake Padding（每侧的局部单位余量，示例 0.25）。点击 **1. 烘焙形状 / 更新 SDF**。
4. 点击 **2. 生成 / 更新雾效**。工具创建一个 FogVolume 子物体及材质，再次点击更新现有代理。
5. 调整参数，使用 **切换为原模型 / 切换为雾** 对照。取消 Show Fog 或禁用组件会恢复原 Renderer 的 enabled 状态；碰撞体保持原样。
6. 点击 **3. 保存为 Prefab…**，或正常保存场景。

相机必须启用 URP Depth Texture，且 Culling Mask 包含代理的 Default 层。本工程当前 Camera 与 CenterEyeAnchor 已启用深度。代理默认在 Default 层，避免被 ScreenBlur 的层过滤影响。

| 参数 | 含义 |
|---|---|
| Density | 每世界单位的吸收浓度；越大越厚、不透明 |
| Edge Softness | SDF 轮廓柔边宽度，源 Mesh 局部单位 |
| Fog Color | 简化的雾颜色，不是原材质颜色 |
| Noise Scale / Strength | 雾团频率与浓淡差异 |
| Flow Speed | 三维程序噪声坐标随时间移动的速度 |
| Contour Noise | 轮廓扰动幅度，局部单位 |
| Dissolve | 0 完整显示，1 完全消失；控制吸收浓度，不是流体模拟 |
| Steps | 每条视线 16–128 次体积积分；示例 64 |

修改 Mesh 或烘焙范围后需要重新烘焙。修改边缘柔度和扰动时，让 Padding 大于两者之和。使用同一个 Shape 的实例共享烘焙结果；重新烘焙该资源会影响所有引用者。需要独立形状时先清空 Shape 再烘焙。

## 实现

### 前后渲染顺序

每个源模型的 **Mesh Fog → Rendering Order / 前后渲染顺序 → Sorting Order** 可独立设置。数值越大越晚绘制，显示在较小值的雾上层，例如后层 `0`、前层 `10`；默认 `0`，相同值继续由透明距离排序决定。运行时限制在 -32768 到 32767。修改立即生效，不需要重新烘焙，也不修改共享材质。

使用代理 MeshRenderer 的 sortingOrder，只影响同一 Sorting Layer 和渲染阶段内的顺序。不同 Renderer Feature 合成阶段之间的顺序不由这个参数决定。该功能固定整块雾的叠放关系，不能实现相交体积的逐像素交错积分；绕到物体背面时固定顺序仍不变。

### 光照

在源模型的 **Mesh Fog → Lighting / 雾光照** 中启用 `Enable Lighting`，无需换材质或重新烘焙 SDF。旧实例默认关闭光照以保留原效果。

- `Ambient Brightness`：固定补光亮度，默认 0.2；0 时无灯区域变暗，不是烘焙环境光。
- `Light Intensity`：场景灯对雾的贡献倍率，默认 1。
- `Anisotropy`：默认 0 为各向同性；正值增强朝向光源观看时的散射。
- `Receive Light Shadows`：采样场景阴影贴图；需开启灯的 Shadows、URP 对应阴影，以及 Renderer 的 Transparent Receive Shadows。当前 VR_Renderer 已开启后者，也会影响其他支持阴影的透明材质。

每个有效体积采样点计算方向光、点光源和聚光灯，包含颜色、距离衰减、聚光锥及 Light Cookie；支持 URP Rendering Layers。Forward 模式遵循管线的每物体灯光数量上限（当前 VR 为 4）。Forward+ 路径按采样点查询灯光簇，但本次未做其运行验证。

Fog Color 是散射颜色，会乘以灯光颜色：纯红色雾会滤掉蓝/绿色光；要明显显示灯的颜色，可将雾调成白或浅灰。保留原模型材料。没有雾自阴影、多次散射或体积密度向场景投影；场景阴影是实体遮挡物投进雾里的阴影。

光照和阴影随 Steps 与灯数增加 GPU 成本，Quest 性能需要真机验证。桌面渲染检查可运行 `MeshFogLightingValidation.Run(example)`，结果位于 `Artifacts/MeshFog/Lighting/`，覆盖灯色、距离、聚光锥、方向光、阴影开关和关闭光照后的旧效果。

- 编辑器 CPU 烘焙，不新增 VFX Graph 包依赖。BVH 加速最近三角形距离和射线奇偶内部测试。
- 预先焊接位置重合的顶点以检查封闭边界；开口或非流形 Mesh 会拒绝烘焙。自相交、重叠壳体和嵌套壳体仍需人工检查，内部按奇偶规则解释。
- SDF 用 RFloat Texture3D 保存：内部为负、外部为正，数值是 Mesh 局部距离；采样点位于体素中心。Shape 同时保存局部 Bounds、原 Mesh 和精度。
- Shader 在包围盒的背面执行，通过逆投影获得每像素视线，并按场景深度截断。射线参数始终使用世界距离，非均匀正缩放不会错误改变步长单位。柔边和噪声随模型局部坐标拉伸；密度仍按世界距离积分。
- SDF 结合三维噪声生成密度，使用 `1-exp(-density*stepLength)` 累积预乘颜色，接近不透明时提前结束。
- 深度纹理使用 URP 的逐眼采样及逆视投影矩阵。保留 Render Graph、4× MSAA 与原 Render Scale，不增加体积 Renderer Feature。

概念参考：[Unity SDF Bake Tool](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.3/manual/sdf-bake-tool.html)、[三维纹理采样](https://docs.unity3d.com/6000.0/Documentation/Manual/class-Texture3D-use-in-shader.html)。本实现使用自己的 CPU 编码，不直接兼容 VFX Graph 的 SDF 编码。

## 验证结果

自动检查和图片保存于项目的 `Artifacts/MeshFog/`，可通过 `MeshFogValidation.Run(example)` 重跑（编辑器 API）。

- C#、Shader 编译通过，最终控制台未见错误或警告。
- 48³ 雕像 SDF 中心为 -0.2118914，边界角点为 +0.5705776。
- 重复生成仍只有一个代理；再次烘焙保留绑定。
- 原模型切换与禁用恢复通过。
- 384×384、HDR、4× MSAA 的桌面相机下，64/128 步的平均 RGB 差异 0.000004。
- 前景遮挡板中心与无雾参考的蓝色差异为 0；截图确认前景边缘清晰。
- 相机置于体积内部可见雾，已检查截图。
- 旋转及正的非均匀缩放已检查截图。
- 示例 Prefab 保存后重新载入，纹理引用和雾显示状态通过。未重开用户整个场景。

## 限制

尚未验证头显左右眼、Android 构建、GPU 帧时或帧率预算。此次视觉检查来自桌面相机，不能视为 VR 验收。噪声流动使用 Shader 时间，检查动态效果时进入 Play Mode 或启用 Scene View 的动画材质显示。

第一版没有真实流体、骨骼变形、复杂光散射、自阴影、低分辨率合成或多个相交雾体的正确排序。透明场景遮挡也不保证正确。密度体不写场景深度、不投射实体阴影。负缩放、极薄或细小结构、视锥远裁剪面穿过代理出口的情况未验证。复杂 Mesh 或高烘焙精度会增加编辑器烘焙时间，可取消。

生成资源是持久资产，撤销场景生成操作不会自动删除资产文件。可正常删除不用的 Shape / Material / Prefab。

本次还对 ScreenBlur 增加了纯深度相机与无效颜色目标检查，以修复体积光插件的辅助深度相机触发 Render Graph 错误的问题。
