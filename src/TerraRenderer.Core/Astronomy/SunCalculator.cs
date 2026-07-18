namespace TerraRenderer.Core.Astronomy;

public static class SunCalculator
{
    public static SunPosition Calculate(DateTimeOffset timeUtc)
    {
        var utc = timeUtc.ToUniversalTime();
        var julianDay = ToJulianDay(utc);
        var n = julianDay - 2451545.0;
        var meanLongitude = NormalizeDegrees(280.460 + 0.9856474 * n);
        var meanAnomaly = DegreesToRadians(NormalizeDegrees(357.528 + 0.9856003 * n));
        var eclipticLongitude = DegreesToRadians(NormalizeDegrees(
            meanLongitude + 1.915 * Math.Sin(meanAnomaly) + 0.020 * Math.Sin(2.0 * meanAnomaly)));
        var obliquity = DegreesToRadians(23.439 - 0.0000004 * n);
        var rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(eclipticLongitude), Math.Cos(eclipticLongitude));
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(eclipticLongitude));
        var gmst = NormalizeDegrees(280.46061837 + 360.98564736629 * (julianDay - 2451545.0));
        var subsolarLongitude = NormalizeLongitude(RadiansToDegrees(rightAscension) - gmst);
        return new(RadiansToDegrees(declination), subsolarLongitude);
    }

    private static double ToJulianDay(DateTimeOffset utc)
    {
        var unixSeconds = utc.ToUnixTimeMilliseconds() / 1000.0;
        return unixSeconds / 86400.0 + 2440587.5;
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;
        return value < 0 ? value + 360.0 : value;
    }

    private static double NormalizeLongitude(double value)
    {
        value %= 360.0;
        if (value > 180.0) value -= 360.0;
        if (value < -180.0) value += 360.0;
        return value;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
    private static double RadiansToDegrees(double value) => value * 180.0 / Math.PI;
}
