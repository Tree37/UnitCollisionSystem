using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

public class UnitCollisionSystemFixedGridStateWithMap : SystemBase
{
    private NativeArray<ushort> grid;
    private NativeHashMap<int , int> activeCells;
    private NativeQueue<CopyCellData> changedCells;

    private const float CELL_SIZE = 1f;
    private const int CELLS_ACROSS = 6000;
    private const int CELL_CAPACITY = 4;
    private const ushort VOID_CELL_VALUE = 0;

    private EntityQuery generalQuery;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
       /* grid = new NativeArray<ushort>( 36000000 , Allocator.Persistent );
        activeCells = new NativeHashMap<int , int>( 20000 , Allocator.Persistent );
        changedCells = new NativeQueue<CopyCellData>( Allocator.Persistent );
        generalQuery = GetEntityQuery( typeof( Translation ) );*/

        /*InitializeJob initJob = new InitializeJob
        {
            CELL_SIZE = CELL_SIZE ,
            CELLS_ACROSS = CELLS_ACROSS ,
            CELL_CAPACITY = CELL_CAPACITY ,
            VOID_CELL_VALUE = VOID_CELL_VALUE ,
            translationHandle = GetComponentTypeHandle<Translation>() ,
            cellHandle = GetComponentTypeHandle<CollisionCell>() ,
            grid = grid,
            activeCells = activeCells
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
            changedCells = changedCells.AsParallelWriter() ,
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
            activeCellsMap = activeCells ,
            changedCells = changedCells
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
        Dependency = copyArray.Dispose( writeBarrier );*/
    }
    protected override void OnDestroy()
    {
        /*grid.Dispose();
        activeCells.Dispose();
        changedCells.Dispose();*/
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
        public NativeHashMap<int , int> activeCells;
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

                if ( !activeCells.ContainsKey( cell ) )
                {
                    activeCells.Add( cell , gridIndex );
                }

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

        public NativeQueue<CopyCellData>.ParallelWriter changedCells;
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

                    changedCells.Enqueue( data );
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
        public NativeHashMap<int , int> activeCellsMap;
        public NativeQueue<CopyCellData> changedCells;

        public void Execute()
        {
            while ( changedCells.TryDequeue( out CopyCellData data ) )
            {
                int gridIndex = data.oldCell * CELL_CAPACITY;
                int unitID = data.unitID;
                int count = 0;
                int unitsInCell = 0;

                while ( count < 4 )
                {
                    if ( grid[ gridIndex + count ] != VOID_CELL_VALUE )
                    {
                        unitsInCell++;
                    }

                    if ( grid[ gridIndex + count ] == unitID )
                    {
                        grid[ gridIndex + count ] = VOID_CELL_VALUE;
                        break;
                    }

                    count++;
                }

                if ( unitsInCell <= 0 )
                {
                    activeCellsMap.Remove( data.oldCell );
                }

                count = 0;
                gridIndex = data.newCell * CELL_CAPACITY;

                if ( !activeCellsMap.ContainsKey( data.newCell ) )
                {
                    activeCellsMap.Add( data.newCell , gridIndex );
                }

                while ( count < 4 )
                {
                    if ( grid[ gridIndex + count ] == VOID_CELL_VALUE )
                    {
                        grid[ gridIndex + count ] = ( ushort ) unitID;
                        break;
                    }

                    count++;
                }
            }

            changedCells.Clear();
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
    [BurstCompile]
    private unsafe struct ResolveCollisionsJob2 : IJobParallelFor
    {
        [ReadOnly] public float CELL_SIZE;
        [ReadOnly] public int CELLS_ACROSS;
        [ReadOnly] public float RADIUS;
        [ReadOnly] public int CELL_CAPACITY;

        [ReadOnly] public NativeArray<ushort> grid;
        [ReadOnly] public NativeArray<int> activeCells;
        [NativeDisableParallelForRestriction] public NativeArray<CopyUnitData> copyUnitData;

        public void Execute( int i )
        {
            int currentCell = activeCells[ i ];

            for ( int cellSlot = 0; cellSlot < CELL_CAPACITY; cellSlot++ )
            {
                int unitID = grid[ currentCell + cellSlot ];

                float px = copyUnitData[ unitID ].position.x;
                float py = copyUnitData[ unitID ].position.z;
                float vx = copyUnitData[ unitID ].velocity.x;
                float vy = copyUnitData[ unitID ].velocity.z;
                float m = copyUnitData[ unitID ].mass;

                float adjustmentPX = px;
                float adjustmentPY = py;
                float adjustmentVX = vx;
                float adjustmentVY = vy;

                
            }

            // for each unit in current cell,
            // do collision with each unit in each neightbour cell
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
