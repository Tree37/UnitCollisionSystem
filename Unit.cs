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

public struct CollisionMapKey : IComponentData
{
    public ushort cell;
    public ushort x_Nei;
    public ushort y_Nei;
    public ushort c_Nei;
}

public struct MapKey : IComponentData
{
    public int Value;
}

public struct TIndex : ISharedComponentData
{
    public int Value;
}
