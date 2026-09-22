using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering.Editor
{
    public static class MeshFogLightingValidation
    {
        public static string Run(MeshFog example)
        {
            string directory = Path.GetFullPath("Artifacts/MeshFog/Lighting");
            Directory.CreateDirectory(directory);
            var report = new StringBuilder();
            GameObject clone = null, cameraObject = null, lightObject = null, blocker = null;
            Material blockerMaterial = null;
            RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                clone = UnityEngine.Object.Instantiate(example.gameObject);
                clone.name = "Fog lighting validation (temporary)";
                clone.transform.SetPositionAndRotation(new Vector3(1000, 1000, 1000), Quaternion.identity);
                clone.transform.localScale = Vector3.one;
                var fog = clone.GetComponent<MeshFog>();
                fog.fogColor = Color.white;
                fog.flowSpeed = Vector3.zero;
                fog.noiseStrength = 0;
                fog.contourNoise = 0;
                fog.enableLighting = true;
                fog.ambientBrightness = 0;
                fog.lightIntensity = 1;
                fog.anisotropy = 0;
                fog.receiveLightShadows = true;
                fog.showFog = true;
                fog.Apply();
                var bounds = fog.volumeRenderer.bounds;
                float radius = bounds.extents.magnitude;
                var center = bounds.center;
                cameraObject = new GameObject("Fog lighting test camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = center + new Vector3(0, 0.65f, -1).normalized * radius * 3;
                camera.transform.LookAt(center);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = radius * 8;
                camera.fieldOfView = 45;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf);
                target.Create();
                camera.targetTexture = target;
                lightObject = new GameObject("Fog lighting test light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = radius * 8;
                light.intensity = radius * radius * 8;
                light.color = Color.red;
                light.transform.position = center + Vector3.up * radius * 2;
                light.shadows = LightShadows.Hard;

                Color Capture(string name)
                {
                    fog.Apply();
                    camera.Render();
                    RenderTexture.active = target;
                    var texture = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                        texture.Apply();
                        var mean = Color.black;
                        foreach (var pixel in texture.GetPixels()) mean += pixel / (256f * 256f);
                        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
                        report.AppendLine(name + " mean RGB: " + mean);
                        return mean;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
                void Check(bool success, string message)
                {
                    if (!success) throw new Exception(message);
                    report.AppendLine("PASS " + message);
                }
                light.enabled = false;
                var dark = Capture("01-no-light");
                light.enabled = true;
                var red = Capture("02-red-point");
                Check(red.r > dark.r + 0.005f && red.r > red.b + 0.005f, "Point light illuminates fog and transfers color");
                light.color = Color.blue;
                var blue = Capture("03-blue-point");
                Check(blue.b > blue.r + 0.005f, "Changing light color changes fog color");
                light.range = radius * 0.1f;
                var outOfRange = Capture("04-out-of-range");
                Check(outOfRange.b < blue.b * 0.25f, "Point light range controls fog illumination");
                light.range = radius * 8;
                light.type = LightType.Spot;
                light.spotAngle = 65;
                light.innerSpotAngle = 40;
                light.transform.LookAt(center);
                var spot = Capture("05-spot-on");
                light.transform.rotation *= Quaternion.Euler(0, 180, 0);
                var away = Capture("06-spot-away");
                Check(spot.b > away.b + 0.005f, "Spot cone illuminates only its aimed region");
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1;
                light.transform.LookAt(center);
                var directional = Capture("07-directional");
                Check(directional.r > dark.r + 0.005f, "Directional light illuminates fog");
                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = center + Vector3.up * radius * 1.25f;
                blocker.transform.localScale = new Vector3(radius * 3, radius * 0.1f, radius * 3);
                blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                blocker.GetComponent<Renderer>().sharedMaterial = blockerMaterial;
                var shadow = Capture("08-shadow");
                fog.receiveLightShadows = false;
                var noShadow = Capture("09-shadows-disabled");
                Check(noShadow.r > shadow.r + 0.003f, "Scene occluder casts a shadow into fog; toggle restores illumination");
                blocker.SetActive(false);
                fog.enableLighting = false;
                var legacyOn = Capture("10-legacy-light-on");
                light.enabled = false;
                var legacyOff = Capture("11-legacy-light-off");
                Check(Mathf.Abs(legacyOn.r - legacyOff.r) < 0.0001f, "Lighting disabled preserves light-independent appearance");
                report.AppendLine("Desktop camera only; Quest stereo, Android and GPU performance are unverified.");
                return report.ToString();
            }
            finally
            {
                File.WriteAllText(Path.Combine(directory, "validation.txt"), report.ToString());
                RenderTexture.active = previous;
                if (cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (blocker) UnityEngine.Object.DestroyImmediate(blocker);
                if (blockerMaterial) UnityEngine.Object.DestroyImmediate(blockerMaterial);
                if (lightObject) UnityEngine.Object.DestroyImmediate(lightObject);
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
            }
        }
    }
}
