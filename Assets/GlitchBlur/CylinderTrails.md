# GlitchBlur cylinder trails

VR_Renderer > GlitchBlur > 三维圆柱拖影 enables the depth-reprojected mode.
This applies to the configured GlitchBlur layer, without individual object components.
Disable it to restore the existing pixel-length horizontal filter and its stored settings.

The original-material color/depth capture is reconstructed in world space as small surface
patches. Multiple weighted samples sweep these patches along horizontal cylinder arcs.
The patches are reprojected to the rendering camera and depth-tested against scene depth;
the nearest contributing eye depth is retained for the final vertical-softness composite.
This is view-dependent captured-surface geometry, not a full mesh or a closed volume.
Off-screen surfaces and hidden back faces cannot contribute. Overlapping transparency has
only one representative captured depth. Strong deformation can expose sampling gaps.

Main Camera supplies a shared player position. Radius interpolates smoothly between near
and far values according to each captured point's horizontal distance to the player.
The vertical axis is world up. Each point's arc is tangent to its player-relative circle;
the radius controls how strongly that arc bends. It is not a single object-pivot cylinder
like SpatialGhosts. Both effects can be enabled together, which stacks their deformations.

Settings:
- Continuous lines: the `连续拉伸成线` button in Cylinder Settings, available globally
  and in each GlitchBlurOverride (`public bool continuousLines`). Disabled by default
  to preserve discrete patches. Enable to join adjacent arc samples with ribbon quads;
  disable to restore the original point-like trails. Existing length, influence,
  upward weighting and Gaussian controls still apply.
- Line length multiplier (`lineLengthMultiplier`, default 1) scales only continuous
  lines. Final arc length = existing inner/outer length * multiplier * random factor
  * upward length weight. Zero collapses the line back to its source patch.
- Random length range (`lineRandomRange`, default 1/1) supplies min/max multipliers.
  Try 0.3/1 for varied lengths; equal endpoints remove variation. World-space cells
  (`lineRandomCellSize`, metres) and `lineRandomSeed` define a deterministic pattern.
  It does not animate with time, but moving sources across cells changes the pattern;
  this is not a persistent object-space random ID for animated objects.
- Overlay points (`overlayPoints`, default false) adds the original discrete trail
  on top of lines, using its original lengths. `overlayPointWeight` (default 0.5)
  controls its additive contribution. This also increases brightness where they overlap
  and adds one procedural draw. All these controls are public and available locally.
- Response distance near/far: world metres, default 0.6/4.
- Radius near/far: world metres, default 0.8/12.
- Length inner/outer: per-side arc length in metres, default 0.6/1.2.
- Influence endpoints: control contributions near the original sample and toward trail ends.
  In this mode the transition is along the sweep, not measured from a screen silhouette.
- Toward player: reverses the depth direction of the arc.
- Samples: odd count, 9–65, default 33. More samples reduce separated bands in point
  mode, or improve arc segmentation in continuous-line mode. Continuous ribbons cover
  more screen area, so their fragment cost can exceed the discrete mode.
- Capture width limit: 64–512, default 256; retains aspect ratio and XR texture layout.
  Higher resolution increases both captured detail and procedural vertex cost.
- Vertical softness, jitter, drift and opacity retain their controls. Pixel lengths are
  used only in flat mode. Avoid arc lengths much larger than the radius unless wrapping
  and overlap are desired.

Cost scales with capture width * height * samples * 6 vertices per view. This is an
experimental quality/performance tradeoff, not yet a Quest frame-budget validated effect.
Stereo macros and per-eye reconstruction are included, but actual headset rendering,
Android shader compilation and stereo disocclusion quality remain untested.

Tools > TRINL > Validate Glitch Cylinder checks curved versus nearly straight projection,
zero-length coverage, perspective visibility, foreground occlusion, and ScreenBlur isolation.
Temporary objects/settings are restored. Reports/images: Library/GlitchCylinderValidation.
The existing Validate GlitchBlur explicitly exercises flat mode and restores the mode.

Tools > TRINL > Validate Glitch Continuous Lines checks that gaps disappear at nine
samples, switching off restores the point image, and local/global mode controls agree.
Reports/images: Library/GlitchLineValidation. This is desktop validation only.
