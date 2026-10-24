using NetTopologySuite;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace WaterWatch.Services;

public static class SpatialMath
{
    public const int Wgs84 = 4326;
    public const int WebMercator = 3857;
    public const int Utm32N = 32632;

    public static readonly GeometryFactory Wgs84Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(Wgs84);

    private static readonly CoordinateSystemFactory CsFactory = new();
    private static readonly CoordinateTransformationFactory CtFactory = new();

    private static readonly CoordinateSystem Wgs84Cs = GeographicCoordinateSystem.WGS84;
    private static readonly CoordinateSystem Utm32Cs = ProjectedCoordinateSystem.WGS84_UTM(32, true);

    public static Geometry EnsureWgs84(Geometry geometry)
    {
        if (geometry.SRID == Wgs84)
        {
            return geometry;
        }

        var clone = geometry.Copy();
        clone.SRID = Wgs84;
        return clone;
    }

    public static Geometry Transform(Geometry geometry, int fromSrid, int toSrid)
    {
        if (fromSrid == toSrid)
        {
            return geometry;
        }

        var source = fromSrid switch
        {
            Wgs84 => Wgs84Cs,
            Utm32N => Utm32Cs,
            _ => throw new NotSupportedException($"SRID {fromSrid} is not supported for transformation.")
        };
        var target = toSrid switch
        {
            Wgs84 => Wgs84Cs,
            Utm32N => Utm32Cs,
            _ => throw new NotSupportedException($"SRID {toSrid} is not supported for transformation.")
        };

        var transform = CtFactory.CreateFromCoordinateSystems(source, target).MathTransform;
        var copied = geometry.Copy();
        copied.Apply(new CoordinateTransformFilter(transform));
        copied.SRID = toSrid;
        copied.GeometryChanged();
        return copied;
    }

    public static Geometry ToUtm32(Geometry geometry) =>
        geometry.SRID == Utm32N ? geometry : Transform(geometry, geometry.SRID == 0 ? Wgs84 : geometry.SRID, Utm32N);

    public static Geometry ToWgs84(Geometry geometry) =>
        geometry.SRID == Wgs84 ? geometry : Transform(geometry, geometry.SRID == 0 ? Utm32N : geometry.SRID, Wgs84);

    public static double AreaSquareKilometers(Geometry geometry)
    {
        if (geometry.IsEmpty)
        {
            return 0;
        }
        return ToUtm32(geometry).Area / 1_000_000d;
    }

    public static double LengthMeters(Geometry geometry)
    {
        if (geometry.IsEmpty)
        {
            return 0;
        }
        return ToUtm32(geometry).Length;
    }

    public static Geometry BufferGeometryMeters(Geometry geometry, double meters)
    {
        var buffered = ToUtm32(geometry).Buffer(meters);
        return ToWgs84(buffered);
    }

    public static Polygon BufferMeters(Geometry geometry, double meters)
    {
        var wgs = BufferGeometryMeters(geometry, meters);
        return wgs as Polygon ?? throw new InvalidOperationException("Buffer did not produce a polygon.");
    }

    public static Point Point(double longitude, double latitude) =>
        Wgs84Factory.CreatePoint(new Coordinate(longitude, latitude));

    private sealed class CoordinateTransformFilter : ICoordinateSequenceFilter
    {
        private readonly MathTransform _transform;
        public CoordinateTransformFilter(MathTransform transform) => _transform = transform;

        public bool Done => false;
        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var x = seq.GetX(i);
            var y = seq.GetY(i);
            double[] input = { x, y };
            var output = _transform.Transform(input);
            seq.SetOrdinate(i, Ordinate.X, output[0]);
            seq.SetOrdinate(i, Ordinate.Y, output[1]);
            if (seq.HasZ)
            {
                seq.SetOrdinate(i, Ordinate.Z, 0);
            }
        }
    }
}
