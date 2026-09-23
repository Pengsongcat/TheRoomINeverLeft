using System.Collections.Generic;
using UnityEngine;

namespace TRINL.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("TRINL/Sky Gradient Controller")]
    public sealed class SkyGradientController : MonoBehaviour
    {
        [Tooltip("指定显示天空的相机。启用时使用 Skybox 清屏，停用时恢复。")]
        public Camera[] targetCameras;
        [SerializeField] Shader skyShader;
        [Header("参考图配色 / 可直接录制动画")]
        [ColorUsage(false, true)] public Color zenithColor = new Color(.015f,.04f,.065f);
        [ColorUsage(false, true)] public Color blueColor = new Color(.20f,.34f,.40f);
        [ColorUsage(false, true)] public Color hazeColor = new Color(.48f,.39f,.38f);
        [ColorUsage(false, true)] public Color orangeColor = new Color(1f,.39f,.045f);
        [ColorUsage(false, true)] public Color rimColor = new Color(.8f,.095f,.018f);
        [ColorUsage(false, true)] public Color groundColor = new Color(.014f,.014f,.019f);
        [Header("形状 / 单位：度")]
        [Range(-80,80)] public float horizonHeight = 0;
        [Range(.1f,20)] public float warmBandWidth = 2;
        [Range(1,80)] public float transitionHeight = 12;
        [Range(.01f,3)] public float edgeSoftness = .18f;
        [Range(0,30)] public float curvature = 6;
        [Header("方向与动画")]
        public float rotation;
        [Range(-90,90)] public float tilt;
        [Range(0,1)] public float directionalWarmth = .25f;
        [Range(0,8)] public float exposure = 1;
        [Tooltip("0 为当前夕阳配色，1 为深蓝夜空；可在 Timeline 中打关键帧。")]
        [Range(0,1)] public float nightBlend;
        Material runtimeMaterial;
        sealed class Binding { public Camera camera; public Skybox skybox; public bool created, enabled; public Material material; public CameraClearFlags flags; }
        readonly List<Binding> bindings = new();
        void OnEnable() { Apply(); }
        void LateUpdate() { Apply(); }
        void OnDidApplyAnimationProperties() { Apply(); }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void DestroyOwned(Object obj) { if (!obj) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void ReleaseBinding(Binding b)
        {
            if(b.camera && b.camera.clearFlags == CameraClearFlags.Skybox) b.camera.clearFlags=b.flags;
            if(b.skybox && b.skybox.material==runtimeMaterial)
            { if(b.created) DestroyOwned(b.skybox); else {b.skybox.material=b.material;b.skybox.enabled=b.enabled;} }
        }
        public void SuspendPreview() { Release(); }
        void Release() { foreach(var b in bindings) ReleaseBinding(b); bindings.Clear(); DestroyOwned(runtimeMaterial); runtimeMaterial=null; }
        public void Apply()
        {
            if(!isActiveAndEnabled) return;
            if(!skyShader) skyShader=Shader.Find("TRINL/Art Directed Sunset Sky");
            if(!skyShader) return;
            if(!runtimeMaterial) runtimeMaterial=new Material(skyShader){name="Sunset Sky (Preview Instance)",hideFlags=HideFlags.HideAndDontSave};
            for(int i=bindings.Count-1;i>=0;i--)
                if(!bindings[i].camera || targetCameras==null || System.Array.IndexOf(targetCameras,bindings[i].camera)<0)
                {ReleaseBinding(bindings[i]);bindings.RemoveAt(i);}
            if(targetCameras!=null) foreach(var c in targetCameras)
            {
                if(!c) continue;
                if(bindings.Exists(b=>b.camera==c)) continue;
                var sky=c.GetComponent<Skybox>();
                var b=new Binding{camera=c,skybox=sky,created=!sky,enabled=sky&&sky.enabled,material=sky?sky.material:null,flags=c.clearFlags};
                if(!sky){sky=c.gameObject.AddComponent<Skybox>();sky.hideFlags=HideFlags.HideAndDontSave;b.skybox=sky;}
                sky.material=runtimeMaterial;sky.enabled=true;c.clearFlags=CameraClearFlags.Skybox;bindings.Add(b);
            }
            float n=Mathf.Clamp01(nightBlend);
            runtimeMaterial.SetColor("_Zenith",Color.Lerp(zenithColor,new Color(.002f,.005f,.016f),n).linear);
            runtimeMaterial.SetColor("_Blue",Color.Lerp(blueColor,new Color(.016f,.035f,.08f),n).linear);
            runtimeMaterial.SetColor("_Haze",Color.Lerp(hazeColor,new Color(.04f,.05f,.09f),n).linear);
            runtimeMaterial.SetColor("_Orange",Color.Lerp(orangeColor,new Color(.055f,.065f,.10f),n).linear);
            runtimeMaterial.SetColor("_Rim",Color.Lerp(rimColor,new Color(.02f,.025f,.05f),n).linear);
            runtimeMaterial.SetColor("_Ground",Color.Lerp(groundColor,Color.black,n).linear);
            runtimeMaterial.SetVector("_Shape",new Vector4(horizonHeight,warmBandWidth,transitionHeight,edgeSoftness));
            runtimeMaterial.SetVector("_Orientation",new Vector4(rotation,tilt,curvature,directionalWarmth));
            runtimeMaterial.SetFloat("_Exposure",exposure);
        }
    }
}
