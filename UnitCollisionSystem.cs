using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

public class UnitCollisionSystem : SystemBase
{
    private EntityQuery query;
    private NativeArray<int> array;

    protected override void OnCreate()
    {
        base.OnCreate();
        query = GetEntityQuery( typeof( CollisionMapKey ) );
        array = new NativeArray<int>( 4000 , Allocator.Persistent );
    }
    protected override void OnUpdate()
    {
        /*HashUnitsJob job = new HashUnitsJob();
        job.CELL_SIZE = 3;
        job.CELLS_ACROSS = 3000;
        job.COLLISION_RADIUS = 0.5f;
        job.MAP_SIZE = 5000;
        job.translationHandle = GetComponentTypeHandle<Translation>();
        job.mapKeyHandle = GetComponentTypeHandle<CollisionMapKey>();

        Dependency = job.Schedule( query , Dependency );

        TestJob job2 = new TestJob();
        job2.array = array;

        Dependency = job2.Schedule( Dependency );*/
    }
    protected override void OnStopRunning()
    {
        array.Dispose();
        base.OnStopRunning();
    }

    private unsafe struct HashUnitsJob : IJobEntityBatch
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float COLLISION_RADIUS;
        [ReadOnly] public int MAP_SIZE;
        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public ComponentTypeHandle<CollisionMapKey> mapKeyHandle;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<CollisionMapKey> batchmapKey = batchInChunk.GetNativeArray( mapKeyHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                //int x_Nei = ( int ) math.round( px );
                //int y_Nei = ( int ) math.round( py );
                //int x_Col = math.select( 0 , 1 , math.abs( x_Nei - px + COLLISION_RADIUS ) < COLLISION_RADIUS );
                //int y_Col = math.select( 0 , 1 , math.abs( y_Nei - py + COLLISION_RADIUS ) < COLLISION_RADIUS );
                //int c_Col = math.select( 0 , 1 , x_Col * y_Col > 0 );

                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                /*int x_NeiHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                int y_NeiHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                int c_Hash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );*/

                ushort h1 = ( ushort ) ( Hash( &cellHash , 32 ) % MAP_SIZE );
                //ushort h2 = ( ushort ) ( Hash( &cellHash , 32 ) % MAP_SIZE );
                //ushort h3 = ( ushort ) ( Hash( &cellHash , 32 ) % MAP_SIZE );
                //ushort h4 = ( ushort ) ( Hash( &cellHash , 32 ) % MAP_SIZE );

                //batchmapKey[ i ] = new CollisionMapKey { cell = h1 , x_Nei = h2 , y_Nei = h3 , c_Nei = h4 };
                batchmapKey[ i ] = new CollisionMapKey { cell = h1 , x_Nei = 1 , y_Nei = 1 , c_Nei = 1 };
            }
        }

        unsafe uint Hash( int* value , uint length )
        {
            uint b = 378551;
            uint a = 63689;
            uint hash = 0;
            uint i = 0;

            for ( i = 0; i < length; ++value, ++i )
            {
                hash = ( uint ) ( hash * a + ( *value ) );
                a = a * b;
            }

            return hash;
        }
    }

    private struct HashUnitsJob2 : IJobEntityBatch
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;

        public void Execute( ArchetypeChunk batchInChunk , int batchIndex )
        {

        }
    }

    [BurstCompile] private struct TestJob : IJob
    {
        [ReadOnly] public NativeArray<int> array;

        public void Execute()
        {
            for ( int i = 0; i < 40000; i++ )
            {
                int numSearches = 2000;
                for ( int j = 0; j < numSearches; j++ )
                {
                    array[ j ] = 2;
                }
            }
        }
    }
}

