using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Rendering;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;


public class GameManager : MonoBehaviour
{
    int[] num;
    int mapLength = 2000;

    public Mesh unitMesh;
    public Material unitMaterial;

    public UnitSpawner unitSpawner;

    // Start is called before the first frame update
    void Start()
    {
        /*num = new int[ 10 ];
        num[ 0 ] = 1;
        num[ 1 ] = 2;
        num[ 2 ] = 3;
        num[ 4 ] = 4;
        num[ 5 ] = 5;
        num[ 6 ] = 6;
        num[ 7 ] = 7;
        num[ 8 ] = 8;
        num[ 9 ] = 9;

        DoHashes( num );*/

        unitSpawner = new UnitSpawner( unitMesh , unitMaterial );
    }

    // Update is called once per frame
    void Update()
    {

    }

    unsafe void DoHashes( int[] num )
    {
        for ( int i = 0; i < num.Length; i++ )
        {
            int v = num[ i ];
            var p = ( int* ) &v;
            Debug.Log( Hash( p , 32 ) % mapLength );
        }
    }

    unsafe uint Hash( int* value, uint length )
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

// vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv