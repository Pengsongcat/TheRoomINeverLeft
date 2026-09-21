using System.Text;
using UnityEngine;

// A block of lines in the Debug Text Readout. Put it as a child of the readout; blocks stack in child order.
// The readout calls these only while it is visible.
public abstract class DebugTextSource : MonoBehaviour
{
    // The readout was just shown: start measuring afresh.
    public virtual void Begin() { }

    // Every frame, for sources that measure over time.
    public virtual void Sample(float deltaTime) { }

    // Append this block's lines (no trailing newline). windowSeconds = time since the last Write.
    public abstract void Write(StringBuilder text, float windowSeconds);
}
