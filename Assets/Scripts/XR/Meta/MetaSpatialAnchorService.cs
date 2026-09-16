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
        var ovrAnchors = new List<OVRSpatialAnchor>();
        foreach (var h in anchors)
        {
            if (h is MetaAnchorHandle meta) ovrAnchors.Add(meta.Anchor);
        }
        var result = await OVRSpatialAnchor.SaveAnchorsAsync(ovrAnchors);
        return result.Success;
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
