using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class GlitchBlurValidation
    {
        [MenuItem("Tools/TRINL/Validate GlitchBlur")]
        public static void Run()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            var effect = data.rendererFeatures.Find(f => f is GlitchBlurRendererFeature) as GlitchBlurRendererFeature;
            if (effect == null) throw new Exception("GlitchBlur not installed.");
            bool wasCylindrical = effect.cylinderTrails;
            bool wasUpward = effect.upwardResponse;
            float savedGaussianRadius = effect.gaussianRadius;
            int glitchLayer = LayerMask.NameToLayer("GlitchBlur"), screenLayer = LayerMask.NameToLayer("ScreenBlur");
            float length = effect.horizontalLength, vertical = effect.verticalSoftness, core = effect.originalWeight, jitter = effect.bandJitter;
            float drift = effect.driftSpeed;
            float outerLength = effect.outerHorizontalLength, outerCore = effect.outerOriginalWeight;
            var oldActive = RenderTexture.active;
            var cameraObject = new GameObject("GlitchBlur validation camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.transform.position = new Vector3(1000, 1000, -5);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.position = new Vector3(1000, 1000, 0);
            probe.transform.localScale = Vector3.one * 0.25f;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", new Color(1, 0.25f, 0.08f));
            probe.GetComponent<Renderer>().sharedMaterial = material;
            string folder = "Library/GlitchBlurValidation";
            Directory.CreateDirectory(folder);
            try
            {
                effect.cylinderTrails = false;
                effect.upwardResponse = false;
                effect.gaussianRadius = 0;
                probe.layer = glitchLayer;
                camera.cullingMask = 1 << glitchLayer;
                effect.verticalSoftness = 0;
                effect.originalWeight = 0;
                effect.bandJitter = 0;
                effect.driftSpeed = 0;
                effect.horizontalLength = 0;
                effect.outerHorizontalLength = 0;
                effect.outerOriginalWeight = 0;
                var baseline = Capture(camera, texture, folder + "/zero-length.png");
                effect.horizontalLength = 100;
                effect.outerHorizontalLength = 100;
                var smear = Capture(camera, texture, folder + "/horizontal-smear.png");
                var a = Extents(baseline);
                var b = Extents(smear);
                if (b.x < a.x + 80 || Mathf.Abs(b.y - a.y) > 8)
                    throw new Exception($"Directional spread failed: baseline={a}, smear={b}");

                effect.horizontalLength = 20;
                effect.originalWeight = 0.65f;
                effect.outerHorizontalLength = 20;
                effect.outerOriginalWeight = 0;
                var shortOuter = Capture(camera, texture, folder + "/outer-short.png");
                effect.outerHorizontalLength = 140;
                var longOuter = Capture(camera, texture, folder + "/outer-long.png");
                int center = 256 * 512 + 256;
                if (Mathf.Abs(shortOuter[center].r - longOuter[center].r) > 1)
                    throw new Exception("Outer length changed the center pixel.");
                if (Extents(longOuter).x < Extents(shortOuter).x + 50)
                    throw new Exception("Outer length did not extend exterior trails.");
                effect.outerOriginalWeight = 1;
                var fadedOuter = Capture(camera, texture, folder + "/outer-original-one.png");
                if (Mathf.Abs(longOuter[center].r - fadedOuter[center].r) > 1)
                    throw new Exception("Outer original weight changed the center pixel.");
                long brightTail = TailEnergy(longOuter), fadedTail = TailEnergy(fadedOuter);
                if (brightTail < 100 || fadedTail > brightTail * 0.05)
                    throw new Exception($"Outer original weight did not fade tails: {brightTail} -> {fadedTail}");

                probe.layer = screenLayer;
                camera.cullingMask = 1 << screenLayer;
                var screenBefore = Capture(camera, texture, folder + "/screenblur-before.png");
                effect.horizontalLength = 220;
                effect.verticalSoftness = 12;
                effect.outerHorizontalLength = 250;
                effect.outerOriginalWeight = 0.3f;
                var screenAfter = Capture(camera, texture, folder + "/screenblur-after.png");
                int difference = 0;
                for (int i = 0; i < screenBefore.Length; i++)
                    if (!screenBefore[i].Equals(screenAfter[i])) difference++;
                if (difference != 0) throw new Exception("GlitchBlur changed ScreenBlur pixels: " + difference);
                string report = $"PASS: horizontal expansion {a.x} -> {b.x} pixels; vertical extent {a.y} -> {b.y}; outer-length width {Extents(shortOuter).x} -> {Extents(longOuter).x}; outer-weight tail energy {brightTail} -> {fadedTail}; center pixel preserved for both outer controls; ScreenBlur isolation changed pixels={difference}.\nDesktop orthographic rendering only; headset/Android/GPU performance not tested.\n";
                File.WriteAllText(folder + "/validation.txt", report);
                Debug.Log("GlitchBlur " + report);
            }
            finally
            {
                effect.cylinderTrails = wasCylindrical;
                effect.upwardResponse = wasUpward;
                effect.gaussianRadius = savedGaussianRadius;
                effect.horizontalLength = length;
                effect.verticalSoftness = vertical;
                effect.originalWeight = core;
                effect.bandJitter = jitter;
                effect.driftSpeed = drift;
                effect.outerHorizontalLength = outerLength;
                effect.outerOriginalWeight = outerCore;
                camera.targetTexture = null;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(probe);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                SceneView.RepaintAll();
            }
        }

        static Color32[] Capture(Camera camera, Texture2D texture, string path)
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return texture.GetPixels32();
        }

        static long TailEnergy(Color32[] pixels)
        {
            long sum = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (Mathf.Abs(i % 512 - 256) > 100) sum += pixels[i].r;
            return sum;
        }

        static Vector2Int Extents(Color32[] pixels)
        {
            int minX = 512, maxX = -1, minY = 512, maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r > 5)
                {
                    int x = i % 512, y = i / 512;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }
    }
}
