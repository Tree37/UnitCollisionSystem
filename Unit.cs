using System;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Unity.Mathematics;

public struct UnitTag : IComponentData
{

}

public struct UnitMoving : IComponentData
{
    public int Value;
}
public struct MoveSpeed : IComponentData
{
    public float Walk;
    public float Run;
}
public struct TargetPosition : IComponentData
{
    public float3 Value;
}
public struct Velocity : IComponentData
{
    public float3 Value;
}
public struct Mass : IComponentData
{
    public float Value;
}
public struct CollisionCell : IComponentData
{
    public int Value;
}
public struct FrameIndex : ISharedComponentData
{
    public int Value;
}
