using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface ISpatialAnchorService
{
    string LastSaveError { get; }
    Task<IAnchorHandle> CreateAnchorAsync(Vector3 position, Quaternion rotation);
    Task<bool> SaveAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
    Task<IReadOnlyList<IAnchorHandle>> LoadAnchorsAsync(IEnumerable<Guid> ids);
    Task<bool> EraseAnchorsAsync(IEnumerable<IAnchorHandle> anchors);
    Task<bool> EraseAnchorsByIdAsync(IEnumerable<Guid> ids); // erases from storage without loading
    void ReleaseAnchors(IEnumerable<IAnchorHandle> anchors); // stops tracking locally; storage is untouched
}
