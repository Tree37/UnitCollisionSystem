using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

public class UnitCollisionSystemFixedGridState : SystemBase
{
    private NativeArray<ushort> grid;
    private NativeQueue<CopyCellData> copyCellData;

    private const float CELL_SIZE = 1f;
    private const int CELLS_ACROSS = 6000;
    private const int CELL_CAPACITY = 4;
    private const ushort VOID_CELL_VALUE = 0;

    private EntityQuery generalQuery;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        /*grid = new NativeArray<ushort>( 36000000 , Allocator.Persistent );
        copyCellData = new NativeQueue<CopyCellData>( Allocator.Persistent );
        generalQuery = GetEntityQuery( typeof( Translation ) );

        InitializeJob initJob = new InitializeJob
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            CELL_CAPACITY = CELL_CAPACITY ,
            VOID_CELL_VALUE = VOID_CELL_VALUE ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            cellHandle = GetComponentTypeHandle<CollisionCell>() ,
            grid = grid
        };

        Dependency = initJob.Schedule( generalQuery , Dependency );*/
    }
    protected override void OnUpdate()
    {
        /*generalQuery = GetEntityQuery( typeof( Translation ) );
        NativeArray<CopyUnitData> copyArray = new NativeArray<CopyUnitData>( generalQuery.CalculateEntityCount() , Allocator.TempJob );

        UpdateCellsJob updateCellsJob = new UpdateCellsJob
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            unitsToUpdate = copyCellData.AsParallelWriter() ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            cellHandle = GetComponentTypeHandle<CollisionCell>()
        };

        JobHandle updateCellsBarrier = updateCellsJob.ScheduleParallel( generalQuery , 1 , Dependency );
        updateCellsBarrier.Complete();

        BroadPhaseJob broadPhaseJob = new BroadPhaseJob()
        {
            CELL_CAPACITY = CELL_CAPACITY ,
            VOID_CELL_VALUE = VOID_CELL_VALUE ,
            grid = grid ,
            cellData = copyCellData
        };
        CopyJob copyJob = new CopyJob
        {
            translationHandle = GetComponentTypeHandle<Translation>() ,
            velocityHandle = GetComponentTypeHandle<Velocity>() ,
            massHandle = GetComponentTypeHandle<Mass>() ,
            copyArray = copyArray
        };

        JobHandle broadphaseBarrier = JobHandle.CombineDependencies(
            broadPhaseJob.Schedule( Dependency ) ,
            copyJob.ScheduleParallel( generalQuery , 1 , Dependency ) );
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
        copyCellData.Clear();
        Dependency = copyArray.Dispose( writeBarrier );*/
    }
    protected override void OnDestroy()
    {
        /*grid.Dispose();
        copyCellData.Dispose();*/
        base.OnDestroy();
    }

    private struct InitializeJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;

        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;
        public ComponentTypeHandle<CollisionCell> cellHandle;
        public NativeArray<ushort> grid;

        [BurstCompile]
        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<CollisionCell> batchCell = batchInChunk.GetNativeArray( cellHandle );

            // Unroll and vectorize this loop
            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                // Vectorize
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                // Can remove the divdes if we decide to go with cell size of 1
                int cell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                batchCell[ i ] = new CollisionCell { Value = cell };

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
    private struct UpdateCellsJob : IJobEntityBatchWithIndex
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;

        public NativeQueue<CopyCellData>.ParallelWriter unitsToUpdate;
        public ComponentTypeHandle<CollisionCell> cellHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationHandle;

        public void Execute( ArchetypeChunk batchInChunk , int batchIndex , int indexOfFirstEntityInQuery )
        {
            NativeArray<Translation> batchTranslation = batchInChunk.GetNativeArray( translationHandle );
            NativeArray<CollisionCell> batchCell = batchInChunk.GetNativeArray( cellHandle );

            for ( int i = 0; i < batchInChunk.Count; i++ )
            {
                float px = batchTranslation[ i ].Value.x;
                float py = batchTranslation[ i ].Value.z;
                // Can remove the divdes if we decide to go with cell size of 1
                int cell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( py / CELL_SIZE ) * CELLS_ACROSS );
                int oldCell = batchCell[ i ].Value;

                if ( oldCell != cell )
                {
                    CopyCellData data = new CopyCellData
                    {
                        unitID = indexOfFirstEntityInQuery + i ,
                        oldCell = oldCell ,
                        newCell = cell
                    };
                    batchCell[ i ] = new CollisionCell { Value = cell };

                    unitsToUpdate.Enqueue( data );
                }
            }
        }
    }
    [BurstCompile]
    private struct BroadPhaseJob : IJob
    {
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;

        public NativeArray<ushort> grid;
        public NativeQueue<CopyCellData> cellData;

        public void Execute()
        {
            while ( cellData.TryDequeue( out CopyCellData data ) )
            {
                int gridIndex = data.oldCell * CELL_CAPACITY;
                int unitID = data.unitID;
                int count = 0;

                while ( count < CELL_CAPACITY )
                {
                    if ( grid[ gridIndex + count ] == unitID )
                    {
                        grid[ gridIndex + count ] = VOID_CELL_VALUE;
                        break;
                    }

                    count++;
                }

                count = 0;
                gridIndex = data.newCell * CELL_CAPACITY;

                while ( count < CELL_CAPACITY )
                {
                    if ( grid[ gridIndex + count ] == VOID_CELL_VALUE )
                    {
                        grid[ gridIndex + count ] = ( ushort ) unitID;
                        break;
                    }

                    count++;
                }
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
            float pz = copyUnitData[ i ].position.z;
            float vx = copyUnitData[ i ].velocity.x;
            float vz = copyUnitData[ i ].velocity.z;
            float m = copyUnitData[ i ].mass;

            int curCell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( pz / CELL_SIZE ) * CELLS_ACROSS );
            int xR = ( int ) math.round( px );
            int yR = ( int ) math.round( pz );
            int xD = math.select( 1 , -1 , xR < px );
            int yD = math.select( 1 , -1 , yR < pz );

            Cells cells = new Cells()
            {
                cell = curCell ,
                xN = curCell + xD ,
                yN = curCell + yD * CELLS_ACROSS ,
                cN = curCell + xD + yD * CELLS_ACROSS
            };

            var p = ( int* ) &cells;
            var length = 4;

            for ( int j = 0; j < length; j++ )
            {
                int gridIndex = p[ j ] * CELL_CAPACITY;
                int count = 0;
                while ( count < CELL_CAPACITY )
                {
                    int otherUnitIndex = grid[ gridIndex ];
                    float px2 = copyUnitData[ otherUnitIndex ].position.x;
                    float pz2 = copyUnitData[ otherUnitIndex ].position.z;
                    float vx2 = copyUnitData[ otherUnitIndex ].velocity.x;
                    float vz2 = copyUnitData[ otherUnitIndex ].velocity.z;
                    float m2 = copyUnitData[ otherUnitIndex ].mass;

                    float distance = math.sqrt( ( px - px2 ) * ( px - px2 ) + ( pz - pz2 ) * ( pz - pz2 ) );
                    int overlaps = math.select( 0 , 1 , distance < RADIUS );

                    float overlap = 0.5f * ( distance - RADIUS );

                    float ax = overlaps * ( overlap * ( px - px2 ) ) / ( distance + 0.01f );
                    float az = overlaps * ( overlap * ( pz - pz2 ) ) / ( distance + 0.01f );

                    float ax1 = px - ax;
                    float az1 = pz - az;
                    float ax2 = px2 + ax;
                    float az2 = pz2 + az;

                    copyUnitData[ i ] = new CopyUnitData
                    {
                        position = new float3( ax1 , copyUnitData[ i ].position.y , az1 ) ,
                        velocity = new float3( pz , copyUnitData[ i ].velocity.y , pz ) ,
                        mass = copyUnitData[ i ].mass
                    };

                    copyUnitData[ otherUnitIndex ] = new CopyUnitData
                    {
                        position = new float3( ax2 , copyUnitData[ i ].position.y , az2 ) ,
                        velocity = new float3( vx2 , copyUnitData[ i ].velocity.y , vz2 ) ,
                        mass = copyUnitData[ i ].mass
                    };

                    gridIndex++;
                    count++;
                }
            }
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


    private struct CopyCellData
    {
        public int unitID;
        public int oldCell;
        public int newCell;
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