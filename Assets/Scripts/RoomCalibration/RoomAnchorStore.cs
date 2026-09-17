using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Persists the four corner anchor UUIDs to PlayerPrefs so they survive app restarts.
// Also keeps a pending-erase list: anchors from a replaced calibration, erased once the new one is confirmed
// (or on the next launch if the app closed first).
public static class RoomAnchorStore
{
    const string Key             = "TRINL_CornerAnchorIds";
    const string PendingEraseKey = "TRINL_PendingEraseAnchorIds";

    public static void Save(Guid[] ids) => Write(Key, ids);

    // Returns the stored IDs, or an empty array if none are saved or the data is corrupted.
    public static Guid[] Load() => Read(Key);

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    // ─── Pending erase ─────────────────────────────────────────────────────────

    public static void MarkForErase(IEnumerable<Guid> ids)
    {
        var all = new List<Guid>(Read(PendingEraseKey));
        foreach (var id in ids)
            if (id != Guid.Empty && !all.Contains(id)) all.Add(id);
        Write(PendingEraseKey, all.ToArray());
    }

    public static Guid[] LoadPendingErase() => Read(PendingEraseKey);

    // Removes only the given IDs, so IDs marked while an erase was in flight are kept.
    public static void ClearPendingErase(IEnumerable<Guid> erased)
    {
        var remaining = new List<Guid>(Read(PendingEraseKey));
        foreach (var id in erased) remaining.Remove(id);
        Write(PendingEraseKey, remaining.ToArray());
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    static void Write(string key, Guid[] ids)
    {
        if (ids.Length == 0)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return;
        }
        var sb = new StringBuilder();
        for (int i = 0; i < ids.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(ids[i].ToString());
        }
        PlayerPrefs.SetString(key, sb.ToString());
        PlayerPrefs.Save();
    }

    static Guid[] Read(string key)
    {
        var raw = PlayerPrefs.GetString(key, string.Empty);
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
}
