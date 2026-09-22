using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

// Each story part's Timeline holds nothing but two Control clips, one driving its Sound prefab's Audio sub-timeline
// and one its Visual prefab's Visual sub-timeline. A Control clip's length does not follow the sub-timeline it
// drives, so a sub-timeline that grows past its clip is cut off without a word.
//
// The scene is as long as its sound, so both clips are sized to the Audio sub-timeline. Run this after editing any
// sub-timeline. Parts with no sound yet (the placeholders) keep the 10 s they were authored with.
public static class StorySubTimelineLengths
{
    const string StoryRoot   = "Assets/Prefabs/Story";
    const double PlaceholderLength = 10.0;

    [MenuItem("Tools/TRINL/Re-sync Story Timeline Lengths")]
    static void Resync()
    {
        var changed = new List<string>();
        int checkedParts = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { StoryRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!name.EndsWith(" Timeline") || name.EndsWith(" Audio Timeline") || name.EndsWith(" Visual Timeline"))
                continue;

            var parent = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            var part   = name.Substring(0, name.Length - " Timeline".Length);
            var audio  = AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                Path.GetDirectoryName(path).Replace('\\', '/') + "/" + part + " Audio Timeline.playable");
            if (audio == null) { Debug.LogWarning($"[StoryTimelines] {part} has no Audio sub-timeline."); continue; }

            checkedParts++;
            var length = audio.duration > 0.01 ? audio.duration : PlaceholderLength;

            foreach (var track in parent.GetOutputTracks())
            {
                if (!(track is ControlTrack)) continue;
                foreach (var clip in track.GetClips())
                {
                    if (Mathf.Approximately((float)clip.duration, (float)length) && clip.start == 0) continue;
                    changed.Add($"{part} / {clip.displayName}: {clip.duration:0.0} > {length:0.0} s");
                    clip.start    = 0;
                    clip.duration = length;
                }
            }
            EditorUtility.SetDirty(parent);
        }

        AssetDatabase.SaveAssets();
        if (changed.Count == 0)
            Debug.Log($"[StoryTimelines] {checkedParts} parts checked; every Control clip already matches its sound.");
        else
            Debug.Log($"[StoryTimelines] {checkedParts} parts checked, {changed.Count} clip(s) resized:\n  " + string.Join("\n  ", changed));
    }
}
