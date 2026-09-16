using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TRINL.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("TRINL/Glitch Blur Override")]
    public sealed class GlitchBlurOverride : MonoBehaviour
    {
        public bool includeChildren = true;
        [Range(0,32)] public float gaussianRadius;
        [Range(0,1)] public float gaussianStrength = 1;
        public bool gaussianDistanceResponse;
        [Min(.01f)] public float gaussianResponseDistance = 5;
        [Range(0,1)] public float gaussianNearStrength = .6f;
        [Range(0,1)] public float gaussianFarStrength = 1;
        public bool cylinderTrails;
        public Vector2 cylinderDistances = new Vector2(.6f,4f);
        public Vector2 cylinderRadii = new Vector2(.8f,12f);
        public Vector2 cylinderLengths = new Vector2(.6f,1.2f);
        public bool cylinderTowardPlayer = true;
        [Range(9,65)] public int cylinderSamples = 33;
        [Range(64,512)] public int cylinderResolution = 256;
        [Range(0,256)] public float horizontalLength = 100;
        [Range(0,256)] public float outerHorizontalLength = 150;
        [Range(0,1)] public float originalWeight = .85f;
        [Range(0,1)] public float outerOriginalWeight = .05f;
        [Range(0,16)] public float verticalSoftness = 2;
        [Range(0,.5f)] public float bandJitter = .08f;
        [Range(4,128)] public int bandCount = 40;
        [Range(0,2)] public float driftSpeed;
        [Range(1,4)] public int downsample = 2;
        [Range(0,1)] public float opacity = 1;
        [Min(0)] public float depthBias = .005f;
        internal static readonly List<GlitchBlurOverride> Active = new();
        readonly Dictionary<long, Material> masks = new();
        void Reset()
        {
#if UNITY_EDITOR
            var data=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            if(data) CopyFrom(data.rendererFeatures.Find(f=>f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature);
#endif
        }
        void OnEnable() { if(!Active.Contains(this)) Active.Add(this); }
        void OnDisable()
        {
            Active.Remove(this);
            foreach(var m in masks.Values) CoreUtils.Destroy(m);
            masks.Clear();
        }
        public void CopyFrom(GlitchBlurRendererFeature source)
        {
            if(!source) return;
            gaussianRadius = source.gaussianRadius;
            gaussianStrength = source.gaussianStrength;
            gaussianDistanceResponse = source.gaussianDistanceResponse;
            gaussianResponseDistance = source.gaussianResponseDistance;
            gaussianNearStrength = source.gaussianNearStrength;
            gaussianFarStrength = source.gaussianFarStrength;
            cylinderTrails = source.cylinderTrails;
            cylinderDistances = source.cylinderDistances;
            cylinderRadii = source.cylinderRadii;
            cylinderLengths = source.cylinderLengths;
            cylinderTowardPlayer = source.cylinderTowardPlayer;
            cylinderSamples = source.cylinderSamples;
            cylinderResolution = source.cylinderResolution;
            horizontalLength = source.horizontalLength;
            outerHorizontalLength = source.outerHorizontalLength;
            originalWeight = source.originalWeight;
            outerOriginalWeight = source.outerOriginalWeight;
            verticalSoftness = source.verticalSoftness;
            bandJitter = source.bandJitter;
            bandCount = source.bandCount;
            driftSpeed = source.driftSpeed;
            downsample = source.downsample;
            opacity = source.opacity;
            depthBias = source.depthBias;
        }
        internal void Collect(List<MaskDraw> draws, Shader shader, int id, Camera camera, int layers)
        {
            if(!shader) return;
            var renderers = includeChildren ? GetComponentsInChildren<Renderer>() : GetComponents<Renderer>();
            foreach(var r in renderers)
            {
                if(!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                    (layers & camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                // The nearest enabled override owns the renderer; disabling it restores parent/global settings.
                GlitchBlurOverride owner = null;
                for(var t=r.transform; t!=null; t=t.parent)
                {
                    var candidate=t.GetComponent<GlitchBlurOverride>();
                    if(candidate && candidate.isActiveAndEnabled && (t==r.transform || candidate.includeChildren)) {owner=candidate;break;}
                }
                if(owner!=this) continue;
#if UNITY_EDITOR
                if(UnityEditor.SceneVisibilityManager.instance.IsHidden(r.gameObject)) continue;
#endif
                if(!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var materials=r.sharedMaterials;
                for(int submesh=0;submesh<materials.Length;submesh++)
                {
                    var source=materials[submesh]; if(!source) continue;
                    long key=((long)r.GetInstanceID()<<32) | (uint)(submesh*32);
                    if(!masks.TryGetValue(key,out var mask) || !mask)
                    { mask=CoreUtils.CreateEngineMaterial(shader); masks[key]=mask; }
                    string map=source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    mask.SetTexture("_BaseMap",source.HasProperty(map) ? source.GetTexture(map) : Texture2D.whiteTexture);
                    var scale=source.HasProperty(map) ? source.GetTextureScale(map) : Vector2.one;
                    var offset=source.HasProperty(map) ? source.GetTextureOffset(map) : Vector2.zero;
                    mask.SetVector("_MaskST",new Vector4(scale.x,scale.y,offset.x,offset.y));
                    mask.SetFloat("_ObjectId",id);
                    float cutoff=source.IsKeywordEnabled("_ALPHATEST_ON") ? (source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f) : 0;
                    mask.SetFloat("_MaskCutoff",cutoff);
                    mask.SetFloat("_MaskCylinder",0);
                    draws.Add(new MaskDraw { renderer=r, material=mask, submesh=submesh, objectId=id });
                    var ghosts=r.GetComponentInParent<SpatialGhosts>();
                    if(!ghosts || !ghosts.isActiveAndEnabled || ghosts.opacity<=0) continue;
                    bool cylinder=ghosts.directionSpace==SpatialGhosts.DirectionSpace.Cylinder;
                    if(!(cylinder ? ghosts.cylinderMaterial : ghosts.ghostMaterial)) continue;
                    var mesh=ghosts.GetMaskMesh(r);
                    if(!mesh || submesh>=mesh.subMeshCount) continue;
                    for(int step=1;step<=Mathf.Clamp(ghosts.copiesPerSide,0,8);step++)
                    {
                        if(ghosts.opacity*Mathf.Pow(ghosts.opacityDecay,step-1)<.001f) continue;
                        for(int side=-1;side<=1;side+=2)
                        {
                            long ghostKey=key+(step-1)*2+(side<0?1:2);
                            if(!masks.TryGetValue(ghostKey,out var ghostMask) || !ghostMask)
                            {ghostMask=CoreUtils.CreateEngineMaterial(shader);masks[ghostKey]=ghostMask;}
                            ghostMask.CopyPropertiesFromMaterial(mask);
                            ghostMask.SetFloat("_ObjectId",id);
                            ghostMask.SetVector("_MaskST",new Vector4(scale.x,scale.y,offset.x,offset.y));
                            ghostMask.SetFloat("_MaskCutoff",cutoff);
                            ghostMask.SetFloat("_MaskCylinder",cylinder?1:0);
                            Matrix4x4 matrix=ghosts.GetGhostMatrix(r.localToWorldMatrix,side*step,camera);
                            if(cylinder)
                            {
                                matrix=r.localToWorldMatrix;
                                ghosts.GetCylinderFrame(camera,out var right,out var forward);
                                ghostMask.SetVector("_MaskPivot",ghosts.Root.TransformPoint(ghosts.pivotOffset));
                                ghostMask.SetVector("_MaskRight",right);ghostMask.SetVector("_MaskForward",forward);
                                ghostMask.SetVector("_MaskShape",new Vector4(ghosts.GetCylinderRadius(camera),
                                    ghosts.cylinderTowardPlayer?1:-1,1+step*ghosts.stretchPerStep,
                                    ghosts.cylinderLength*side*step/Mathf.Max(1,ghosts.copiesPerSide)));
                            }
                            draws.Add(new MaskDraw {mesh=mesh,matrix=matrix,material=ghostMask,submesh=submesh,objectId=id});
                        }
                    }
                }
            }
        }
        internal struct MaskDraw { internal Renderer renderer; internal Mesh mesh; internal Matrix4x4 matrix; internal Material material; internal int submesh, objectId; }
    }
}
