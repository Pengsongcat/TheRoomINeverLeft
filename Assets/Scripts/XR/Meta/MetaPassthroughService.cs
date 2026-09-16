using System;
using UnityEngine;

public class MetaPassthroughService : PassthroughService
{
    public override bool IsActive => false;
    public override float Opacity { get; set; }
    public override void Enable()  => throw new NotImplementedException();
    public override void Disable() => throw new NotImplementedException();
}
