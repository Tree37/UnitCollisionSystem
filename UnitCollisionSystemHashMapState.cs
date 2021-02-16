using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

public class UnitCollisionSystemFixedGridState2 : SystemBase
{
    private NativeArray<ushort> grid;
    private NativeArray<BitField32> occupancyGrid;
    private NativeQueue<CopyCellData> copyCellData;

    private const float CELL_SIZE = 1f;
    private const int CELLS_ACROSS = 6000;
    private const int CELL_CAPACITY = 4;
    private const ushort VOID_CELL_VALUE = 0;

    private EntityQuery generalQuery;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        /*grid = new NativeArray<ushort>( 36000000 , Allocator.Persistent , NativeArrayOptions.ClearMemory );
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
            grid = grid ,
        };

        Dependency = initJob.Schedule( generalQuery , Dependency );*/
    }
    protected override void OnUpdate()
    {

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
                batchCell[ i ] = new CollisionCell { Value = cell };

                if ( oldCell != cell )
                {
                    CopyCellData data = new CopyCellData
                    {
                        unitID = indexOfFirstEntityInQuery + i ,
                        oldCell = oldCell ,
                        newCell = cell
                    };

                    unitsToUpdate.Enqueue( data );
                }
            }
        }
    }
    [BurstCompile]
    private unsafe struct ResolveCollisionsJob3 : IJobParallelFor
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

            float adjustmentPX = px;
            float adjustmentPY = pz;
            float adjustmentVX = vx;
            float adjustmentVY = vz;

            int curCell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( pz / CELL_SIZE ) * CELLS_ACROSS );
            int xR = ( int ) math.round( px );
            int zR = ( int ) math.round( pz );
            int xD = math.select( 1 , -1 , xR < px );
            int zD = math.select( 1 , -1 , zR < pz );

            Cells cells = new Cells();
            cells.cell = curCell;
            cells.xN = cells.cell + xD;
            cells.zN = cells.cell + zD * CELLS_ACROSS;
            cells.cN = curCell + xD + zD * CELLS_ACROSS;

            var p = ( int* ) &cells;
            var length = 4;
            //UnsafeUtility.SizeOf<Cells>() / UnsafeUtility.SizeOf<int>();

            FixedList4096<VectorizedCopyData> vD = new FixedList4096<VectorizedCopyData>();
            for ( int j = 0; j < length; j++ )
            {
                int cell = p[ j ];
                int u1 = grid[ cell ];
                int u2 = grid[ cell + 1 ];
                int u3 = grid[ cell + 2 ];
                int u4 = grid[ cell + 3 ];

                vD.Add( new VectorizedCopyData
                {
                    xPos = new float4(
                        copyUnitData[ u1 ].position.x ,
                        copyUnitData[ u2 ].position.x ,
                        copyUnitData[ u3 ].position.x ,
                        copyUnitData[ u4 ].position.x ) ,
                    yPos = new float4(
                        copyUnitData[ u1 ].position.y ,
                        copyUnitData[ u2 ].position.y ,
                        copyUnitData[ u3 ].position.y ,
                        copyUnitData[ u4 ].position.y ) ,
                    zPos = new float4(
                        copyUnitData[ u1 ].position.z ,
                        copyUnitData[ u2 ].position.z ,
                        copyUnitData[ u3 ].position.z ,
                        copyUnitData[ u4 ].position.z ) ,
                    xVel = new float4(
                        copyUnitData[ u1 ].velocity.x ,
                        copyUnitData[ u2 ].velocity.x ,
                        copyUnitData[ u3 ].velocity.x ,
                        copyUnitData[ u4 ].velocity.x ) ,
                    yVel = new float4(
                        copyUnitData[ u1 ].velocity.y ,
                        copyUnitData[ u2 ].velocity.y ,
                        copyUnitData[ u3 ].velocity.y ,
                        copyUnitData[ u4 ].velocity.y ) ,
                    zVel = new float4(
                        copyUnitData[ u1 ].velocity.z ,
                        copyUnitData[ u2 ].velocity.z ,
                        copyUnitData[ u3 ].velocity.z ,
                        copyUnitData[ u4 ].velocity.z ) ,
                    mass = new float4(
                        copyUnitData[ u1 ].mass ,
                        copyUnitData[ u2 ].mass ,
                        copyUnitData[ u3 ].mass ,
                        copyUnitData[ u4 ].mass ) ,
                } );

                float4 px4 = new float4( px , px , px , px );
                float4 pz4 = new float4( pz , pz , pz , pz );
                float4 ax4 = new float4( 0 , 0 , 0 , 0 );
                float4 az4 = new float4( 0 , 0 , 0 , 0 );
                float ax = px;
                float az = pz;

                float4 distance = math.sqrt( ( px4 - vD[ 0 ].xPos ) * ( px4 - vD[ 0 ].xPos ) + ( pz4 - vD[ 0 ].zPos ) * ( pz4 - vD[ 0 ].zPos ) );
                int4 overlaps = math.select( ( int4 ) 0 , ( int4 ) 1 , distance < RADIUS );
                float4 overlap = 0.5f * ( distance - RADIUS );
                ax4 -= overlaps * ( overlap * ( px4 - vD[ 0 ].xPos ) ) / ( distance + 0.01f );
                az4 -= overlaps * ( overlap * ( pz4 - vD[ 0 ].zPos ) ) / ( distance + 0.01f );

                ax -= ( ax4.x + ax4.y + ax4.z + ax4.w );
                az -= ( az4.x + az4.y + az4.z + az4.w );

                copyUnitData[ i ] = new CopyUnitData
                {
                    position = new float3( ax , copyUnitData[ i ].position.y , az ) ,
                    velocity = new float3( copyUnitData[ i ].velocity.x , copyUnitData[ i ].velocity.y , copyUnitData[ i ].velocity.z ) ,
                    mass = copyUnitData[ i ].mass
                };

                vD.Clear();
            }
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
        [NativeDisableParallelForRestriction] public NativeArray<CopyUnitData> copyUnitData;

        public void Execute( int i )
        {
            float px = copyUnitData[ i ].position.x;
            float pz = copyUnitData[ i ].position.z;
            float vx = copyUnitData[ i ].velocity.x;
            float vz = copyUnitData[ i ].velocity.z;
            float m = copyUnitData[ i ].mass;

            float adjustmentPX = px;
            float adjustmentPZ = pz;
            float adjustmentVX = vx;
            float adjustmentVZ = vz;

            int curCell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( pz / CELL_SIZE ) * CELLS_ACROSS );
            int gridIndex = ( int ) ( curCell + CELLS_ACROSS - 1 );
            int row2 = ( int ) ( CELLS_ACROSS );
            int row3 = ( int ) ( CELLS_ACROSS * 2 );
            //TOP LEFT
            int i2 = grid[ gridIndex ];
            float distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            int overlaps = math.select( 0 , 1 , distance < RADIUS );
            float overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // TOP CENTER
            i2 = grid[ gridIndex + 1 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // TOP RIGHT
            i2 = grid[ gridIndex + 2 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // MIDDLE LEFT
            i2 = grid[ gridIndex - row2 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // MIDDLE RIGHT
            i2 = grid[ gridIndex - row2 + 2 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // BOTTOM LEFT
            i2 = grid[ gridIndex - row3 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // BOTTOM CENTER
            i2 = grid[ gridIndex - row3 + 1 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );
            // BOTTOM RIGHT
            i2 = grid[ gridIndex - row3 + 2 ];
            distance = math.sqrt( ( px - copyUnitData[ i2 ].position.x ) * ( px - copyUnitData[ i2 ].position.x ) + ( pz - copyUnitData[ i2 ].position.z ) * ( pz - copyUnitData[ i2 ].position.z ) );
            overlaps = math.select( 0 , 1 , distance < RADIUS );
            overlap = 0.5f * ( distance - RADIUS );
            adjustmentPX -= overlaps * ( overlap * ( px - copyUnitData[ i2 ].position.x ) ) / ( distance + 0.01f );
            adjustmentPZ -= overlaps * ( overlap * ( pz - copyUnitData[ i2 ].position.z ) ) / ( distance + 0.01f );

            copyUnitData[ i ] = new CopyUnitData
            {
                position = new float3( adjustmentPX , copyUnitData[ i ].position.y , adjustmentPZ ) ,
                velocity = new float3( adjustmentVX , copyUnitData[ i ].velocity.y , adjustmentVZ ) ,
                mass = copyUnitData[ i ].mass
            };
        }
    }
    private struct UpdateCellsJob2 : IJobEntityBatchWithIndex
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
                batchCell[ i ] = new CollisionCell { Value = cell };

                if ( oldCell != cell )
                {
                    CopyCellData data = new CopyCellData
                    {
                        unitID = indexOfFirstEntityInQuery + i ,
                        oldCell = oldCell ,
                        newCell = cell
                    };

                    unitsToUpdate.Enqueue( data );
                }
            }
        }
    }
    [BurstCompile]
    private struct BroadPhaseJob2 : IJob
    {
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;

        public NativeArray<ushort> grid;
        public NativeHashSet<int> activeCells;
        public NativeQueue<CopyCellData> cellData;

        public void Execute()
        {
            while ( cellData.TryDequeue( out CopyCellData data ) )
            {
                int gridIndex = data.oldCell * CELL_CAPACITY;
                int unitID = data.unitID;
                int count = 0;
                int unitCount = 0;

                if ( grid[ gridIndex ] == data.unitID )
                {
                    grid[ gridIndex + count ] = VOID_CELL_VALUE;
                }

                gridIndex = data.newCell * CELL_CAPACITY;

                if ( grid[ gridIndex + count ] == VOID_CELL_VALUE )
                {
                    grid[ gridIndex ] = ( ushort ) unitID;
                }
            }
        }
    }
    private struct BuildGridJob2 : IJobEntityBatchWithIndex
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
                grid[ cell * CELL_CAPACITY ] = ( ushort ) ( indexOfFirstEntityInQuery + i );
            }
        }
    }
    [BurstCompile]
    private struct BroadPhaseJob : IJob
    {
        [ReadOnly] public int CELL_CAPACITY;
        [ReadOnly] public ushort VOID_CELL_VALUE;

        public NativeArray<ushort> grid;
        public NativeHashSet<int> activeCells;
        public NativeQueue<CopyCellData> cellData;

        public void Execute()
        {
            while ( cellData.TryDequeue( out CopyCellData data ) )
            {
                int gridIndex = data.oldCell * CELL_CAPACITY;
                int unitID = data.unitID;
                int count = 0;
                int unitCount = 0;

                while ( count < 4 )
                {
                    if ( grid[ gridIndex + count ] != VOID_CELL_VALUE )
                    {
                        unitCount++;
                    }

                    if ( grid[ gridIndex + count ] == unitID )
                    {
                        grid[ gridIndex + count ] = VOID_CELL_VALUE;
                        //break;
                    }

                    count++;
                }

                if ( unitCount <= 1 )
                {
                    activeCells.Remove( data.oldCell );
                }

                count = 0;
                gridIndex = data.newCell * CELL_CAPACITY;

                while ( count < 4 )
                {
                    if ( grid[ gridIndex + count ] == VOID_CELL_VALUE )
                    {
                        grid[ gridIndex + count ] = ( ushort ) unitID;
                        activeCells.Add( data.newCell );
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

            float adjustmentPX = px;
            float adjustmentPZ = pz;
            float adjustmentVX = vx;
            float adjustmentVZ = vz;

            int curCell = ( int ) ( math.floor( px / CELL_SIZE ) + math.floor( pz / CELL_SIZE ) * CELLS_ACROSS );
            int xR = ( int ) math.round( px );
            int yR = ( int ) math.round( pz );
            int xD = math.select( 1 , -1 , xR < px );
            int zD = math.select( 1 , -1 , yR < pz );

            Cells cells = new Cells();
            cells.cell = curCell;
            cells.xN = cells.cell + xD;
            cells.zN = cells.cell + zD * CELLS_ACROSS;
            cells.cN = curCell + xD + zD * CELLS_ACROSS;

            var p = ( int* ) &cells;
            var length = UnsafeUtility.SizeOf<Cells>() / UnsafeUtility.SizeOf<int>();

            for ( int j = 0; j < length; j++ )
            {
                int gridIndex = p[ j ] * CELL_CAPACITY;
                int count = 0;
                while ( count < 4 )
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

                    adjustmentPX -= overlaps * ( overlap * ( px - px2 ) ) / ( distance + 0.01f );
                    adjustmentPZ -= overlaps * ( overlap * ( pz - pz2 ) ) / ( distance + 0.01f );

                    adjustmentVX = adjustmentVX; //+ vx2 * m2;
                    adjustmentVZ = adjustmentVZ; // + vx2 * m2;

                    gridIndex++;
                    count++;
                }
            }

            copyUnitData[ i ] = new CopyUnitData
            {
                position = new float3( adjustmentPX , copyUnitData[ i ].position.y , adjustmentPZ ) ,
                velocity = new float3( adjustmentVX , copyUnitData[ i ].velocity.y , adjustmentVZ ) ,
                mass = copyUnitData[ i ].mass
            };
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
        public int zN;
        public int cN;
    }
    private struct VectorizedCopyData
    {
        public float4 xPos;
        public float4 yPos;
        public float4 zPos;
        public float4 xVel;
        public float4 yVel;
        public float4 zVel;
        public float4 mass;
    }
}
