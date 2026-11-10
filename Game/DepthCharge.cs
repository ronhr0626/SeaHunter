namespace SeaHunter.Game;

public class DepthCharge
{
    public const double Size = 6;
    public const double BaseSinkSpeed = 45; // Pixels per second at level 1

    public double X { get; set; }
    public double Y { get; set; }

    // Set when dropped, so higher levels can sink faster
    public double SinkSpeed { get; init; } = BaseSinkSpeed;

    public void Update(double dt) => Y += SinkSpeed * dt;
}