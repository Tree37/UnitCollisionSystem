using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

public class QuadTreeSystem : SystemBase
{
    QuadTree tree;

    protected override void OnCreate()
    {
        base.OnCreate();
    }

    protected override void OnUpdate()
    {

    }
}

struct Point
{
    public int x;
    public int y;
}

struct Rect
{
    public int x;
    public int y;
    public int w;
    public int h;

    public bool Contains( Point point )
    {
        return (
            point.x >= x - w &&
            point.x <= x + w &&
            point.y >= y - h &&
            point.y <= y + h );
    }

    public bool Intersects( Rect range )
    {
        return !(
            range.x - range.w > x + w ||
            range.x + range.w < x - w ||
            range.y - range.h > y + h ||
            range.y + range.h < y - h );
    }
}

// 

class QuadTree
{
    public Rect rect;
    public int capacity;
    public int count;
    public Point[] points;
    public bool divided;
    public QuadTree nW;
    public QuadTree nE;
    public QuadTree sW;
    public QuadTree sE;

    public QuadTree( Rect _rect )
    {
        rect = _rect;
    }

    public bool Insert( Point point )
    {
        if ( !rect.Contains( point ) )
        {
            return false;
        }

        if ( count < capacity )
        {
            points[ count ] = point;
            count++;
            return true;
        }
        else
        {
            if ( !divided )
            {
                Subdivide();
            }

            if ( nW.Insert( point ) )
                return true;
            if ( nE.Insert( point ) )
                return true;
            if ( sW.Insert( point ) )
                return true;
            if ( sE.Insert( point ) )
                return true;

            return false;
        }
    }

    public void Subdivide()
    {
        Rect nw = new Rect();
        nw.x = rect.x + rect.w / 2;
        nw.y = rect.y + rect.h / 2;
        nw.w = rect.w / 2;
        nw.h = rect.h / 2;
        Rect ne = new Rect();
        ne.x = rect.x - rect.w / 2;
        ne.y = rect.y + rect.h / 2;
        ne.w = rect.w / 2;
        ne.h = rect.h / 2;
        Rect sw = new Rect();
        sw.x = rect.x + rect.w / 2;
        sw.y = rect.y - rect.h / 2;
        sw.w = rect.w / 2;
        sw.h = rect.h / 2;
        Rect se = new Rect();
        se.x = rect.x - rect.w / 2;
        se.y = rect.y - rect.h / 2;
        se.w = rect.w / 2;
        se.h = rect.h / 2;

        divided = true;
    }

    public List<Point> Query( Rect range , List<Point> found )
    {
        if ( !rect.Intersects( range ) )
        {
            return found;
        }
        else
        {
            for ( int i = 0; i < count; i++ )
            {
                if ( range.Contains( points[ i ] ) )
                {
                    found.Add( points[ i ] );
                }
            }

            if ( divided )
            {
                found.AddRange( nW.Query( range , found ) );
                found.AddRange( nE.Query( range , found ) );
                found.AddRange( sW.Query( range , found ) );
                found.AddRange( sE.Query( range , found) );
            }

            return found;
        } 
    }
}