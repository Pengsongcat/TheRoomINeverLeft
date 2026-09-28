using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorExitDissolve : MonoBehaviour
{
    public DoorPortal portal;
    [Tooltip("When set, walking within Start Radius of it (horizontally; height is ignored) after walking through " +
             "the door starts the dissolve, the game and the passthrough fade together. Empty: they start when the " +
             "visitor walks through the door.")]
    public Transform startPoint;
    [Min(0.01f)] public float startRadius = 0.5f;
    [Tooltip("Without a Start Point: start as soon as the visitor enters through the door, the same moment the " +
             "passthrough fade starts. Off: start when they cross the exit plane instead. Delay still applies.")]
    public bool triggerOnEnter = true;
    [Tooltip("Cross this plane along its blue +Z axis after entering through the door. Unused with Trigger On Enter.")]
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
    [Tooltip("On Room. Told to start, with the door room's opacity, when the dissolve begins: it fades the door " +
             "room out while the passthrough turns dark grey, then turns the MR Portal off. Empty: nothing else fades.")]
    public RoomPassthroughTransition roomTransition;
    public bool Triggered { get; private set; }
    public float Progress { get; private set; }
    public bool IsComplete => completed;
    // Raised once in Play Mode when the start trigger is reached: the Start Point's radius, or without one the door
    // (the passthrough fade starts then). DoorGameStart starts the game on it.
    public event Action Started;
    // Raised once in Play Mode when the door has fully dissolved.
    public event Action Completed;
    Camera trackedCamera;
    Vector3 previous;
    bool sampled, entered, started, completed;
    float elapsed, top, bottom;
    Material[][] originals, copies;
    MaterialPropertyBlock block;
    float[] lightInitialIntensities;
    float nextDiagnosticTime;

    void Update()
    {
        // With a Start Point the door's passthrough fade waits for it instead of starting at the doorway.
        if (portal) portal.holdInsideFade = startPoint;
        var camera = Camera.main;
        if (!Triggered && camera) ObserveCamera(camera);
        if (Application.isPlaying && !Triggered && Time.unscaledTime >= nextDiagnosticTime)
        {
            nextDiagnosticTime = Time.unscaledTime + 5f;
            Vector3 offset = camera && startPoint ? camera.transform.position - startPoint.position : Vector3.zero;
            offset.y = 0f;
            Debug.Log($"[DoorEntry] camera={(camera ? camera.name : "missing")}; entered={entered}; " +
                $"head={(camera ? camera.transform.position : Vector3.zero)}; " +
                $"start={(startPoint ? startPoint.position : Vector3.zero)}; horizontalDistance={offset.magnitude:F2}; radius={startRadius:F2}; " +
                $"portalActive={(portal && portal.isActiveAndEnabled)}", this);
        }
        if (Triggered) Advance(Time.deltaTime);
    }
    public void ObserveCamera(Camera camera)
    {
        if (!camera || !portal || Triggered) return;
        if (trackedCamera != camera) { trackedCamera = camera; sampled = false; entered = false; }
        entered |= portal.IsCameraInside(camera);
        if (startPoint)
        {
            // Only after walking through the door, so reaching the point some other way does not count.
            if (!entered) return;
            Vector3 offset = camera.transform.position - startPoint.position; offset.y = 0f;
            if (offset.sqrMagnitude <= startRadius * startRadius) { FireStarted(); portal.StartInsideFade(); BeginDissolve(); }
            return;
        }
        if (entered) FireStarted();
        if (triggerOnEnter) { if (entered) BeginDissolve(); return; }
        if (!exitPlane) return;
        Vector3 point = exitPlane.InverseTransformPoint(camera.transform.position);
        if (sampled && entered && previous.z < 0 && point.z >= 0)
        {
            Vector3 crossing = Vector3.Lerp(previous,point,-previous.z/(point.z-previous.z));
            if (Mathf.Abs(crossing.x)<=exitHalfSize.x && Mathf.Abs(crossing.y)<=exitHalfSize.y) BeginDissolve();
        }
        previous=point; sampled=true;
    }
    void FireStarted()
    {
        if (started) return;
        started = true;
        if (Application.isPlaying) Started?.Invoke();
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
            var used=new Material[originals[i].Length];
            for(int j=0;j<used.Length;j++)
            {
                used[j]=originals[i][j];
                // Already cuttable (e.g. the MR Portal's stencil mark): keep it, or it would lose its job.
                if(!used[j] || used[j].HasProperty("_CutY"))continue;
                var material=new Material(originals[i][j]);material.shader=dissolveShader;
                material.SetFloat("_CutY",10000f);material.SetFloat("_NoiseHeight",edgeIrregularity);material.SetFloat("_NoiseScale",noiseScale);
                copies[i][j]=used[j]=material;
            }
            renderer.sharedMaterials=used;
        }
        if(float.IsInfinity(top)){Restore();return;}
        if(lightsToFade!=null && lightsToFade.Length>0)
        {
            lightInitialIntensities=new float[lightsToFade.Length];
            for(int i=0;i<lightsToFade.Length;i++)lightInitialIntensities[i]=lightsToFade[i]?lightsToFade[i].intensity:0f;
        }
        elapsed=0;Progress=0;completed=false;Triggered=true;
        // Hand the door room's current opacity to Room so it can fade the room and the passthrough together.
        if(roomTransition && Application.isPlaying && portal)
            roomTransition.Begin(portal,portal.GetInsideWallOpacity(trackedCamera?trackedCamera:Camera.main),this);
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
        // Only in Play Mode: the Inspector preview must not start the game.
        if(Progress>=1f && !completed && Application.isPlaying)
        {
            completed=true;
            Debug.Log("[DoorExitDissolve] Door fully dissolved.",this);
            Completed?.Invoke();
        }
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
        originals=null;copies=null;lightInitialIntensities=null;Triggered=false;Progress=0;elapsed=0;sampled=false;entered=false;started=false;completed=false;
    }
    void OnDisable(){Restore();}
    // Always drawn, so the Start Point's reach shows in the Scene view without selecting the door.
    void OnDrawGizmos()
    {
        if(!startPoint)return;
        Gizmos.color=started?Color.green:new Color(1f,0.6f,0f);
        Vector3 c=startPoint.position;const int n=48;
        for(int i=0;i<n;i++)
        {
            float a=i*Mathf.PI*2f/n,b=(i+1)*Mathf.PI*2f/n;
            Vector3 p=c+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*startRadius,q=c+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*startRadius;
            Gizmos.DrawLine(p,q);Gizmos.DrawLine(p+Vector3.up*2f,q+Vector3.up*2f); // height is ignored: a column
            if(i%12==0)Gizmos.DrawLine(p,p+Vector3.up*2f);
        }
        Gizmos.DrawWireSphere(c,0.05f);
    }
    void OnDrawGizmosSelected()
    {
        if(!exitPlane)return;
        var old=Gizmos.matrix;Gizmos.matrix=exitPlane.localToWorldMatrix;Gizmos.color=Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero,new Vector3(exitHalfSize.x*2,exitHalfSize.y*2,0.02f));
        Gizmos.DrawLine(Vector3.zero,Vector3.forward);Gizmos.matrix=old;
    }
}
