﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Npgsql;

namespace NpgsqlTypes
{
    #pragma  warning disable 1591
    /// <summary>
    /// Represents the identifier of the Well Known Binary representation of a geographical feature specified by the OGC.
    /// http://portal.opengeospatial.org/files/?artifact_id=13227 Chapter 6.3.2.7 
    /// </summary>
    enum WkbIdentifier : uint
    {
        Point = 1,
        LineString = 2,
        Polygon = 3,
        MultiPoint = 4,
        MultiLineString = 5,
        MultiPolygon = 6,
        GeometryCollection = 7
    }

    /// <summary>
    /// The modifiers used by postgis to extend the geomtry's binary representation
    /// </summary>
    [Flags]
    enum EwkbModifier : uint
    {
        HasSRID = 0x20000000,
        HasMDim = 0x40000000,
        HasZDim = 0x80000000
    }

    /// <summary>
    /// A structure representing a 2D double precision floating point coordinate;
    /// </summary>
    public struct Coordinate2D
    {
        /// <summary>
        /// X coordinate.
        /// </summary>
        public Double X;

        /// <summary>
        /// Y coordinate.
        /// </summary>
        public Double Y;

        /// <summary>
        /// Generates a new BBpoint with the specified coordinates.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public Coordinate2D(Double x, Double y) { X = x; Y = y;}
    }
              
    /// <summary>
    /// Represents an Postgis feature.
    /// </summary>
    public abstract class IGeometry
    {

        /// <summary>
        /// returns the binary length of the data structure without header.
        /// </summary>
        /// <returns></returns>
        protected abstract int GetLenHelper();
        internal abstract WkbIdentifier Identifier { get;}

        internal int GetLen()
        {
            // header = 
            //      1 byte for the endianness of the structure
            //    + 4 bytes for the type identifier
            //   (+ 4 bytes for the SRID if present)
            return 5 + (SRID == 0 ? 0 : 4) + GetLenHelper(); 
        }

        /// <summary>
        /// The Spatial Reference System Identifier of the geometry (0 if unspecified).
        /// </summary>
        public uint SRID { get; set; }
    }

    /// <summary>
    /// Represents an Postgis 2D Point
    /// </summary>
    public class PostgisPoint : IGeometry, IEquatable<PostgisPoint>
    {

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.Point; }
        }

        Coordinate2D _coord;

        protected override int GetLenHelper()
        {
            return 16;
        }

        public PostgisPoint(Double x, Double y)
        {
            _coord.X = x;
            _coord.Y = y;
        }

        public Double X
        {
            get { return _coord.X ; }
            set { _coord.X = value; }
        }

        public Double Y
        {
            get { return _coord.Y; }
            set { _coord.Y = value; }
        }

        public bool Equals(PostgisPoint other)
        {
            if (object.ReferenceEquals(other, null))
                return false;
            return X == other.X && Y == other.Y;
        }


        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisPoint);
        }

        public static bool operator ==(PostgisPoint x, PostgisPoint y)
        {
            if (Object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisPoint x, PostgisPoint y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ PGUtil.RotateShift(Y.GetHashCode(), sizeof(int) / 2);
        }
    }

    /// <summary>
    /// Represents an Ogc 2D LineString
    /// </summary>
    public class PostgisLineString : IGeometry, IEquatable<PostgisLineString>, IEnumerable<Coordinate2D>
    {
        private Coordinate2D[] _points;

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.LineString; }
        }

        protected override int GetLenHelper()
        {
            return 4 + _points.Length * 16;
        }

        public IEnumerator<Coordinate2D> GetEnumerator()
        {
            return ((IEnumerable<Coordinate2D>)_points).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public Coordinate2D this[Int32 index]
        {
            get { return _points[index]; }
        }

        public PostgisLineString(IEnumerable<Coordinate2D> points)
        {
            _points = points.ToArray();
        }

        public PostgisLineString(Coordinate2D[] points)
        {
            _points = points;
        }

        public Int32 PointCount
        {
            get { return _points.Length; }
        }

        public bool Equals(PostgisLineString other)
        {
            if (object.ReferenceEquals(other , null))
                return false ;

            if (_points.Length != other._points.Length) return false;
            for (int i = 0; i < _points.Length; i++)
            {
                if (!(_points[i].X == other._points[i].X && _points[i].Y == other._points[i].Y))
                    return false;
            }
            return true;
        }


        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisLineString);
        }

        public static bool operator ==(PostgisLineString x, PostgisLineString y)
        {
            if (object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisLineString x, PostgisLineString y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _points.Length; i++)
            {
                ret ^= PGUtil.RotateShift(_points[i].GetHashCode(), ret % sizeof(int));
            }
            return ret;
        }
    }

    /// <summary>
    /// Represents an Postgis 2D Polygon.
    /// </summary>
    public class PostgisPolygon : IGeometry, IEquatable<PostgisPolygon>
    {

        private Coordinate2D[][] _rings;

        protected override int GetLenHelper()
        {
            return 4 + _rings.Length * 4 + TotalPointCount * 16;
        }

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.Polygon; }
        }

        public Coordinate2D this[Int32 ringIndex, Int32 pointIndex]
        {
            get
            {
                return _rings[ringIndex][pointIndex];
            }
        }

        public Coordinate2D[] this[Int32 ringIndex]
        {
            get
            {
                return _rings[ringIndex];
            }
        }

        public PostgisPolygon(Coordinate2D[][] rings)
        {
            _rings = rings;
        }

        public PostgisPolygon(IEnumerable<IEnumerable<Coordinate2D>> rings)
        {
            _rings = rings.Select(x => x.ToArray()).ToArray();
        }

        public bool Equals(PostgisPolygon other)
        {
            if (Object.ReferenceEquals(other, null))
                return false;

            if (_rings.Length != other._rings.Length) 
                return false;
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i].Length != other._rings[i].Length) 
                    return false;
                for (int j = 0; j < _rings[i].Length; j++)
                {
                    if (!(_rings[i][j].X == other._rings[i][j].X && _rings[i][j].Y == other._rings[i][j].Y))
                        return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisPolygon);
        }

        public static bool operator ==(PostgisPolygon x, PostgisPolygon y)
        {
            if (Object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisPolygon x, PostgisPolygon y)
        {
            return !(x == y);
        }

        public Int32 RingCount
        {
            get { return _rings.Length; }
        }

        public Int32 TotalPointCount
        {
            get
            {
                Int32 r = 0;
                for (int i = 0; i < _rings.Length; i++)
                {
                    r += _rings[i].Length;
                }
                return r;
            }
        }

        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _rings.Length; i++)
            {
                for (int j = 0; j < _rings[i].Length; j++)
                {
                    ret ^= PGUtil.RotateShift(_rings[i][j].GetHashCode(), ret % sizeof(int));
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// Represents a Postgis 2D MultiPoint
    /// </summary>
    public class PostgisMultiPoint : IGeometry, IEquatable<PostgisMultiPoint>, IEnumerable<Coordinate2D>
    {
        private Coordinate2D[] _points;

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.MultiPoint; }
        }

        protected override int GetLenHelper()
        {
            return 4 + _points.Length * 21; //each point of a multipoint is a postgispoint, not a building block point.
        }

        public IEnumerator<Coordinate2D> GetEnumerator()
        {
            return ((IEnumerable<Coordinate2D>)_points).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public PostgisMultiPoint (Coordinate2D[] points)
        {
            _points = points;
        }

        public PostgisMultiPoint(IEnumerable<Coordinate2D> points)
        {
            _points = points.ToArray();
        }

        public Coordinate2D this[Int32 indexer]
        {
            get { return _points[indexer]; }
        }

        public bool Equals(PostgisMultiPoint other)
        {
            if (object.ReferenceEquals(other ,null))
                return false ;

            if (_points.Length != other._points.Length) 
                return false;
            for (int i = 0; i < _points.Length; i++)
            {
                if (!(_points[i].X == other._points[i].X && _points[i].Y == other._points[i].Y)) 
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisMultiPoint);
        }

        public static bool operator ==(PostgisMultiPoint x, PostgisMultiPoint y)
        {
            if (object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisMultiPoint x, PostgisMultiPoint y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _points.Length; i++)
            {
                ret ^= PGUtil.RotateShift(_points[i].GetHashCode(), ret % sizeof(int));
            }
            return ret;
        }

        public int PointCount 
        {
            get { return _points.Length; }
        }
    }

    /// <summary>
    /// Represents a Postgis 2D MultiLineString
    /// </summary>
    public class PostgisMultiLineString : IGeometry, IEquatable<PostgisMultiLineString>, IEnumerable<PostgisLineString>
    {
        private PostgisLineString[] _lineStrings;

        internal PostgisMultiLineString(Coordinate2D[][] pointArray)
        {
            _lineStrings = new PostgisLineString[pointArray.Length];
            for (int i = 0; i < pointArray.Length; i++)
            {
                _lineStrings[i] = new PostgisLineString(pointArray[i]);
            }
        }

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.MultiLineString; }
        }

        protected override int GetLenHelper()
        {
            int n = 4;
            for (int i = 0; i < _lineStrings.Length; i++)
            {
                n += _lineStrings[i].GetLen();
            }
            return n;
        }

        public IEnumerator<PostgisLineString> GetEnumerator()
        {
            return ((IEnumerable<PostgisLineString>)_lineStrings).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public PostgisMultiLineString(PostgisLineString[] linestrings)
        {
            _lineStrings = linestrings;
        }

        public PostgisMultiLineString(IEnumerable<PostgisLineString> linestrings)
        {
            _lineStrings = linestrings.ToArray();
        }

        public PostgisLineString this[Int32 index]
        {
            get { return _lineStrings[index]; }
        }

        public PostgisMultiLineString(IEnumerable<IEnumerable<Coordinate2D>> pointList)
        {
            _lineStrings = pointList.Select(x => new PostgisLineString(x)).ToArray();
        }

        public bool Equals(PostgisMultiLineString other)
        {
            if (object.ReferenceEquals(other ,null))
                return false ;

            if (_lineStrings.Length != other._lineStrings.Length) return false;
            for (int i = 0; i < _lineStrings.Length; i++)
            {
                if (_lineStrings[i] != other._lineStrings[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisMultiLineString);
        }

        public static bool operator ==(PostgisMultiLineString x, PostgisMultiLineString y)
        {
            if (object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisMultiLineString x, PostgisMultiLineString y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _lineStrings.Length; i++)
            {
                ret ^= PGUtil.RotateShift(_lineStrings[i].GetHashCode(), ret % sizeof(int));
            }
            return ret;
        }

        public int LineCount
        {
            get
            {
                return _lineStrings.Length;
            }
        }
    }

    /// <summary>
    /// Represents a Postgis 2D MultiPolygon.
    /// </summary>
    public class PostgisMultiPolygon : IGeometry, IEquatable<PostgisMultiPolygon>, IEnumerable<PostgisPolygon>
    {
        private PostgisPolygon[] _polygons;

        public IEnumerator<PostgisPolygon> GetEnumerator()
        {
            return ((IEnumerable<PostgisPolygon>)_polygons).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.MultiPolygon; }
        }

        public PostgisPolygon this[Int32 index]
        {
            get { return _polygons[index]; }
        }

        public PostgisMultiPolygon(PostgisPolygon[] polygons)
        {
            _polygons = polygons;
        }

        public PostgisMultiPolygon(IEnumerable<PostgisPolygon> polygons)
        {
            _polygons = polygons.ToArray();
        }

        public PostgisMultiPolygon(IEnumerable<IEnumerable<IEnumerable<Coordinate2D>>> ringList)
        {
            _polygons = ringList.Select(x => new PostgisPolygon(x)).ToArray();
        }

        public bool Equals(PostgisMultiPolygon other)
        {
            if (_polygons.Length != other._polygons.Length) return false;
            for (int i = 0; i < _polygons.Length; i++)
            {
                if (_polygons[i] != other._polygons[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is PostgisMultiPolygon && Equals((PostgisMultiPolygon)obj);
        }

        public static bool operator ==(PostgisMultiPolygon x, PostgisMultiPolygon y)
        {
            if (object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisMultiPolygon x, PostgisMultiPolygon y)
        {
            return !(x == y);
        }


        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _polygons.Length; i++)
            {
                ret ^= PGUtil.RotateShift(_polygons[i].GetHashCode(), ret % sizeof(int));
            }
            return ret;
        }

        protected override int GetLenHelper()
        {
            int n = 4;
            for (int i = 0; i < _polygons.Length; i++)
            {
                n += _polygons[i].GetLen();
            }
            return n;
        }


        public int PolygonCount
        {
            get
            {
                return _polygons.Length;
            }
        }
    }

    /// <summary>
    /// Represents a collection of Postgis feature.
    /// </summary>
    public class PostgisGeometryCollection : IGeometry, IEquatable<PostgisGeometryCollection>, IEnumerable<IGeometry>
    {
        private IGeometry[] _geometries;

        public IGeometry this[Int32 index]
        {
            get { return _geometries[index]; }
        }

        internal override WkbIdentifier Identifier
        {
            get { return WkbIdentifier.GeometryCollection; }
        }

        public IEnumerator<IGeometry> GetEnumerator()
        {
            return ((IEnumerable<IGeometry>)_geometries).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public PostgisGeometryCollection(IGeometry[] geometries)
        {
            _geometries = geometries;
        }

        public PostgisGeometryCollection(IEnumerable<IGeometry> geometries)
        {
            _geometries = geometries.ToArray();
        }

        public bool Equals(PostgisGeometryCollection other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (_geometries.Length != other._geometries.Length) return false;
            for (int i = 0; i < _geometries.Length; i++)
            {
                if (!_geometries[i].Equals(other._geometries[i])) return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PostgisGeometryCollection);
        }

        public static bool operator ==(PostgisGeometryCollection x, PostgisGeometryCollection y)
        {
            if (object.ReferenceEquals(x, null))
                return object.ReferenceEquals(y, null);
            return x.Equals(y);
        }

        public static bool operator !=(PostgisGeometryCollection x, PostgisGeometryCollection y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            int ret = 266370105;//seed with something other than zero to make paths of all zeros hash differently.
            for (int i = 0; i < _geometries.Length; i++)
            {
                ret ^= PGUtil.RotateShift(_geometries[i].GetHashCode(), ret % sizeof(int));
            }
            return ret;
        }

        protected override int GetLenHelper()
        {
            int n = 4;
            for (int i = 0; i < _geometries.Length; i++)
            {
                n += _geometries[i].GetLen();
            }
            return n;
        }

        public int GeometryCount
        {
            get
            {
                return _geometries.Length;
            }
        }
    }
}
