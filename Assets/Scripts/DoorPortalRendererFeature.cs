using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public sealed class DoorPortalRendererFeature : ScriptableRendererFeature
{
    PortalPass pass;
    public override void Create() { pass = new PortalPass { renderPassEvent = (RenderPassEvent)449 }; }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (data.cameraData.cameraType == CameraType.Preview || data.cameraData.cameraType == CameraType.Reflection) return;
        if (DoorPortal.Active.Count > 0) renderer.EnqueuePass(pass);
    }
    sealed class Draw { public Renderer renderer; public Material material; public int submesh; public int pass; public float opacity; public bool litDoors; }
    sealed class PassData { public List<Draw> draws; public RendererListHandle litDoors; }
    sealed class PortalPass : ScriptableRenderPass
    {
        bool loggedXR;
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var resources=frame.Get<UniversalResourceData>();
            var camera=frame.Get<UniversalCameraData>().camera;
            if(!resources.activeColorTexture.IsValid()||!resources.activeDepthTexture.IsValid())return;
            var draws=new List<Draw>();
            foreach(var portal in DoorPortal.Active)
            {
                if(!portal || !portal.isActiveAndEnabled || !portal.aperture || (camera.cullingMask & (1<<portal.aperture.gameObject.layer))==0)continue;
                portal.Refresh();
                if (portal.IsCameraInside(camera))
                {
                    // Inside the room, bypass the aperture stencil and front-side clip.
                    if(portal.interiorRenderers!=null)foreach(var item in portal.interiorRenderers)Add(draws,item,1,portal.GetInsideWallOpacity(camera));
                    continue;
                }
                Add(draws,portal.aperture);Add(draws,portal.depthReset);
                // A door swinging behind the aperture must be restored into the cleared depth.
                if(portal.retainedRenderers!=null && portal.retainedRenderers.Length>0) draws.Add(new Draw { litDoors=true });
                if(portal.interiorRenderers!=null)foreach(var item in portal.interiorRenderers)Add(draws,item);
            }
            if(draws.Count==0)return;
            var renderingData=frame.Get<UniversalRenderingData>();
            var cameraData=frame.Get<UniversalCameraData>();
            var drawing=RenderingUtils.CreateDrawingSettings(new List<ShaderTagId>{new ShaderTagId("UniversalForward"),new ShaderTagId("UniversalForwardOnly"),new ShaderTagId("SRPDefaultUnlit")},renderingData,cameraData,frame.Get<UniversalLightData>(),cameraData.defaultOpaqueSortFlags);
            var filtering=new FilteringSettings(RenderQueueRange.opaque,camera.cullingMask,DoorPortal.RetainedRenderingLayer);
            var litList=graph.CreateRendererList(new RendererListParams(renderingData.cullResults,drawing,filtering));
            using(var builder=graph.AddRasterRenderPass<PassData>("Door Portal: Aperture, Depth, Interior",out var data))
            {
                data.draws=draws; data.litDoors=litList; builder.UseRendererList(litList);
                builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                if (cameraData.xr.enabled)
                {
                    // Match URP's geometry passes. A non-foveated redraw into the XR
                    // target does not line up with the already-rendered door/depth.
                    bool canFoveate = resources.isActiveTargetBackBuffer ||
                        Application.platform != RuntimePlatform.Android || UnityEngine.Experimental.Rendering.XRSystem.GetRenderViewportScale() == 1f;
                    builder.EnableFoveatedRasterization(cameraData.xr.supportsFoveatedRendering && canFoveate);
                    builder.SetExtendedFeatureFlags(ExtendedFeatureFlags.MultiviewRenderRegionsCompatible);
                    if (!loggedXR)
                    {
                        loggedXR = true;
                        Debug.Log($"[DoorPortalRender] version={Application.version}; camera={camera.name}; " +
                            $"singlePass={cameraData.xr.singlePassEnabled}; foveation={cameraData.xr.supportsFoveatedRendering && canFoveate}; " +
                            $"portals={DoorPortal.Active.Count}; draws={draws.Count}");
                    }
                }
                builder.SetRenderFunc((PassData d,RasterGraphContext context)=>{
                    foreach(var draw in d.draws) if(draw.litDoors) context.cmd.DrawRendererList(d.litDoors); else if(draw.renderer && draw.material)
                    {
                        context.cmd.SetGlobalFloat("_DoorPortalCameraOpacity",draw.opacity);
                        context.cmd.DrawRenderer(draw.renderer,draw.material,draw.submesh,draw.pass);
                    }
                    context.cmd.SetGlobalFloat("_DoorPortalCameraOpacity",1f);
                });
            }
        }
        static void Add(List<Draw> draws,Renderer renderer,int passIndex=0,float opacity=1f)
        {
            if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)return;
            var materials=renderer.sharedMaterials;
            // Skip materials without this pass, e.g. the MR Portal's stencil mark added by PortalDissolve.
            for(int i=0;i<materials.Length;i++)if(materials[i]&&passIndex<materials[i].passCount)draws.Add(new Draw{renderer=renderer,material=materials[i],submesh=i,pass=passIndex,opacity=opacity});
        }
    }
}
