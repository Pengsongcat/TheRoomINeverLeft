using System;
using System.Text;
using UnityEngine;

// Persists the four corner anchor UUIDs to PlayerPrefs so they survive app restarts.
public static class RoomAnchorStore
{
    const string Key = "TRINL_CornerAnchorIds";

    public static void Save(Guid[] ids)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < ids.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(ids[i].ToString());
        }
        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }

    // Returns the stored IDs, or an empty array if none are saved or the data is corrupted.
    public static Guid[] Load()
    {
        var raw = PlayerPrefs.GetString(Key, string.Empty);
        if (string.IsNullOrEmpty(raw)) return Array.Empty<Guid>();

        var parts = raw.Split(',');
        var result = new Guid[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Guid.TryParse(parts[i], out result[i]))
                return Array.Empty<Guid>();
        }
        return result;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
