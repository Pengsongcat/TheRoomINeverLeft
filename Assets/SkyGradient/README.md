# 天空渐变与 Timeline

场景对象 `Sky Gradient Controller`：可直接编辑颜色、暖色带宽度、地平线高度、边缘柔度、弧度、旋转、倾斜、亮度、夜色混合。
仅影响 Target Cameras，停用恢复相机设置；不会修改 RenderSettings、环境光或原始材质。
天空始终位于几何体后面，已有 Ground 和 GlobleBlur 仍可能覆盖它。天空下半部为绘制背景，不是实体碰撞或物理大气。
方向使用世界空间；水平颜色均匀且 Curvature/Directional Warmth 为 0 时，旋转不会有明显效果。

选中对象，打开 Window > Sequencing > Timeline。已绑定 SunsetSkyDemo.playable：20 秒夕阳转夜空并旋转 30 度、倾斜 6 度；Play On Awake 关闭。
可修改 SunsetToNight.anim，或新建 Animation Track/Clip，在 Add Property 中添加 SkyGradientController 的字段。
Night Blend=0 为 Inspector 配色，1 为预设夜空；所有 Color 字段也可逐个录制。
拖动 Timeline 预览后退出预览，即可回到手动调色。颜色按线性渲染转换；整体外观仍受相机后处理影响。

组件使用临时独立材质；编辑器保存场景前撤销临时相机设置、保存后恢复预览。
Shader 用序列化引用保留到构建。已验证桌面渲染和 Timeline 第 10 秒的参数驱动。
Quest/Android、双眼与 GPU 性能尚未验证。
