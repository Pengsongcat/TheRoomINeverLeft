# Spatial Ghosts / 三维拉伸残影

选中模型，使用 `GameObject > TRINL > Add Spatial Ghosts`。或者添加 `TRINL/Spatial Ghosts` 组件，再点击 Inspector 中“使用附带 Shader Graph 材质”。编辑模式和运行模式均可预览。关闭组件即可移除额外绘制，原模型、材质、Collider 和脚本保持不变。

## 调节

- **左右各几份**：2 表示增加四份三维副本。
- **重影间距**：世界米，不随根节点缩放改变。
- **方向坐标系**：Local 随目标转动；World 固定世界方向；Camera 随主摄像机（`Camera.main`）朝向转动，例如把方向设为 (0,0,1) 可让残影始终沿当前视角向屏幕深处展开，跟随摄像机转动而不受目标自身朝向影响。场景中没有 `MainCamera` 标签的摄像机时（例如仅在编辑器里用 Scene 视图预览），Camera 会退化为固定世界方向。默认 X 轴，可用任意非零向量。
- **每层增加的拉伸**：0.25 表示近影 1.25 倍、第二层 1.50 倍。围绕“拉伸支点”沿展开方向拉伸整个源层级。
- **近侧残影透明度 / 透明度衰减倍率**：第二层透明度 = 近层透明度 × 衰减倍率。
- **轮廓淡化**：按法线和观看方向渐淡轮廓，是表面透明度效果，不是体积模糊。
- **Layer 自动继承**：每份残影使用对应源 Renderer 的 GameObject Layer，每次相机渲染时重新读取；运行时改 Layer 也会立即跟随。无独立的残影 Layer 参数。

源物体在 ScreenBlur Layer 时，残影一起进入 ScreenBlur。该 Renderer Feature 现在先绘制该层的不透明物体，再按透明顺序绘制透明物体，随后统一模糊；VR_Renderer 的普通不透明和透明通道均排除 ScreenBlur Layer，避免重复显示清晰版本。使用其他 Renderer Data 时，也需配置相同的排除规则。

## Shader Graph 与几何的分工

`SpatialGhost.shadergraph` 是可打开和修改的真实 URP Unlit Shader Graph。节点包括原纹理和颜色、UV 缩放偏移、透明度以及轮廓淡化。**三维重复、位移和轴向拉伸由组件提交的几何矩阵完成**；此版本并没有把这些几何参数做成 Vertex Graph 节点。

额外模型通过 Graphics.DrawMesh 提交，不产生 Hierarchy 副本，不复制行为脚本或碰撞体。标准 MeshRenderer 共享源 Mesh；SkinnedMeshRenderer 烘焙当前姿态，运行时每帧最多一次/源模型，再复用给不同相机。更多层数和蒙皮模型会增加成本。

原始材质保持不变。残影读取常规 `_BaseMap` / `_MainTex` 和 `_BaseColor` / `_Color`，不复制任意自定义材质逻辑、法线贴图、光照、发光或 MaterialPropertyBlock 覆盖。运行时替换材质或层级后调用 `RefreshSources()`。LODGroup 自动筛选尚未实现，宜挂到指定 LOD 模型，不要挂在包含所有 LOD 的根物体上。

透明副本使用双面绘制、正常深度测试、不投影。普通通道不写深度；ScreenBlur 的离屏捕获为透明物体记录代表性前表面深度，用于原有的遮挡判断。闭合模型内部或多个透明副本相交时可能出现透明排序问题；并非顺序无关透明方案，也不是逐透明层深度模糊。Prefab 隔离模式下暂停额外绘制。源模型需要处于启用和激活状态。

## 当前示例

已配置 `Field_Poppy_vmcobd0ja_Mid_vmcobd0ja_VarC_LOD0`：左右各 2 份，间距 0.23 米，每层增加 0.25 倍拉伸，近影透明度 0.32，衰减 0.55。源花朵与新增三维残影均使用源物体的 Layer 7，并一起模糊。

## 验证

选中带组件的物体，执行 `Tools > TRINL > Validate Selected Spatial Ghosts`。在桌面相机中比较关闭后的原物体与开启残影、正面和斜面渲染、支点位移和拉伸、原材质引用、无克隆 GameObject，以及源物体改到相机排除的 Layer 后残影也不再绘制。测试结束会恢复源 Layer。结果和图片位于 `Library/SpatialGhostsValidation/`。当前场景的 ScreenBlur 接入另经多角度截图检查。

头显双眼效果、Android 构建和 GPU 帧预算需要在目标设备上另行验证。

## Camera：Billboard 模式
每份残影的几何体会整体朝向相机位置，相机平移绕行也会转向。原物体保持不变。空相机引用自动赋值 Camera.main，并在主相机晚生成时重试；手动引用保留。默认根物体 +Z 朝向相机，模型正面校正可调整正面轴（例如 Y=180）。当前已绑定 XR CenterEyeAnchor。
Tools > TRINL > Validate Spatial Ghost Camera Direction 已验证固定相机旋转、移动到三处位置时四份残影都面向相机，以及自动赋值与显式引用保留。头显运行尚未验证。