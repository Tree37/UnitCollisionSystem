using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

public class UnitSpawner
{
    private EntityManager entityManager;

    private Mesh unitMesh;
    private Material unitMaterial;

    private const float UNIT_SCALE = 0.5f;
    private const int GROUP_SIZE = 200;
    private const int GROUP_WIDTH = 8;
    private const int GROUP_LENGTH = 25;
    private const float UNIT_SPACING = 0.5f;
    private const float GROUP_SPACING = 1f;

    private int frame = 0;
    private int numFrames = 5;

    public UnitSpawner( Mesh mesh , Material material )
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        unitMesh = mesh;
        unitMaterial = material;

        CreateUnits();
    }

    private void CreateUnits()
    {
        float3 groupPosition = new float3( GROUP_SPACING , 1 , GROUP_SPACING );
        float groupSizeX = 1; //UNIT_SCALE * GROUP_WIDTH;
        float groupSizeY = 1; // UNIT_SCALE * GROUP_LENGTH;

        for ( int i = 0; i < 1; i++ )
        {
            groupPosition = new float3( GROUP_SPACING , 1 , groupPosition.z + groupSizeY * i + GROUP_SPACING );
            for ( int j = 0; j < 2; j++ )
            {
                CreateGroup( groupPosition );
                groupPosition = new float3( groupPosition.x + groupSizeX * j + GROUP_SPACING , 1 , groupPosition.z );
            }
        }
    }

    private void CreateGroup( float3 startPosition )
    {
        for ( int i = 0; i < GROUP_LENGTH; i++ )
        {
            for ( int j = 0; j < GROUP_WIDTH; j++ )
            {
                float x = startPosition.x + j * ( UNIT_SCALE + UNIT_SPACING );
                float y = startPosition.z + i * ( UNIT_SCALE + UNIT_SPACING );
                float3 unitPosition = new float3( x , 1 , y );

                CreateUnit( unitPosition );
            }
        }
    }

    private void CreateUnit( float3 position )
    {
        Entity unit = entityManager.CreateEntity(
            typeof( UnitTag ) ,

            typeof( RenderMesh ) ,
            typeof( RenderBounds ) ,
            typeof( LocalToWorld ) ,
            typeof( Translation ) ,
            typeof( Rotation ) ,
            typeof( Scale ) ,
            typeof( Velocity ) ,
            typeof( Mass ) ,
            typeof( MapKey ) ,
            typeof( TargetPosition ) ,
            typeof( MoveSpeed ) ,
            typeof( CollisionMapKey ) ,
            typeof( TIndex ) );

        entityManager.SetComponentData( unit , new Velocity { Value = float3.zero } );
        entityManager.SetComponentData( unit , new Mass { Value = 1f } );
        entityManager.SetComponentData( unit , new TargetPosition { Value = new float3( UnityEngine.Random.Range(5, 100) , 1 , UnityEngine.Random.Range( 5 , 100 ) ) } );
        entityManager.SetComponentData( unit , new MoveSpeed { Walk = 0.4f , Run = 0.8f } );

        entityManager.SetSharedComponentData( unit , new TIndex { Value = frame } );

        entityManager.SetSharedComponentData( unit , new RenderMesh
        {
            mesh = unitMesh ,
            material = unitMaterial
        } );

        entityManager.SetComponentData( unit , new Translation { Value = position } );
        entityManager.SetComponentData( unit , new Scale { Value = UNIT_SCALE } );

        frame++;

        if ( frame > numFrames )
        {
            frame = 0;
        }
    }

    /*private void CreateUnit2( float3 position )
    {
        BlobAssetReference<Unity.Physics.Collider> spCollider = Unity.Physics.CapsuleCollider.Create( new CapsuleGeometry { } );
        Entity unit = CreateBody( entityManager , unitMesh , position , quaternion.identity , spCollider , float3.zero , float3.zero , 1 , true );

        PhysicsGravityFactor gravity = new PhysicsGravityFactor
        {
            Value = 0 ,
        };
        entityManager.AddComponentData<PhysicsGravityFactor>( unit , gravity );
    }

    public unsafe Entity CreateBody(
    EntityManager entityManager ,
    RenderMesh displayMesh , float3 position , quaternion orientation , BlobAssetReference<Collider> collider ,
    float3 linearVelocity , float3 angularVelocity , float mass , bool isDynamic
    )
    {
        ComponentType[] componentTypes = new ComponentType[ isDynamic ? 9 : 6 ];

        componentTypes[ 0 ] = typeof( RenderMesh );
        componentTypes[ 1 ] = typeof( RenderBounds );
        componentTypes[ 2 ] = typeof( Translation );
        componentTypes[ 3 ] = typeof( Rotation );
        componentTypes[ 4 ] = typeof( LocalToWorld );
        componentTypes[ 5 ] = typeof( PhysicsCollider );
        if ( isDynamic )
        {
            componentTypes[ 6 ] = typeof( PhysicsVelocity );
            componentTypes[ 7 ] = typeof( PhysicsMass );
            componentTypes[ 8 ] = typeof( PhysicsDamping );
        }
        Entity entity = entityManager.CreateEntity( componentTypes );

        entityManager.SetSharedComponentData( entity , displayMesh );
        entityManager.SetComponentData( entity , new RenderBounds { Value = displayMesh.mesh.bounds.ToAABB() } );

        entityManager.SetComponentData( entity , new Translation { Value = position } );
        entityManager.SetComponentData( entity , new Rotation { Value = orientation } );

        entityManager.SetComponentData( entity , new PhysicsCollider { Value = collider } );

        if ( isDynamic )
        {
            Collider* colliderPtr = ( Collider* ) collider.GetUnsafePtr();
            entityManager.SetComponentData( entity , PhysicsMass.CreateDynamic( colliderPtr->MassProperties , mass ) );
            // Calculate the angular velocity in local space from rotation and world angular velocity
            float3 angularVelocityLocal = math.mul( math.inverse( colliderPtr->MassProperties.MassDistribution.Transform.rot ) , angularVelocity );
            entityManager.SetComponentData( entity , new PhysicsVelocity()
            {
                Linear = linearVelocity ,
                Angular = angularVelocityLocal
            } );
            entityManager.SetComponentData( entity , new PhysicsDamping()
            {
                Linear = 0.01f ,
                Angular = 0.05f
            } );
        }

        return entity;
    }*/
}
