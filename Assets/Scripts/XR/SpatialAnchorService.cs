using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public abstract class SpatialAnchorService : MonoBehaviour, ISpatialAnchorService
{
    public abstract Task<IAnchorHandle> CreateAnchorAsync(Vector3 position, Quaternion rotation);
    public abstract Task<bool> SaveAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
    public abstract Task<IReadOnlyList<IAnchorHandle>> LoadAnchorsAsync(IEnumerable<Guid> ids);
    public abstract Task<bool> EraseAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
    public abstract Task<bool> EraseAnchorsByIdAsync(IEnumerable<Guid> ids);
    public abstract void ReleaseAnchors(IEnumerable<IAnchorHandle> anchors);
}
