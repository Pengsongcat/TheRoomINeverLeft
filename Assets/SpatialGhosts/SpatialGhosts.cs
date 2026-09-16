using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TRINL.Rendering
{
    /// <summary>Draws spatial copies of meshes; never clones gameplay objects or edits source materials.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("TRINL/Spatial Ghosts")]
    public sealed class SpatialGhosts : MonoBehaviour
    {
        public enum DirectionSpace { Local, World, Camera, Cylinder }

        public Vector2 cylinderDistances = new Vector2(0.6f, 4f);
        public Vector2 cylinderRadii = new Vector2(0.8f, 12f);
        [Min(0)] public float cylinderLength = 1.5f;
        public bool cylinderTowardPlayer = true;
        public Material cylinderMaterial;

        [Tooltip("Leave empty to use this object and its children.")]
        public Transform sourceRoot;
        [Range(0, 8)] public int copiesPerSide = 2;
        [Min(0)] public float spacing = 0.12f;
        public Vector3 direction = Vector3.right;
        [Tooltip("Local rotates with the target. World is fixed. Camera billboards each ghost toward the camera position.")]
        public DirectionSpace directionSpace = DirectionSpace.Local;
        [Tooltip("Auto-filled from Camera.main when empty, including cameras spawned later. Explicit assignments are preserved.")]
        public Camera directionCamera;
        [Tooltip("Adjust the model's front axis for billboarding. At zero, source-root local +Z faces the camera. Use Y=180 for -Z-front models.")]
        public Vector3 billboardRotationOffset;
        [Tooltip("Additional axial scale per step: 0.35 gives scales 1.35, 1.70, ...")]
        [Range(0, 3)] public float stretchPerStep = 0.35f;
        [Range(0, 1)] public float opacity = 0.3f;
        [Range(0, 1)] public float opacityDecay = 0.55f;
        [ColorUsage(true, true)] public Color tint = new Color(0.72f, 0.82f, 1f, 1f);
        [Range(0, 1)] public float edgeSoftness = 0.65f;
        [Tooltip("Local to the source root. This is also the center of stretching.")]
        public Vector3 pivotOffset;
        [Tooltip("The supplied SpatialGhost material, or a compatible graph material.")]
        public Material ghostMaterial;

        sealed class Source
        {
            public Renderer renderer;
            public MeshFilter filter;
            public SkinnedMeshRenderer skin;
            public Mesh baked;
            public Material[] materials;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public int bakedFrame = -1;
        }

        [NonSerialized] List<Source> sources;
        [NonSerialized] bool rebuild = true;
        [NonSerialized] Transform cachedRoot;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TilingId = Shader.PropertyToID("_GhostTiling");
        static readonly int OffsetId = Shader.PropertyToID("_GhostOffset");
        static readonly int OpacityId = Shader.PropertyToID("_GhostOpacity");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeSoftness");

        public Transform Root => sourceRoot != null ? sourceRoot : transform;

        void OnEnable()
        {
            rebuild = true;
            AssignMainCamera();
            RenderPipelineManager.beginCameraRendering += DrawForCamera;
        }

        public void AssignMainCamera()
        {
            if ((directionSpace == DirectionSpace.Camera || directionSpace == DirectionSpace.Cylinder) && directionCamera == null)
                directionCamera = Camera.main;
        }

        void LateUpdate() => AssignMainCamera();

        Camera ResolveCamera(Camera renderingCamera)
        {
            AssignMainCamera();
            return directionCamera != null ? directionCamera : renderingCamera;
        }

        static Quaternion FacingRotation(Camera camera, Vector3 position)
        {
            Vector3 forward = camera.transform.position - position;
            if (forward.sqrMagnitude < 0.000001f) forward = -camera.transform.forward;
            forward.Normalize();
            Vector3 up = camera.transform.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f) up = camera.transform.right;
            return Quaternion.LookRotation(forward, up);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawForCamera;
            ReleaseSources();
        }

        void OnValidate()
        {
            copiesPerSide = Mathf.Clamp(copiesPerSide, 0, 8);
            spacing = Mathf.Max(0, spacing);
            stretchPerStep = Mathf.Clamp(stretchPerStep, 0, 3);
            opacity = Mathf.Clamp01(opacity);
            opacityDecay = Mathf.Clamp01(opacityDecay);
            edgeSoftness = Mathf.Clamp01(edgeSoftness);
            cylinderDistances.x = Mathf.Max(0, cylinderDistances.x);
            cylinderDistances.y = Mathf.Max(cylinderDistances.x + 0.01f, cylinderDistances.y);
            cylinderRadii.x = Mathf.Max(0.05f, cylinderRadii.x);
            cylinderRadii.y = Mathf.Max(0.05f, cylinderRadii.y);
            cylinderLength = Mathf.Max(0, cylinderLength);
            rebuild = true;
        }

        void OnTransformChildrenChanged() => rebuild = true;

        [ContextMenu("Refresh Source Renderers")]
        public void RefreshSources() => rebuild = true;

        internal Mesh GetMaskMesh(Renderer renderer)
        {
            if (rebuild || cachedRoot != Root || sources == null) CollectSources();
            foreach (var source in sources)
            {
                if (source.renderer != renderer) continue;
                if (source.skin != null)
                {
                    if (!Application.isPlaying || source.bakedFrame != Time.frameCount)
                    {
                        source.skin.BakeMesh(source.baked, false);
                        source.bakedFrame = Time.frameCount;
                    }
                    return source.baked;
                }
                return source.filter != null ? source.filter.sharedMesh : null;
            }
            return null;
        }

        void ReleaseSources()
        {
            if (sources == null) return;
            foreach (var source in sources)
            {
                if (source.baked == null) continue;
                if (Application.isPlaying) Destroy(source.baked);
                else DestroyImmediate(source.baked);
            }
            sources.Clear();
        }

        void CollectSources()
        {
            ReleaseSources();
            if (sources == null) sources = new List<Source>();
            cachedRoot = Root;
            foreach (var renderer in cachedRoot.GetComponentsInChildren<Renderer>(true))
            {
                // An enabled child effect owns its own renderers.
                var childEffect = renderer.GetComponentInParent<SpatialGhosts>();
                if (childEffect != null && childEffect != this && childEffect.isActiveAndEnabled) continue;
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>() : null;
                if ((skin == null || skin.sharedMesh == null) && (filter == null || filter.sharedMesh == null)) continue;
                var item = new Source { renderer = renderer, skin = skin, filter = filter, materials = renderer.sharedMaterials };
                if (skin != null)
                {
                    item.baked = new Mesh { name = "SpatialGhosts pose", hideFlags = HideFlags.HideAndDontSave };
                    item.baked.MarkDynamic();
                }
                sources.Add(item);
            }
            rebuild = false;
        }

        public Vector3 WorldDirection => GetWorldDirection(null);

        public float GetCylinderRadius(Camera camera = null)
        {
            camera = ResolveCamera(camera);
            float distance = camera == null ? cylinderDistances.y : Vector3.ProjectOnPlane(
                camera.transform.position - Root.TransformPoint(pivotOffset), Vector3.up).magnitude;
            float t = Mathf.InverseLerp(cylinderDistances.x, Mathf.Max(cylinderDistances.x + .01f, cylinderDistances.y), distance);
            return Mathf.Max(.05f, Mathf.Lerp(cylinderRadii.x, cylinderRadii.y, t * t * (3 - 2 * t)));
        }

        public void GetCylinderFrame(Camera camera, out Vector3 right, out Vector3 forward)
        {
            camera = ResolveCamera(camera);
            forward = camera != null ? camera.transform.position - Root.TransformPoint(pivotOffset) : Root.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < .000001f) forward = Vector3.forward;
            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward);
        }

        // Shared with the editor preview and validation. The shader uses the same mapping.
        public static Vector3 CylinderPoint(Vector3 point, Vector3 pivot, Vector3 right, Vector3 forward,
            float radius, float sign, float scale, float offset)
        {
            Vector3 p = point - pivot;
            float angle = (Vector3.Dot(p, right) * scale + offset) / radius;
            float depth = Vector3.Dot(p, forward);
            return pivot + right * ((radius - sign * depth) * Mathf.Sin(angle))
                + Vector3.up * p.y + forward * (sign * radius * (1 - Mathf.Cos(angle)) + depth * Mathf.Cos(angle));
        }

        public Vector3 GetWorldDirection(Camera renderingCamera)
        {
            var axis = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right;
            switch (directionSpace)
            {
                case DirectionSpace.Cylinder:
                    GetCylinderFrame(renderingCamera, out var right, out _);
                    return right;
                case DirectionSpace.Local: return Root.TransformDirection(axis).normalized;
                case DirectionSpace.Camera:
                    var reference = ResolveCamera(renderingCamera);
                    if (reference == null) reference = Camera.current;
                    return reference != null ? FacingRotation(reference, Root.TransformPoint(pivotOffset)) * axis : axis;
                default: return axis;
            }
        }

        /// <summary>Stretch the complete source assembly about a shared pivot, then offset it in world units.</summary>
        public Matrix4x4 GetGhostMatrix(Matrix4x4 sourceMatrix, int signedStep, Camera renderingCamera = null)
        {
            var axis = GetWorldDirection(renderingCamera);
            var pivot = Root.TransformPoint(pivotOffset);
            float amount = Mathf.Abs(signedStep) * stretchPerStep;
            var stretch = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    stretch[row, column] += amount * axis[row] * axis[column];
            var ghostPivot = pivot + axis * (spacing * signedStep);
            var rotation = Quaternion.identity;
            if (directionSpace == DirectionSpace.Camera)
            {
                var reference = ResolveCamera(renderingCamera);
                if (reference != null)
                    rotation = FacingRotation(reference, ghostPivot)
                        * Quaternion.Euler(billboardRotationOffset) * Quaternion.Inverse(Root.rotation);
            }
            // Rotate the actual source geometry, not only its repetition axis. Stretch before
            // facing rotation so nonuniform axial stretching does not skew the facing normal.
            var localStretchAxis = directionSpace == DirectionSpace.Camera
                ? Root.TransformDirection(direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right).normalized
                : axis;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    stretch[row, column] = (row == column ? 1 : 0) + amount * localStretchAxis[row] * localStretchAxis[column];
            return Matrix4x4.Translate(ghostPivot) * Matrix4x4.Rotate(rotation)
                * stretch * Matrix4x4.Translate(-pivot) * sourceMatrix;
        }

        void DrawForCamera(ScriptableRenderContext context, Camera camera)
        {
            bool cylindrical = directionSpace == DirectionSpace.Cylinder;
            var drawMaterial = cylindrical ? cylinderMaterial : ghostMaterial;
            if (!isActiveAndEnabled || drawMaterial == null || copiesPerSide == 0 || opacity <= 0) return;
            if (camera == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
#if UNITY_EDITOR
            // Do not leak the main scene into prefab isolation or unrelated preview stages.
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return;
#endif
            if (rebuild || cachedRoot != Root || sources == null) CollectSources();
            foreach (var source in sources)
            {
                var renderer = source.renderer;
                if (renderer == null) { rebuild = true; continue; }
                if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                // Read each renderer's layer every camera render, including runtime layer changes.
                int sourceLayer = renderer.gameObject.layer;
                if ((camera.cullingMask & (1 << sourceLayer)) == 0) continue;
#if UNITY_EDITOR
                if (UnityEditor.SceneVisibilityManager.instance.IsHidden(renderer.gameObject)) continue;
#endif
                Mesh mesh;
                if (source.skin != null)
                {
                    if (source.skin.sharedMesh == null) continue;
                    if (!Application.isPlaying || source.bakedFrame != Time.frameCount)
                    {
                        source.skin.BakeMesh(source.baked, false);
                        source.bakedFrame = Time.frameCount;
                    }
                    mesh = source.baked;
                }
                else mesh = source.filter != null ? source.filter.sharedMesh : null;
                if (mesh == null) continue;

                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var material = source.materials.Length > 0 ? source.materials[Mathf.Min(submesh, source.materials.Length - 1)] : null;
                    FillAppearance(source.block, material);
                    for (int step = 1; step <= copiesPerSide; step++)
                    {
                        float alpha = opacity * Mathf.Pow(opacityDecay, step - 1);
                        if (alpha < 0.001f) continue;
                        source.block.SetFloat(OpacityId, alpha);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (cylindrical)
                            {
                                DrawCylinder(source, mesh, camera, submesh, side * step, sourceLayer);
                                continue;
                            }
                            Graphics.DrawMesh(mesh, GetGhostMatrix(renderer.localToWorldMatrix, side * step, camera),
                                ghostMaterial, sourceLayer, camera, submesh, source.block,
                                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                        }
                    }
                }
            }
        }

        void DrawCylinder(Source source, Mesh mesh, Camera camera, int submesh, int step, int layer)
        {
            GetCylinderFrame(camera, out var right, out var forward);
            var pivot = Root.TransformPoint(pivotOffset);
            float radius = GetCylinderRadius(camera);
            var block = source.block;
            block.SetVector("_CylinderPivot", pivot);
            block.SetVector("_CylinderRight", right);
            block.SetVector("_CylinderForward", forward);
            block.SetVector("_CylinderShape", new Vector4(radius, cylinderTowardPlayer ? 1 : -1,
                1 + Mathf.Abs(step) * stretchPerStep, cylinderLength * step / Mathf.Max(1, copiesPerSide)));
            // GPU deformation extends outside the original mesh bounds. Supply a conservative
            // world bound so sideways views and both XR eyes retain the curved geometry.
            var sourceBounds = source.renderer.bounds;
            float extent = (sourceBounds.center - pivot).magnitude + sourceBounds.extents.magnitude;
            var rp = new RenderParams(cylinderMaterial) {
                camera = camera, layer = layer, matProps = block,
                worldBounds = new Bounds(pivot, Vector3.one * (4 * radius + 2 * extent + .1f)),
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMesh(rp, mesh, submesh, source.renderer.localToWorldMatrix);
        }

        void FillAppearance(MaterialPropertyBlock block, Material original)
        {
            block.Clear();
            Color color = Color.white;
            Texture texture = Texture2D.whiteTexture;
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            if (original != null)
            {
                if (original.HasProperty(BaseColor)) color = original.GetColor(BaseColor);
                else if (original.HasProperty(ColorId)) color = original.GetColor(ColorId);
                int map = original.HasProperty(BaseMap) ? BaseMap : MainTex;
                if (original.HasProperty(map))
                {
                    texture = original.GetTexture(map) ?? Texture2D.whiteTexture;
                    scale = original.GetTextureScale(map);
                    offset = original.GetTextureOffset(map);
                }
            }
            block.SetTexture(BaseMap, texture);
            block.SetColor(BaseColor, color * tint);
            block.SetVector(TilingId, new Vector4(scale.x, scale.y, 0, 0));
            block.SetVector(OffsetId, new Vector4(offset.x, offset.y, 0, 0));
            block.SetFloat(EdgeId, edgeSoftness);
        }

        void OnDrawGizmosSelected()
        {
            var center = Root.TransformPoint(pivotOffset);
            Gizmos.color = new Color(0.35f, 0.8f, 1, 0.8f);
            if (directionSpace == DirectionSpace.Cylinder)
            {
                GetCylinderFrame(null, out var right, out var forward);
                float radius = GetCylinderRadius();
                Vector3 previous = CylinderPoint(center, center, right, forward, radius,
                    cylinderTowardPlayer ? 1 : -1, 1, -cylinderLength);
                for (int i = 1; i <= 64; i++)
                {
                    var next = CylinderPoint(center, center, right, forward, radius,
                        cylinderTowardPlayer ? 1 : -1, 1, Mathf.Lerp(-cylinderLength, cylinderLength, i / 64f));
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
                return;
            }
            Gizmos.DrawLine(center - WorldDirection * spacing * copiesPerSide, center + WorldDirection * spacing * copiesPerSide);
        }
    }
}
