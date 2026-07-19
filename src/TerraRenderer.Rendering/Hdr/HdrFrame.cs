namespace TerraRenderer.Rendering.Hdr;

internal sealed class HdrFrame
{
    private readonly HdrColor[] _pixels;

    public HdrFrame(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new HdrColor[checked(width * height)];
    }

    public int Width { get; }
    public int Height { get; }

    public HdrColor this[int x, int y]
    {
        get => _pixels[y * Width + x];
        set => _pixels[y * Width + x] = value;
    }

    public HdrColor[] Pixels => _pixels;
}
