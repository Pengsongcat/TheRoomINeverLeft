using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorExitDissolve : MonoBehaviour
{
    public DoorPortal portal;
    [Tooltip("Cross this plane along its blue +Z axis after entering through the door.")]
    public Transform exitPlane;
    public Vector2 exitHalfSize = new Vector2(2.4f,2f);
    public Renderer[] doorRenderers;
    public Shader dissolveShader;
    [Min(0f)] public float delay = 0.3f;
    [Min(0.01f)] public float duration = 3f;
    [Min(0f)] public float edgeIrregularity = 0.06f;
    [Min(0.1f)] public float noiseScale = 12f;
    [Tooltip("Lights that fade to black in sync with the door dissolve.")]
    public Light[] lightsToFade;
    public bool Triggered { get; private set; }
    public float Progress { get; private set; }
    Camera trackedCamera;
    Vector3 previous;
    bool sampled, entered;
    float elapsed, top, bottom;
    Material[][] originals, copies;
    MaterialPropertyBlock block;
    float[] lightInitialIntensities;

    void Update()
    {
        var camera = Camera.main;
        if (!Triggered && camera) ObserveCamera(camera);
        if (Triggered) Advance(Time.deltaTime);
    }
    public void ObserveCamera(Camera camera)
    {
        if (!camera || !portal || !exitPlane || Triggered) return;
        if (trackedCamera != camera) { trackedCamera = camera; sampled = false; entered = false; }
        entered |= portal.IsCameraInside(camera);
        Vector3 point = exitPlane.InverseTransformPoint(camera.transform.position);
        if (sampled && entered && previous.z < 0 && point.z >= 0)
        {
            Vector3 crossing = Vector3.Lerp(previous,point,-previous.z/(point.z-previous.z));
            if (Mathf.Abs(crossing.x)<=exitHalfSize.x && Mathf.Abs(crossing.y)<=exitHalfSize.y) BeginDissolve();
        }
        previous=point; sampled=true;
    }
    public void BeginDissolve()
    {
        if (Triggered || doorRenderers==null || doorRenderers.Length==0 || !dissolveShader) return;
        originals=new Material[doorRenderers.Length][];copies=new Material[doorRenderers.Length][];
        top=float.NegativeInfinity;bottom=float.PositiveInfinity;
        for(int i=0;i<doorRenderers.Length;i++)
        {
            var renderer=doorRenderers[i];if(!renderer)continue;
            top=Mathf.Max(top,renderer.bounds.max.y);bottom=Mathf.Min(bottom,renderer.bounds.min.y);
            originals[i]=renderer.sharedMaterials;copies[i]=new Material[originals[i].Length];
            for(int j=0;j<copies[i].Length;j++)
            {
                if(!originals[i][j])continue;
                var material=new Material(originals[i][j]);material.shader=dissolveShader;
                material.SetFloat("_CutY",10000f);material.SetFloat("_NoiseHeight",edgeIrregularity);material.SetFloat("_NoiseScale",noiseScale);
                copies[i][j]=material;
            }
            renderer.sharedMaterials=copies[i];
        }
        if(float.IsInfinity(top)){Restore();return;}
        if(lightsToFade!=null && lightsToFade.Length>0)
        {
            lightInitialIntensities=new float[lightsToFade.Length];
            for(int i=0;i<lightsToFade.Length;i++)lightInitialIntensities[i]=lightsToFade[i]?lightsToFade[i].intensity:0f;
        }
        elapsed=0;Progress=0;Triggered=true;
    }
    public void Advance(float deltaTime)
    {
        if(!Triggered)return;
        elapsed+=Mathf.Max(0,deltaTime);
        Progress=Mathf.Clamp01((elapsed-delay)/Mathf.Max(0.01f,duration));
        float t=Progress*Progress*(3f-2f*Progress);
        float cut=Mathf.Lerp(top+edgeIrregularity+0.01f,bottom-edgeIrregularity-0.01f,t);
        foreach(var renderer in doorRenderers) SetCut(renderer,cut);
        if(lightsToFade!=null && lightInitialIntensities!=null)
            for(int i=0;i<lightsToFade.Length;i++)
                if(lightsToFade[i]) lightsToFade[i].intensity=Mathf.Lerp(lightInitialIntensities[i],0f,t);
    }
    void SetCut(Renderer renderer,float cut)
    {
        if(!renderer)return;
        if(block==null)block=new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);block.SetFloat("_CutY",cut);renderer.SetPropertyBlock(block);
    }
    public void Restore()
    {
        if(originals!=null)for(int i=0;i<originals.Length;i++)
        {
            if(doorRenderers[i] && originals[i]!=null){doorRenderers[i].sharedMaterials=originals[i];SetCut(doorRenderers[i],10000f);}
            if(copies[i]!=null)foreach(var material in copies[i])if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        }
        if(lightsToFade!=null && lightInitialIntensities!=null)
            for(int i=0;i<lightsToFade.Length;i++)
                if(lightsToFade[i]) lightsToFade[i].intensity=lightInitialIntensities[i];
        originals=null;copies=null;lightInitialIntensities=null;Triggered=false;Progress=0;elapsed=0;sampled=false;entered=false;
    }
    void OnDisable(){Restore();}
    void OnDrawGizmosSelected()
    {
        if(!exitPlane)return;
        var old=Gizmos.matrix;Gizmos.matrix=exitPlane.localToWorldMatrix;Gizmos.color=Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero,new Vector3(exitHalfSize.x*2,exitHalfSize.y*2,0.02f));
        Gizmos.DrawLine(Vector3.zero,Vector3.forward);Gizmos.matrix=old;
    }
}
