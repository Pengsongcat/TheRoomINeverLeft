using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface ISpatialAnchorService
{
    Task<IAnchorHandle> CreateAnchorAsync(Vector3 position, Quaternion rotation);
    Task<bool> SaveAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
    Task<IReadOnlyList<IAnchorHandle>> LoadAnchorsAsync(IEnumerable<Guid> ids);
    Task<bool> EraseAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
}
