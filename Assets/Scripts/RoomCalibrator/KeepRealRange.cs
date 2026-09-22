using UnityEngine;

// Put on a sound or a trigger under Room that should keep its authored range in metres whatever the room is,
// the range counterpart of KeepRealSize. Room Scaled Ranges skips this object and everything under it, so an
// AudioSource keeps its authored min / max distance and a trigger sphere its authored world radius.
//
// Its position still follows the room; only the reach stays put. Use it where the reach is about the visitor
// rather than the layout (an arm's length, a step). Leave it off for anything that should shrink with the room.
public class KeepRealRange : MonoBehaviour
{
}
