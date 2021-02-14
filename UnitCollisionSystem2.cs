using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

[AlwaysUpdateSystem]
public class UnitCollisionSystem2 : SystemBase
{
    private const int CELL_SIZE = 3;
    private const int CELLS_ACROSS = 3000;
    private const float CELL_OVERLAP_DIST = 0.5f;
    private const int CELL_CAPACITY = 18;

    private NativeArray<int> collisionCells;
    private NativeHashMap<int , int> collisionMap;
    // kciiiiikciiiiikciiiii

    private EntityQuery query;

    protected override void OnCreate()
    {
        base.OnCreate();
        query = GetEntityQuery( typeof( UnitTag ) , typeof( Translation ) );
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        query = GetEntityQuery( typeof( UnitTag ) );

        collisionCells = new NativeArray<int>( 40000 , Allocator.Persistent );
        collisionMap = new NativeHashMap<int , int>( 10000 , Allocator.Persistent );

        /*for ( int i = 0; i < collisionCells.Length; i+= 20 )
        {
            collisionCells[ i ] = -1;
        }

        BuildCollisionMapJob job = new BuildCollisionMapJob();
        job.CELL_SIZE = CELL_SIZE;
        job.CELLS_ACROSS = CELLS_ACROSS;
        job.CELL_OVERLAP_DIST = CELL_OVERLAP_DIST;
        job.CELL_CAPACITY = CELL_CAPACITY;
        job.translationHandle = GetComponentTypeHandle<Translation>();
        job.collisionCells = collisionCells;
        job.collisionMap = collisionMap;
        job.Run( query );*/
    }
    protected override void OnUpdate()
    {
        /*query = GetEntityQuery( typeof( Translation ) );
        NativeArray<Translation> unitTranslations = query.ToComponentDataArray<Translation>( Allocator.TempJob );
        //Debug.Log( unitTranslations.Length );
        RemoveKeysJob job = new RemoveKeysJob();
        job.CELL_CAPACITY = CELL_CAPACITY;
        job.unitTranslations = unitTranslations;
        job.collisionCells = collisionCells;

        JobHandle handle = job.Schedule( Dependency );
        unitTranslations.Dispose( handle );
        //unitTranslations.Dispose();
        Dependency = handle;*/
    }
    protected override void OnDestroy()
    {
        collisionCells.Dispose();
        collisionMap.Dispose();
        base.OnDestroy();
    }

    private struct BuildCollisionMapJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float CELL_OVERLAP_DIST;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeArray<int> collisionCells;
        public NativeHashMap<int , int> collisionMap;

        //[BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            Debug.Log( "Running" );
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                if ( collisionMap.TryGetValue( cellHash , out int item ) )
                {
                    int cellIndex = item;
                    int count = collisionCells[ cellIndex + 1 ];
                    collisionCells[ cellIndex + count + 1 ] = indexOfFirstEntityInQuery + i;
                    collisionCells[ cellIndex + 1 ] = count + 1;
                }
                else
                {
                    int currentCellIndex = 0;
                    bool searching = true;

                    while ( searching )
                    {
                        if ( collisionCells[ currentCellIndex ] == -1 )
                        {
                            int cellIndex = currentCellIndex;
                            int count = collisionCells[ cellIndex + 1 ];
                            collisionMap.Add( cellHash , cellIndex );
                            collisionCells[ cellIndex ] = cellHash;
                            collisionCells[ cellIndex + count + 1 ] = indexOfFirstEntityInQuery + i;
                            collisionCells[ cellIndex + 1 ] = count + 1;
                            searching = false;
                        }

                        currentCellIndex += CELL_CAPACITY + 2;
                    }
                }
            }
        }
    }

    // Update Map
    // F1 Remove Keys
    // F2 Remove Keys
    // F3 Remove Keys
    // F4 Remove Keys
    // F5 Add Keys
    // F6 Add Keys
    // F7 Add Keys
    // F8 Add Keys
    // F9 Rebuild Map

    [BurstCompile]
    private struct RemoveKeysJob : IJob
    {
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public NativeArray<Translation> unitTranslations;
        public NativeArray<int> collisionCells;

        public void Execute()
        {
            int loop1Length = collisionCells.Length / 20;
            for ( int i = 0; i < loop1Length; i += 20 )
            {
                for ( int j = 2; j < CELL_CAPACITY; j++ )
                {
                    int index = i + j;
                    if ( collisionCells[ index ] > -1 )
                    {
                        int translationID = collisionCells[ index ];
                        float3 translation = unitTranslations[ translationID ].Value;
                        float px = translation.x;
                        float py = translation.z;
                        int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                        if ( collisionCells[ i ] != cellHash )
                        {
                            collisionCells[ index ] = -1;
                            collisionCells[ i + 1 ] = collisionCells[ i + 1 ] - 1;
                        }
                    }
                }
            }
        }
    }

    private struct AddKeysJob : IJobEntityBatchWithIndex
    {
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            
        }
    }
}
