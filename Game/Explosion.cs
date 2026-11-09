namespace SeaHunter.Game;

public class Explosion
{
    public const double Duration = 0.6; // Seconds

    public double X { get; init; }
    public double Y { get; init; }
    public double Age { get; private set; }

    // 0.0 = just started, 1.0 = finished
    public double Progress => Age / Duration;
    public bool IsDone => Age >= Duration;

    public void Update(double dt) => Age += dt;
}