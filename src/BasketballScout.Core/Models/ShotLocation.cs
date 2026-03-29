namespace BasketballScout.Core.Models;

public class ShotLocation
{
    public float X { get; set; }
    public float Y { get; set; }

    public ShotLocation(float x, float y)
    {
        X = x;
        Y = y;
    }
}
