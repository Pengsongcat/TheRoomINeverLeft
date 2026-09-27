using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;

[InitializeOnLoad]
public static class DoorAudioEditorUtility
{
    static readonly System.Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
    static readonly MethodInfo play = audioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    static readonly MethodInfo stop = audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    static AudioClip preview;
    public static bool PreviewSupported => play != null && stop != null;
    static DoorAudioEditorUtility()
    {
        DoorAudioController.PreviewPlay = Play;
        DoorAudioController.PreviewStop = Stop;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }
    static void Play(AudioClip clip, float volume)
    {
        Stop();
        if (!clip) return;
        if (!PreviewSupported) { Debug.LogWarning("This Unity version does not support door audio preview."); return; }
        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0)) { Debug.LogWarning("Door preview needs decompressed audio."); return; }
        for (int i=0;i<data.Length;i++) data[i] *= Mathf.Clamp01(volume);
        preview = AudioClip.Create("Door audio preview", clip.samples, clip.channels, clip.frequency, false);
        preview.hideFlags = HideFlags.HideAndDontSave;
        preview.SetData(data, 0);
        play.Invoke(null, new object[] { preview, 0, false });
    }
    static void Stop()
    {
        if (!preview) return;
        stop?.Invoke(null, null);
        Object.DestroyImmediate(preview);
        preview = null;
    }

    // Offline small-room approximation: four damped comb reflections, RT60 ~0.65s.
    // Both edit preview and runtime use this same rendered clip. Originals stay untouched.
    public static void BakeRoom(string inputPath, string outputPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(inputPath);
        if (!clip) throw new System.Exception("Missing audio: " + inputPath);
        var dry = new float[clip.samples * clip.channels];
        if (!clip.GetData(dry,0)) throw new System.Exception("Cannot read audio: " + inputPath);
        int count=clip.samples + Mathf.CeilToInt(clip.frequency * 0.8f);
        var mono = new float[count];
        for(int i=0;i<clip.samples;i++) for(int ch=0;ch<clip.channels;ch++) mono[i]+=dry[i*clip.channels+ch]/clip.channels;
        var wet = new float[count];
        foreach(float seconds in new[]{0.0292f,0.0371f,0.0417f,0.0533f})
        {
            int delay=Mathf.RoundToInt(seconds*clip.frequency);
            var ring=new float[delay]; float low=0; float feedback=Mathf.Pow(0.001f,seconds/0.65f);
            for(int i=0;i<count;i++) { int p=i%delay;float echo=ring[p];low=Mathf.Lerp(low,echo,0.35f);ring[p]=mono[i]+low*feedback;wet[i]+=echo*0.25f; }
        }
        float peak=0;
        for(int i=0;i<count;i++){mono[i]+=wet[i]*0.22f;peak=Mathf.Max(peak,Mathf.Abs(mono[i]));}
        float gain=peak>0.95f?0.95f/peak:1f;
        using(var writer=new BinaryWriter(File.Create(outputPath)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(clip.frequency);writer.Write(clip.frequency*2);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
            for(int i=0;i<count;i++)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(mono[i]*gain,-1,1)*32767));
        }
        AssetDatabase.ImportAsset(outputPath);
    }
}
