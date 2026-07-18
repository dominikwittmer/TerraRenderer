using TerraRenderer.Core.Geometry;

namespace TerraRenderer.Core.Projection;

/// <summary>
/// A pinhole camera looking at the centre of a unit sphere. The camera position is
/// defined by the geographic point that faces it, giving a controllable hero composition
/// while retaining a genuine perspective projection.
/// </summary>
public sealed class PerspectiveEarthProjection
{
    private readonly Vector3d _cameraPosition;
    private readonly Vector3d _forward;
    private readonly Vector3d _right;
    private readonly Vector3d _up;
    private readonly double _tanHalfFov;
    private readonly double _aspectRatio;

    public PerspectiveEarthProjection(int width, int height, double centerLatitudeDegrees,
        double centerLongitudeDegrees, double cameraDistance, double fieldOfViewDegrees,
        double rollDegrees = 0.0)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (cameraDistance <= 1.0) throw new ArgumentOutOfRangeException(nameof(cameraDistance),
            "Camera distance must be greater than the unit sphere radius.");
        if (fieldOfViewDegrees is <= 1.0 or >= 120.0) throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees));

        _aspectRatio = width / (double)height;
        _tanHalfFov = Math.Tan(DegreesToRadians(fieldOfViewDegrees) * 0.5);

        var facingPoint = ToCartesian(centerLatitudeDegrees, centerLongitudeDegrees).Normalize();
        _cameraPosition = facingPoint * cameraDistance;
        _forward = (-facingPoint).Normalize();

        var worldNorth = new Vector3d(0.0, 0.0, 1.0);
        var east = Vector3d.Cross(worldNorth, facingPoint).Normalize();
        if (east.LengthSquared < 1e-12) east = new Vector3d(0.0, 1.0, 0.0);
        var north = Vector3d.Cross(facingPoint, east).Normalize();

        var roll = DegreesToRadians(rollDegrees);
        var cosRoll = Math.Cos(roll);
        var sinRoll = Math.Sin(roll);
        _right = (east * cosRoll + north * sinRoll).Normalize();
        _up = (north * cosRoll - east * sinRoll).Normalize();
    }

    public CameraRay CreateRay(double normalizedX, double normalizedY)
    {
        var direction = (_forward
                         + _right * (normalizedX * _aspectRatio * _tanHalfFov)
                         + _up * (normalizedY * _tanHalfFov)).Normalize();
        return new CameraRay(_cameraPosition, direction);
    }

    public static bool TryIntersect(CameraRay ray, double sphereRadius, out SphereIntersection intersection)
    {
        var b = Vector3d.Dot(ray.Origin, ray.Direction);
        var c = ray.Origin.LengthSquared - sphereRadius * sphereRadius;
        var discriminant = b * b - c;
        if (discriminant < 0.0)
        {
            intersection = default;
            return false;
        }

        var root = Math.Sqrt(discriminant);
        var distance = -b - root;
        if (distance <= 0.0) distance = -b + root;
        if (distance <= 0.0)
        {
            intersection = default;
            return false;
        }

        var point = ray.Origin + ray.Direction * distance;
        var normal = (point / sphereRadius).Normalize();
        intersection = new SphereIntersection(point, normal, distance);
        return true;
    }

    public static GeoCoordinate ToGeoCoordinate(Vector3d normal)
    {
        var n = normal.Normalize();
        var latitude = Math.Asin(Math.Clamp(n.Z, -1.0, 1.0));
        var longitude = Math.Atan2(n.Y, n.X);
        return new GeoCoordinate(RadiansToDegrees(latitude), NormalizeLongitude(RadiansToDegrees(longitude)));
    }

    private static Vector3d ToCartesian(double latitudeDegrees, double longitudeDegrees)
    {
        var lat = DegreesToRadians(latitudeDegrees);
        var lon = DegreesToRadians(longitudeDegrees);
        var cosLat = Math.Cos(lat);
        return new Vector3d(cosLat * Math.Cos(lon), cosLat * Math.Sin(lon), Math.Sin(lat));
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

public readonly record struct CameraRay(Vector3d Origin, Vector3d Direction);
public readonly record struct SphereIntersection(Vector3d Point, Vector3d Normal, double Distance);
