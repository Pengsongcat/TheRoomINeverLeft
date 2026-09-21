using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

// Frame rate lines: fps against the headset refresh rate, average frame time and the worst frame of the last
// second. fps is green at 95 % of the refresh rate or better, yellow down to 80 %, red below.
// On Air Link this is the PC's render rate; frames lost in streaming don't show here.
public class FrameRateText : DebugTextSource
{
    int   _frames;
    float _worstFrame;
    float _worstFrameShown;
    float _worstResetTime;

    public override void Begin()
    {
        _frames = 0;
        _worstFrame = 0f;
        _worstFrameShown = 0f;
        _worstResetTime = Time.unscaledTime;
    }

    public override void Sample(float deltaTime)
    {
        _frames++;
        _worstFrame = Mathf.Max(_worstFrame, deltaTime);

        if (Time.unscaledTime - _worstResetTime >= 1f)
        {
            _worstFrameShown = _worstFrame;
            _worstFrame      = 0f;
            _worstResetTime  = Time.unscaledTime;
        }
    }

    public override void Write(StringBuilder text, float windowSeconds)
    {
        if (_frames == 0 || windowSeconds <= 0f)
        {
            text.Append("– fps\n– ms");
            return;
        }

        float fps   = _frames / windowSeconds;
        float hz    = RefreshRate();
        var   color = hz <= 0f || fps >= hz * 0.95f ? "#6f6" : fps >= hz * 0.8f ? "#ff6" : "#f66";
        text.Append($"<color={color}>{fps:F0} fps</color>");
        if (hz > 0f) text.Append($" / {hz:F0} Hz");
        text.Append($"\n{1000f / fps:F1} ms · worst {_worstFrameShown * 1000f:F0} ms");
        _frames = 0;
    }

    static readonly List<XRDisplaySubsystem> Displays = new();

    static float RefreshRate()
    {
        SubsystemManager.GetSubsystems(Displays);
        foreach (var d in Displays)
            if (d.running && d.TryGetDisplayRefreshRate(out float hz)) return hz;
        return 0f;
    }
}
