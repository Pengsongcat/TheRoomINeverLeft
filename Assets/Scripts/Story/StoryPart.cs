using System;
using UnityEngine;

// One step of the story (Onboarding, a scene, the Ending). The StoryDirector turns it on, calls Begin, and moves on
// when it raises Finished. Parts never reference each other, so the order lives in one place.
public abstract class StoryPart : MonoBehaviour
{
    public event Action<StoryPart> Finished;

    public abstract void Begin();

    protected void RaiseFinished()
    {
        Debug.Log($"[Story] {name} finished.");
        Finished?.Invoke(this);
    }
}
