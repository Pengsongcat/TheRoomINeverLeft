using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Audio Sources the Listening Mode window has changed, with their own settings, kept in the project's Library
// folder (not in version control) so they survive script reloads and editor restarts.
[FilePath("Library/ListeningMode.asset", FilePathAttribute.Location.ProjectFolder)]
public class ListeningModeStore : ScriptableSingleton<ListeningModeStore>
{
    public enum Mode { Flat, Unity3D }

    [System.Serializable]
    public class Record
    {
        public string         Id;           // GlobalObjectId of the Audio Source
        public string         Label;        // scene: hierarchy path, for the list
        public string         ScenePath;
        public Mode           Mode;
        public bool           Spatialize;
        public float          SpatialBlend;
        public float[]        CurveTimes;   // the whole Spatial Blend curve (by distance), when it has more than one
        public float[]        CurveValues;  // key; plain numbers, which the Library file keeps
    }

    public List<Record> Records = new();

    public void Save() => Save(true);
}
