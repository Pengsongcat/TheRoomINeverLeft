using System;
using UnityEngine;
using UnityEngine.Playables;

// One step of the story (Onboarding, a scene, the Ending). The StoryDirector turns it on, calls Begin, and moves on
// when it raises Finished. Parts never reference each other, so the order lives in one place.
public abstract class StoryPart : MonoBehaviour
{
    public event Action<StoryPart> Finished;

    public abstract void Begin();

    // Development shortcut (Story Skipper): end what is playing now as if it had run to its end.
    public abstract void Skip();

    // Applies the Timeline's last frame, then stops it, so its stopped handler finishes the part the normal way.
    public static void JumpToEnd(PlayableDirector director)
    {
        if (director.state != PlayState.Playing) director.Play();
        director.time = director.duration;
        director.Evaluate();
        director.Stop();
    }

    protected void RaiseFinished()
    {
        Debug.Log($"[Story] {name} finished.");
        Finished?.Invoke(this);
    }
}
