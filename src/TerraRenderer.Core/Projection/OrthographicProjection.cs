using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Core.Projection;

public sealed class OrthographicProjection
{
    private readonly double _centerLat;
    private readonly double _centerLon;
    private readonly double _sinCenterLat;
    private readonly double _cosCenterLat;

    public OrthographicProjection(double centerLatitudeDegrees, double centerLongitudeDegrees)
    {
        _centerLat = DegreesToRadians(centerLatitudeDegrees);
        _centerLon = DegreesToRadians(centerLongitudeDegrees);
        _sinCenterLat = Math.Sin(_centerLat);
        _cosCenterLat = Math.Cos(_centerLat);
    }

    public bool TryUnproject(double x, double y, out GeoCoordinate coordinate)
    {
        var rhoSquared = x * x + y * y;
        if (rhoSquared > 1.0)
        {
            coordinate = default;
            return false;
        }

        var rho = Math.Sqrt(rhoSquared);
        if (rho < 1e-12)
        {
            coordinate = new(RadiansToDegrees(_centerLat), NormalizeLongitude(RadiansToDegrees(_centerLon)));
            return true;
        }

        var c = Math.Asin(Math.Clamp(rho, 0.0, 1.0));
        var sinC = Math.Sin(c);
        var cosC = Math.Cos(c);
        var latitude = Math.Asin(cosC * _sinCenterLat + y * sinC * _cosCenterLat / rho);
        var longitude = _centerLon + Math.Atan2(
            x * sinC,
            rho * _cosCenterLat * cosC - y * _sinCenterLat * sinC);

        coordinate = new(RadiansToDegrees(latitude), NormalizeLongitude(RadiansToDegrees(longitude)));
        return true;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
    private static double RadiansToDegrees(double value) => value * 180.0 / Math.PI;
    private static double NormalizeLongitude(double value)
    {
        value %= 360.0;
        if (value > 180.0) value -= 360.0;
        if (value < -180.0) value += 360.0;
        return value;
    }
}
