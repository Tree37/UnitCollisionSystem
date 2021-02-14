using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

public class UnitCollisionSystemFixedGridStateless : SystemBase
{
    private NativeArray<ushort> grid;
    private NativeQueue<int> queue;

    private const float CELL_SIZE = 1f;
    private const int CELLS_ACROSS = 6000;
    private const int CELL_CAPACITY = 4;
    private const ushort VOID_CELL_VALUE = 0;

    private EntityQuery generalQuery;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        grid = new NativeArray<ushort>( 36000000 , Allocator.Persistent );
        queue = new NativeQueue<int>( Allocator.Persistent );
    }
    protected override void OnUpdate()
    {
        /*generalQuery = GetEntityQuery( typeof( Translation ) );
        NativeArray<CopyUnitData> copyArray = new NativeArray<CopyUnitData>( generalQuery.CalculateEntityCount() , Allocator.TempJob );

        BroadPhaseJob job = new BroadPhaseJob()
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            CELL_CAPACITY = CELL_CAPACITY ,
            VOID_CELL_VALUE = VOID_CELL_VALUE ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            grid = grid
        };
        CopyJob copyJob = new CopyJob
        {
            translationHandle = GetComponentTypeHandle<Translation>() ,
            velocityHandle = GetComponentTypeHandle<Velocity>() ,
            massHandle = GetComponentTypeHandle<Mass>() ,
            copyArray = copyArray
        };
        RecordActiveCellsJob recordJob = new RecordActiveCellsJob
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            CELL_CAPACITY = CELL_CAPACITY ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            queue = queue.AsParallelWriter() ,
        };

        JobHandle broadphaseBarrier = JobHandle.CombineDependencies( 
            job.Schedule( generalQuery , Dependency ) , 
            copyJob.ScheduleParallel( generalQuery , 1 , Dependency ) , 
            recordJob.ScheduleParallel( generalQuery , 1 , Dependency ) );
        broadphaseBarrier.Complete();

        ResolveCollisionsJob resolveCollisionsJob = new ResolveCollisionsJob()
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            CELL_CAPACITY = CELL_CAPACITY ,
            RADIUS = 0.5f ,
            grid = grid ,
            copyUnitData = copyArray
        };

        JobHandle narrowphaseBarrier = resolveCollisionsJob.Schedule( copyArray.Length , 80 , broadphaseBarrier );
        narrowphaseBarrier.Complete();

        WriteDataJob writeJob = new WriteDataJob
        {
            copyArray = copyArray ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            velocityHandle = GetComponentTypeHandle<Velocity>() ,
            massHandle = GetComponentTypeHandle<Mass>()
        };

        JobHandle writeBarrier = writeJob.ScheduleParallel( generalQuery , 1 , narrowphaseBarrier );
        writeBarrier.Complete();

        NativeArray<int> removeIndices = queue.ToArray( Allocator.TempJob );

        ClearGridJob clearJob = new ClearGridJob
        {
            CELL_CAPACITY = CELL_CAPACITY ,
            VOID_CELL_VALUE = VOID_CELL_VALUE , 
            removeIndices = removeIndices ,
            grid = grid
        };

        JobHandle finalBarrier = clearJob.Schedule( removeIndices.Length , 80 , writeBarrier );
        finalBarrier.Complete();

        queue.Clear();

        Dependency = JobHandle.CombineDependencies( copyArray.Dispose( finalBarrier ) , removeIndices.Dispose( finalBarrier ) );*/
    }
    protected override void OnDestroy()
    {
        grid.Dispose();
        queue.Dispose();
        base.OnDestroy();
    }

    private struct BroadPhaseJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeArray<ushort> grid;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );

            // Unroll and vectorize this loop
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                // Vectorize
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                // Can remove the divdes if we decide to go with cell size of 1
                int cell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                // Single
                int count = 0;
                int gridIndex = cell * CELL_CAPACITY;
                while ( grid[ gridIndex + count ] != VOID_CELL_VALUE && count < CELL_CAPACITY )
                {
                    count++;
                }

                grid[ gridIndex + count ] = ( ushort ) ( indexOfFirstEntityInQuery + i );
            }
        }
    }
    private struct RecordActiveCellsJob : IJobEntityBatch
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public NativeQueue<int>.ParallelWriter queue;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );

            // Unroll and vectorize this loop
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                //Vectorize
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                int cellHash = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );

                queue.Enqueue( ( int ) ( cellHash * CELL_CAPACITY ) );
            }
        }
    }
    private struct CopyJob : IJobEntityBatchWithIndex
    {
        [NativeDisableParallelForRestriction] public NativeArray<CopyUnitData> copyArray;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        [ReadOnly] public ComponentTypeHandle<Velocity> velocityHandle;
        [ReadOnly] public ComponentTypeHandle<Mass> massHandle;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int firstEntityInQueryIndex )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<Velocity> batchVelocity = batchInChunk.GetNativeArray( velocityHandle );
            NativeArray<Mass> batchMass = batchInChunk.GetNativeArray( massHandle );

            // Unroll and vectorize this loop
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                copyArray[ firstEntityInQueryIndex + i ] = new CopyUnitData
                {
                    position = batchTranslation[ i ].Value ,
                    velocity = batchVelocity[ i ].Value ,
                    mass = batchMass[ i ].Value
                };
            }
        }
    }

    [BurstCompile]
    private unsafe struct ResolveCollisionsJob : IJobParallelFor
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float RADIUS;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public NativeArray<ushort> grid;
        [NativeDisableParallelForRestriction] public NativeArray<CopyUnitData> copyUnitData;

        public void Execute( int i )
        {
            float px = copyUnitData[ i ].position.x;
            float py = copyUnitData[ i ].position.z;
            float vx = copyUnitData[ i ].velocity.x;
            float vy = copyUnitData[ i ].velocity.z;
            float m = copyUnitData[ i ].mass;

            float adjustmentPX = px;
            float adjustmentPY = py;
            float adjustmentVX = vx;
            float adjustmentVY = vy;

            int curCell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
            //int xNei = ( int ) ( math.sign( math.round( px ) - px ) );
            //int yNei = ( int ) ( math.sign( math.round( py ) - py ) );
            int xR = ( int ) math.round( px );
            int yR = ( int ) math.round( py );
            int xD = math.select( 1 , -1 , xR < px );
            int yD = math.select( 1 , -1 , yR < py );

            Cells cells = new Cells();
            cells.cell = curCell;
            /*cells.xN = cells.cell + xNei;
            cells.yN = cells.cell + yNei * CELLS_ACROSS;
            cells.cN = curCell + xNei + yNei * CELLS_ACROSS;*/
            cells.xN = cells.cell + xD;
            cells.yN = cells.cell + yD * CELLS_ACROSS;
            cells.cN = curCell + xD + yD * CELLS_ACROSS;

            var p = ( int* ) &cells;
            var length = 4;
            //UnsafeUtility.SizeOf<Cells>() / UnsafeUtility.SizeOf<int>();

            for ( int j = 0; j < length; j++ )
            {
                int gridIndex = p[ j ] * CELL_CAPACITY;
                int count = 0;
                while ( count < 4 )
                {
                    int otherUnitIndex = grid[ gridIndex ];
                    float px2 = copyUnitData[ otherUnitIndex ].position.x;
                    float py2 = copyUnitData[ otherUnitIndex ].position.z;
                    float vx2 = copyUnitData[ otherUnitIndex ].velocity.x;
                    float vy2 = copyUnitData[ otherUnitIndex ].velocity.z;
                    float m2 = copyUnitData[ otherUnitIndex ].mass;

                    float distance = math.sqrt( ( px - px2 ) * ( px - px2 ) + ( py - py2 ) * ( py - py2 ) );
                    int overlaps = math.select( 0 , 1 , distance < RADIUS );

                    float overlap = 0.5f * ( distance - RADIUS );

                    adjustmentPX -= overlaps * ( overlap * ( px - px2 ) ) / ( distance + 0.01f );
                    adjustmentPY -= overlaps * ( overlap * ( py - py2 ) ) / ( distance + 0.01f );

                    adjustmentVX = adjustmentVX; //+ vx2 * m2;
                    adjustmentVY = adjustmentVY; // + vx2 * m2;

                    gridIndex++;
                    count++;
                }
            }

            copyUnitData[ i ] = new CopyUnitData
            {
                position = new float3( adjustmentPX , copyUnitData[ i ].position.y , adjustmentPY ) ,
                velocity = new float3( adjustmentVX , copyUnitData[ i ].velocity.y , adjustmentVY ) ,
                mass = copyUnitData[ i ].mass
            };

            /*copyUnitData[ i ] = new CopyUnitData
            {
                position = copyUnitData[ i ].position ,
                velocity = copyUnitData[ i ].velocity ,
                mass = copyUnitData[ i ].mass
            };*/
        }
    }
    private struct WriteDataJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public NativeArray<CopyUnitData> copyArray;

        [NativeDisableParallelForRestriction] public ComponentTypeHandle<Translation> translationHandle;
        [NativeDisableParallelForRestriction] public ComponentTypeHandle<Velocity> velocityHandle;
        [NativeDisableParallelForRestriction] public ComponentTypeHandle<Mass> massHandle;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<Velocity> batchVelocity = batchInChunk.GetNativeArray( velocityHandle );
            NativeArray<Mass> batchMass = batchInChunk.GetNativeArray( massHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                batchTranslation[ i ] = new Translation { Value = copyArray[ indexOfFirstEntityInQuery + i ].position };
                batchVelocity[ i ] = new Velocity { Value = copyArray[ indexOfFirstEntityInQuery + i ].velocity };
                batchMass[ i ] = new Mass { Value = copyArray[ indexOfFirstEntityInQuery + i ].mass };
            }
        }
    }

    [BurstCompile]
    private struct ClearGridJob : IJobParallelFor
    {
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;
        public NativeArray<int> removeIndices;
        [NativeDisableParallelForRestriction] public NativeArray<ushort> grid;

        public void Execute( int index )
        {
            int startIndex = removeIndices[ index ];
            for ( int i = 0; i < CELL_CAPACITY; i++ )
            {
                grid[ startIndex + i ] = VOID_CELL_VALUE;
            }
        }
    }


    private struct CopyUnitData
    {
        public float3 position;
        public float3 velocity;
        public float mass;
    }
    private struct Cells
    {
        public int cell;
        public int xN;
        public int yN;
        public int cN;
    }

    private struct VectorizedCopyData
    {
        public float3x4 position;
        public float3x4 velocity;
        public float4 mass;
    }
}

// Copy data to vectorized array, array is sorted by entity index
// Parralel write vectors to map
// Parallel resolve
// Parallel write back

// To parallel write to the grid where theres more than 1 slot
// Divide entities by number of available threads with SharedComponentData
// Then, assign one slot of each bucket of 4 to one on the threads
// This way each thread will have their own memory slot to write to