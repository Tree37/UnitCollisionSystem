using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

public class UnitCollisionSystem3 : SystemBase
{
    private const int CELL_SIZE = 3;
    private const int CELLS_ACROSS = 3000;
    private const float CELL_OVERLAP_DIST = 0.5f;
    private const int CELL_CAPACITY = 20;
    private const int NUM_COLLISION_CELLS = 4000;

    private NativeArray<int> collisionCells;
    // kckckckckckckckckckckckckckckckckckc
    private NativeHashMap<int , int> collisionMap;
    // vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
    private EntityQuery query;

    private NativeMultiHashMap<int , int> collisionHashMap;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        collisionCells = new NativeArray<int>( NUM_COLLISION_CELLS * CELL_CAPACITY , Allocator.Persistent );
        collisionMap = new NativeHashMap<int , int>( NUM_COLLISION_CELLS , Allocator.Persistent );
        collisionHashMap = new NativeMultiHashMap<int , int>( 60000 , Allocator.Persistent );
    }
    protected override void OnUpdate()
    {
        query = GetEntityQuery( typeof( Translation ) );
        //JobHandle handle;

        /*ClearArraysJob clearArraysJob = new ClearArraysJob();
        clearArraysJob.collisionCells = collisionCells;
        clearArraysJob.collisionMap = collisionMap;

        handle = clearArraysJob.Schedule( Dependency );*/

        /*BroadPhaseJob job = new BroadPhaseJob();
        job.CELL_SIZE = CELL_SIZE;
        job.CELLS_ACROSS = CELLS_ACROSS;
        job.CELL_OVERLAP_DIST = CELL_OVERLAP_DIST;
        job.CELL_CAPACITY = CELL_CAPACITY;
        job.translationHandle = GetComponentTypeHandle<Translation>();
        job.collisionCells = collisionCells;*/

        /*BroadPhaseWithMapJob job = new BroadPhaseWithMapJob();
        job.CELL_SIZE = CELL_SIZE;
        job.CELLS_ACROSS = CELLS_ACROSS;
        job.CELL_OVERLAP_DIST = CELL_OVERLAP_DIST;
        job.CELL_CAPACITY = CELL_CAPACITY;
        job.translationHandle = GetComponentTypeHandle<Translation>();
        job.collisionCells = collisionCells;
        job.collisionMap = collisionMap;

        handle = job.Schedule( query , handle );*/

        /*ClearMapJob clearJob = new ClearMapJob();
        clearJob.map = collisionHashMap;

        handle = clearJob.Schedule( Dependency );

        BroadPhaseWithHashMapJob hashMapJob = new BroadPhaseWithHashMapJob();
        hashMapJob.CELLS_ACROSS = CELLS_ACROSS;
        hashMapJob.CELL_SIZE = CELL_SIZE;
        hashMapJob.translationHandle = GetComponentTypeHandle<Translation>();
        hashMapJob.collisionMap = collisionHashMap.AsParallelWriter();

        handle = hashMapJob.ScheduleParallel( query , 1 , handle );
        Dependency = handle;
        handle.Complete();*/
    }
    protected override void OnDestroy()
    {
        collisionCells.Dispose();
        collisionMap.Dispose();
        collisionHashMap.Dispose();
        base.OnDestroy();
    }

    private struct BroadPhaseJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float CELL_OVERLAP_DIST;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeArray<int> collisionCells;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                int currentCellIndex = 0;
                int firstEmptyCellIndexFound = 0;
                bool found = false;
                int loopCount = 0;
                int maxIterations = collisionCells.Length / CELL_CAPACITY;

                while ( loopCount < maxIterations )
                {
                    if ( collisionCells[ currentCellIndex ] == -1 )
                    {
                        firstEmptyCellIndexFound = currentCellIndex;
                    }
                    else if ( collisionCells[ currentCellIndex ] == cellHash )
                    {
                        int count = collisionCells[ currentCellIndex + 1 ];
                        collisionCells[ currentCellIndex + 1 ] = count + 1;
                        collisionCells[ currentCellIndex + 1 + count ] = indexOfFirstEntityInQuery + 1;
                        found = true;
                        break;
                    }

                    currentCellIndex += CELL_CAPACITY;
                    loopCount++;
                }

                if ( !found )
                {
                    int count = collisionCells[ firstEmptyCellIndexFound + 1 ];
                    collisionCells[ firstEmptyCellIndexFound + 1 ] = count + 1;
                    collisionCells[ firstEmptyCellIndexFound ] = cellHash;
                    collisionCells[ firstEmptyCellIndexFound + 1 + count ] = indexOfFirstEntityInQuery + 1;
                }
            }
        }
    }

    private struct BroadPhaseWithMapJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float CELL_OVERLAP_DIST;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeHashMap<int , int> collisionMap;
        public NativeArray<int> collisionCells;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            int nextFreeCellIndex = 0;

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                if ( collisionMap.TryGetValue( cellHash , out int item ) )
                {
                    int collisionCellIndex = item;
                    while ( collisionCells[ collisionCellIndex ] != -1 )
                    {
                        collisionCellIndex++;
                    }

                    collisionCells[ collisionCellIndex ] = indexOfFirstEntityInQuery + i;
                }
                else
                {
                    collisionCells[ nextFreeCellIndex ] = indexOfFirstEntityInQuery + i;
                    collisionMap.Add( cellHash , nextFreeCellIndex );

                    nextFreeCellIndex += CELL_CAPACITY;
                }
            }
        }
    }

    private struct BroadPhaseWithHashMapJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public int CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeMultiHashMap<int , int>.ParallelWriter collisionMap;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                collisionMap.Add( cellHash , indexOfFirstEntityInQuery + i );
            }
        }
    }

    [BurstCompile]
    private unsafe struct ClearArraysJob : IJob
    {
        public NativeArray<int> collisionCells;
        public NativeHashMap<int , int> collisionMap;

        public void Execute()
        {
            int intValue = -1;
            void* pointer = ( void* ) &intValue;
            UnsafeUtility.MemCpyReplicate( collisionCells.GetUnsafePtr() , pointer , sizeof( int ) , collisionCells.Length );

            collisionMap.Clear();
        }
    }

    [BurstCompile]
    private struct ClearMapJob : IJob
    {
        public NativeMultiHashMap<int , int> map;

        public void Execute()
        {
            map.Clear();
        }
    }
}

// TRY THIS AFTER
// Hash units to separate arrays in parrallel
// Store number of cells
// Append arrays
// Single thread sort new appended array
// meaning in job we have appended array and new empty array
// so basically start at cell 1 and do a linear search of all cells for each cell and put all unit indices into corresponding cells in new array
// Then in parallel do collision in parallel on new array
