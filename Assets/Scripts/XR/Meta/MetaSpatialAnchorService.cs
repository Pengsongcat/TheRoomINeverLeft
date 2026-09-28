using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MetaSpatialAnchorService : SpatialAnchorService
{
    public override async Task<IAnchorHandle> CreateAnchorAsync(Vector3 position, Quaternion rotation)
    {
        var go = new GameObject("CornerAnchor");
        go.transform.SetPositionAndRotation(position, rotation);
        var ovrAnchor = go.AddComponent<OVRSpatialAnchor>();

        if (!await ovrAnchor.WhenLocalizedAsync())
        {
            Destroy(go);
            return null;
        }
        return new MetaAnchorHandle(ovrAnchor);
    }

    public override async Task<bool> SaveAnchorsAsync(IEnumerable<IAnchorHandle> anchors)
    {
        LastSaveError = null;
        var ovrAnchors = new List<OVRSpatialAnchor>();
        foreach (var h in anchors)
        {
            if (h is not MetaAnchorHandle meta || meta.Anchor == null || !meta.Anchor.Created)
                return SaveFailed("Invalid or uncreated corner anchor.");
            ovrAnchors.Add(meta.Anchor);
        }
        if (ovrAnchors.Count == 0) return SaveFailed("No corner anchors to save.");

        try
        {
            var result = await OVRSpatialAnchor.SaveAnchorsAsync(ovrAnchors);
            if (result.Success) return true;

            string advice = result.Status switch
            {
                OVRAnchor.SaveResult.FailureInsufficientView => "Look around the room to improve tracking, then try again.",
                OVRAnchor.SaveResult.FailureTooDark => "Increase the room lighting, then try again.",
                OVRAnchor.SaveResult.FailureTooBright => "Avoid very bright light, then try again.",
                OVRAnchor.SaveResult.FailurePermissionInsufficient => "Check anchor permissions and Meta app/account trust. See the device log.",
                OVRAnchor.SaveResult.FailureStorageAtCapacity => "Free up anchor storage on the headset.",
                OVRAnchor.SaveResult.FailureRateLimited => "Wait several seconds before trying again.",
                OVRAnchor.SaveResult.FailureUnsupported => "Check the headset OS and spatial anchor runtime support.",
                OVRAnchor.SaveResult.FailurePersistenceNotEnabled => "Anchor persistence is not ready. Check the device log.",
                _ => "Check the device log for details."
            };
            return SaveFailed($"{result.Status} ({(int)result.Status}). {advice}");
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return SaveFailed($"{e.GetType().Name}: {e.Message}");
        }
    }

    bool SaveFailed(string reason)
    {
        LastSaveError = reason;
        Debug.LogError($"[MetaSpatialAnchorService] Save failed: {reason} " +
                       $"Platform={Application.platform}; OS={SystemInfo.operatingSystem}; " +
                       $"app={Application.identifier}; version={Application.version}", this);
        return false;
    }

    public override async Task<IReadOnlyList<IAnchorHandle>> LoadAnchorsAsync(IEnumerable<Guid> ids)
    {
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(ids, unboundAnchors);
        if (!loadResult.Success) return Array.Empty<IAnchorHandle>();

        var handles = new List<IAnchorHandle>();
        foreach (var unbound in unboundAnchors)
        {
            if (!await unbound.LocalizeAsync()) continue;

            var go = new GameObject("LoadedAnchor");
            var anchor = go.AddComponent<OVRSpatialAnchor>();
            unbound.BindTo(anchor);
            await Task.Yield(); // let UpdateTransform run so Position is current
            handles.Add(new MetaAnchorHandle(anchor));
        }
        return handles;
    }

    public override async Task<bool> EraseAnchorsAsync(IEnumerable<IAnchorHandle> anchors)
    {
        var ovrAnchors = new List<OVRSpatialAnchor>();
        foreach (var h in anchors)
        {
            if (h is MetaAnchorHandle meta && meta.Anchor != null) ovrAnchors.Add(meta.Anchor);
        }
        if (ovrAnchors.Count == 0) return true;

        var result = await OVRSpatialAnchor.EraseAnchorsAsync(ovrAnchors, null);

        // Erasing only removes the anchor from storage; the tracked GameObject stays until destroyed.
        // Callers discard erased anchors, so destroy them whether or not the erase succeeded.
        foreach (var a in ovrAnchors)
            if (a != null) Destroy(a.gameObject);

        return result.Success;
    }

    public override async Task<bool> EraseAnchorsByIdAsync(IEnumerable<Guid> ids)
    {
        var list = new List<Guid>(ids);
        if (list.Count == 0) return true;

        var result = await OVRSpatialAnchor.EraseAnchorsAsync(null, list);
        return result.Success;
    }

    public override void ReleaseAnchors(IEnumerable<IAnchorHandle> anchors)
    {
        foreach (var h in anchors)
            if (h is MetaAnchorHandle meta && meta.Anchor != null) Destroy(meta.Anchor.gameObject);
    }

    sealed class MetaAnchorHandle : IAnchorHandle
    {
        public OVRSpatialAnchor Anchor { get; }
        public Guid Uuid           => Anchor.Uuid;
        public Vector3 Position    => Anchor.transform.position;
        public Quaternion Rotation => Anchor.transform.rotation;
        public bool IsLocalized    => Anchor.Localized;

        public MetaAnchorHandle(OVRSpatialAnchor anchor) => Anchor = anchor;
    }
}
