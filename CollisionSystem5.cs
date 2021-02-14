using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

public class CollisionSystem5 : SystemBase
{
    protected override void OnUpdate()
    {
        EntityQuery query = GetEntityQuery( typeof( Translation ) );

       /* HashUnitsJob job = new HashUnitsJob();
        job.CELL_SIZE = 3;
        job.CELLS_ACROSS = 9000;
        job.translationHandle = GetComponentTypeHandle<Translation>();
        job.mapKeyHandle = GetComponentTypeHandle<MapKey>();

        Dependency = job.Schedule( query , Dependency );*/
    }

    private struct HashUnitsJob : IJobEntityBatch
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public ComponentTypeHandle<MapKey> mapKeyHandle;

        [BurstCompile]
        public unsafe void Execute( ArchetypeChunk batchInChunk , int batchIndex )
        {
            // 16b rn -> xp 2b xp 2b xv 0.5b yv 0.5b m 2b state 1b
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<MapKey> batchMapKey = batchInChunk.GetNativeArray( mapKeyHandle );

            // This loop can be unrolled
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                // simd this
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                //var p = ( int* ) &cellHash;
                //int v = (int) Hash( p , 10 );

                batchMapKey[ i ] = new MapKey { Value = 5 }; //new MapKey { Value = v % 4000 };
            }

            // simd this
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
    }

    private struct HashUnitsJob2 : IJobEntityBatch
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        [ReadOnly] public ComponentTypeHandle<Velocity> velocityHandle;
        [ReadOnly] public ComponentTypeHandle<Mass> massHandle;
        public ComponentTypeHandle<MapKey> mapKeyHandle;

        [BurstCompile]
        public unsafe void Execute( ArchetypeChunk batchInChunk , int batchIndex )
        {
            // 16b rn -> xp 2b xp 2b xv 0.5b yv 0.5b m 2b state 1b
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<Velocity> batchVelocity = batchInChunk.GetNativeArray( velocityHandle );
            NativeArray<Mass> batchMass = batchInChunk.GetNativeArray( massHandle );
            NativeArray<MapKey> batchMapKey = batchInChunk.GetNativeArray( mapKeyHandle );

            // This loop can be unrolled
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                // simd this
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                //var p = ( int* ) &cellHash;
                //int v = (int) Hash( p , 10 );

                batchMapKey[ i ] = new MapKey { Value = 5 }; //new MapKey { Value = v % 4000 };
            }

            // simd this
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
    }

    private struct WriteToMapJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public int CELLS_PER_THREAD;

        [ReadOnly] public ComponentTypeHandle<MapKey> mapKeyHandle;

        [BurstCompile]
        public unsafe void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<MapKey> batchMapKey = batchInChunk.GetNativeArray( mapKeyHandle );
            int scd = 5;

            // This loop can be unrolled
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {

            }
        }
    }
}

// Parallel loop over units, hash position to its thread's corresponding bucket, store with entity, ei : separate hash function for each sharedcomponentdatathread
// Parallel write to array of buckets using thread index
// Parallel resolve collisions by looping over units and accessing map
