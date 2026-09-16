# Cylinder mode

Select the object, then Spatial Ghosts > Direction Space > Cylinder.
Cylinder Settings contains near/far response distances and near/far radii in paired rows.
Horizontal player distance drives smoothstep interpolation; smaller radii bend more.
The camera reference automatically resolves Camera.main when empty. All rendering cameras
and XR eyes use this shared reference rather than independent eye-relative geometry.

The source remains unchanged. Ghost vertices stretch horizontally in world space and bend
around a shared vertical mathematical cylinder through the source pivot. No cylinder object
or collider is required. Each Side Length is the arc distance to the outermost copy center;
mesh width and per-step stretch extend beyond it. Large lengths with small radii can wrap
around or overlap. Keep radius larger than the source's depth for an unfolded shape.

Cylinder mode uses SpatialGhostCylinder.mat and its dedicated vertex shader. Existing
Local/World/Camera modes still use SpatialGhost.shadergraph. Appearance uses the same base
texture, base color, tint, opacity decay and edge fading conventions. Custom source shader
features are not reproduced. Ghost draw layers continue to inherit source renderer layers.
GlitchBlur remains a screen-space softening pass over the resulting geometry; its settings
are independent of these per-object cylinder settings.

GPU geometry bounds are supplied explicitly to prevent curved ghosts being culled using
the original mesh bounds. Normals are transformed using the deformation Jacobian.
Coarse triangles can reveal faceting under strong bending; this shader does not subdivide.

Tools > TRINL > Validate Cylinder Ghosts runs a desktop render check on the configured
flower (3), restoring all temporary appearance, camera and layer overrides in finally.
It checks near/far/midpoint radii, pixel differences, layer inheritance and original asset
references. Images and report are in Library/SpatialGhostCylinderValidation.
Quest stereo rendering, Android builds and headset frame time require device validation.
