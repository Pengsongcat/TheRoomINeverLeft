using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TRINL.Rendering.Editor
{
    /// <summary>Explicit, on-demand desktop check. Never runs automatically or changes saved settings.</summary>
    public static class SpatialGhostsValidation
    {
        [MenuItem("Tools/TRINL/Validate Selected Spatial Ghosts")]
        public static void Run()
        {
            var effect = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SpatialGhosts>() : null;
            if (effect == null) throw new InvalidOperationException("Select a SpatialGhosts object first.");
            var renderer = effect.Root.GetComponentInChildren<Renderer>();
            var originalMaterials = renderer.sharedMaterials;
            int count = effect.Root.GetComponentsInChildren<Transform>(true).Length;
            var sourceRenderers = effect.Root.GetComponentsInChildren<Renderer>(true);
            var layers = new int[sourceRenderers.Length];
            for (int i = 0; i < layers.Length; i++) layers[i] = sourceRenderers[i].gameObject.layer;
            bool wasEnabled = effect.enabled;
            var oldActive = RenderTexture.active;
            var cameraObject = new GameObject("SpatialGhosts validation camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            string folder = "Library/SpatialGhostsValidation";
            Directory.CreateDirectory(folder);
            try
            {
                for (int i = 0; i < layers.Length; i++) sourceRenderers[i].gameObject.layer = 31;
                var center = effect.Root.TransformPoint(effect.pivotOffset);
                var axis = effect.WorldDirection;
                var point = effect.GetGhostMatrix(Matrix4x4.identity, 1).MultiplyPoint3x4(center);
                if (Vector3.Distance(point, center + axis * effect.spacing) > 0.0001f)
                    throw new Exception("Ghost pivot offset is incorrect.");
                var axial = effect.GetGhostMatrix(Matrix4x4.identity, 1).MultiplyVector(axis).magnitude;
                if (Mathf.Abs(axial - (1 + effect.stretchPerStep)) > 0.0001f)
                    throw new Exception("Axial stretch is incorrect.");

                float distance = Mathf.Max(2.5f, renderer.bounds.size.magnitude * 2.2f + effect.spacing * effect.copiesPerSide);
                var front = Vector3.Cross(axis, Vector3.up).normalized;
                if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
                camera.transform.position = center + front * distance;
                camera.transform.LookAt(center);
                effect.enabled = false;
                int off = Capture(camera, pixels, folder + "/disabled.png");
                var baseline = pixels.GetPixels32();
                effect.enabled = true;
                int on = Capture(camera, pixels, folder + "/front.png");
                int changed = 0;
                var enabledPixels = pixels.GetPixels32();
                for (int i = 0; i < baseline.Length; i++)
                    if (!baseline[i].Equals(enabledPixels[i])) changed++;
                camera.transform.position = center + (front + axis).normalized * distance;
                camera.transform.LookAt(center);
                int oblique = Capture(camera, pixels, folder + "/oblique.png");
                if (changed < 50 || on < 50 || oblique < 50)
                    throw new Exception($"Draw/disable check failed: off={off}, on={on}, oblique={oblique}");

                var after = renderer.sharedMaterials;
                for (int i = 0; i < after.Length; i++)
                    if (after[i] != originalMaterials[i]) throw new Exception("Original material changed.");
                int finalCount = effect.Root.GetComponentsInChildren<Transform>(true).Length;
                if (finalCount != count) throw new Exception("Unexpected source hierarchy object creation.");
                // Changing the source layer must remove both source and ghosts from this camera.
                for (int i = 0; i < layers.Length; i++) sourceRenderers[i].gameObject.layer = 30;
                int excluded = Capture(camera, pixels, folder + "/layer-excluded.png");
                if (excluded != 0) throw new Exception("Ghosts did not follow the source layer change.");
                string report = $"PASS: center offset and axial stretch; original-only pixels={off}; ghost-change pixels={changed}; front pixels={on}; oblique pixels={oblique}; source-layer-excluded pixels={excluded}; original material references preserved; no model GameObjects cloned.\nDesktop camera rendering only. Quest stereo, Android build and GPU budget not tested.\n";
                File.WriteAllText(folder + "/validation.txt", report);
                Debug.Log("SpatialGhosts " + report);
            }
            finally
            {
                for (int i = 0; i < layers.Length; i++) sourceRenderers[i].gameObject.layer = layers[i];
                effect.enabled = wasEnabled;
                camera.targetTexture = null;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                SceneView.RepaintAll();
            }
        }

        static int Capture(Camera camera, Texture2D pixels, string path)
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
            pixels.Apply();
            int lit = 0;
            foreach (var pixel in pixels.GetPixels32())
                if (pixel.r > 3 || pixel.g > 3 || pixel.b > 3) lit++;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return lit;
        }
    }
}
