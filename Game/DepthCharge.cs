namespace SeaHunter.Game;

public class DepthCharge
{
    public const double Size = 6;
    public const double SinkSpeed = 45; // Pixels per second, straight down

    public double X { get; set; }
    public double Y { get; set; }

    public void Update(double dt) => Y += SinkSpeed * dt;
}