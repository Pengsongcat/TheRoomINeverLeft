using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MetaSpatialAnchorService : SpatialAnchorService
{
    public override Task<IAnchorHandle> CreateAnchorAsync(Vector3 position, Quaternion rotation)
        => throw new NotImplementedException();

    public override Task<bool> SaveAnchorsAsync(IEnumerable<IAnchorHandle> anchors)
        => throw new NotImplementedException();

    public override Task<IReadOnlyList<IAnchorHandle>> LoadAnchorsAsync(IEnumerable<Guid> ids)
        => throw new NotImplementedException();

    public override Task<bool> EraseAnchorsAsync(IEnumerable<IAnchorHandle> anchors)
        => throw new NotImplementedException();
}
