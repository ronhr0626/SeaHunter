using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SeaHunter.Game;

namespace SeaHunter;

// Draws the game and runs the game loop. Menus are handled by MainWindow.
public class GameCanvas : Control
{
    private static readonly IBrush TorpedoBrush = new SolidColorBrush(Color.Parse("#D8DEE9"));
    private static readonly IBrush ChargeBrush = new SolidColorBrush(Color.Parse("#1C1F26"));
    private static readonly IBrush ChargeBandBrush = new SolidColorBrush(Color.Parse("#E0A030"));
    private static readonly IBrush AmmoFullBrush = new SolidColorBrush(Color.Parse("#FFD34D"));
    private static readonly IBrush AmmoEmptyBrush = new SolidColorBrush(Color.Parse("#40FFFFFF"));
    private static readonly IBrush HudBackBrush = new SolidColorBrush(Color.Parse("#60000000"));

    private static readonly Typeface HudFont = new("Consolas", FontStyle.Normal, FontWeight.Bold);

    private GameWorld _world = new(); // Replaced every time a new game starts
    private readonly InputState _input = new();
    private readonly Bitmap _background;
    private readonly Bitmap _subSprite;
    private readonly Bitmap _patrolBoatSprite;
    private readonly Bitmap _freighterSprite;
    private readonly Bitmap _battleshipSprite;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TopLevel? _topLevel;

    // True while a game is being played (not in menus)
    public bool IsRunning { get; private set; }

    // Raised once when the player loses their last life. The int is the final score.
    public event Action<int>? GameOver;

    public GameCanvas()
    {
        _background = LoadImage("BackgroundSea.png");
        _subSprite = LoadImage("sub.png");
        _patrolBoatSprite = LoadImage("1.png");
        _freighterSprite = LoadImage("2.png");
        _battleshipSprite = LoadImage("3.png");

        // Keep pixel art sharp when scaled up
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

        // ~60 frames per second
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => GameLoop();
    }

    private static Bitmap LoadImage(string fileName) =>
        new(AssetLoader.Open(new Uri($"avares://SeaHunter/Assets/{fileName}")));

    public void StartNewGame()
    {
        _world = new GameWorld();
        IsRunning = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Listen for keys on the whole window, not just this control
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(KeyUpEvent, HandleKeyUp, RoutingStrategies.Tunnel);

        _clock.Start();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        _topLevel?.RemoveHandler(KeyDownEvent, HandleKeyDown);
        _topLevel?.RemoveHandler(KeyUpEvent, HandleKeyUp);
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        // In menus, let keys reach the buttons (Enter, Tab, Space)
        if (!IsRunning) return;

        _input.Press(e.PhysicalKey);
        e.Handled = true; // Stop Space etc. from clicking hidden buttons
    }

    // Always track releases, so no key gets "stuck" when switching screens
    private void HandleKeyUp(object? sender, KeyEventArgs e) => _input.Release(e.PhysicalKey);

    private void GameLoop()
    {
        // Time since last frame in seconds
        double dt = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        dt = Math.Min(dt, 0.05); // Avoid huge jumps if the window freezes

        // Update while playing, and after game over so the last explosions finish
        if (IsRunning || _world.IsGameOver)
            _world.Update(dt, _input);

        _input.EndFrame(); // Forget one-shot presses so they only count once

        // Tell the window exactly once that the game just ended
        if (IsRunning && _world.IsGameOver)
        {
            IsRunning = false;
            GameOver?.Invoke(_world.Score);
        }

        InvalidateVisual(); // Ask Avalonia to call Render() again
    }

    public override void Render(DrawingContext context)
    {
        // Black bars around the game if the window has a different shape
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        // Scale the 640x360 world to fit the window, centered
        double scale = Math.Min(Bounds.Width / GameWorld.Width, Bounds.Height / GameWorld.Height);
        double offsetX = (Bounds.Width - GameWorld.Width * scale) / 2;
        double offsetY = (Bounds.Height - GameWorld.Height * scale) / 2;

        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);

        // Everything inside this block is drawn in world coordinates (0..640, 0..360)
        using (context.PushTransform(transform))
        {
            context.DrawImage(_background, new Rect(0, 0, GameWorld.Width, GameWorld.Height));
            DrawShips(context);
            DrawDepthCharges(context);
            DrawTorpedoes(context);
            DrawSubmarine(context);
            DrawExplosions(context);

            if (IsRunning)
                DrawHud(context);
        }
    }

    // Draws a sprite that faces right, mirrored when facingRight is false
    private static void DrawSprite(DrawingContext context, Bitmap sprite,
        double x, double y, double width, double height, bool facingRight)
    {
        if (facingRight)
        {
            context.DrawImage(sprite, new Rect(x, y, width, height));
            return;
        }

        // Mirror horizontally: flip X, then move back into place
        var flip = Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(x + width, y);
        using (context.PushTransform(flip))
            context.DrawImage(sprite, new Rect(0, 0, width, height));
    }

    private void DrawShips(DrawingContext context)
    {
        // Draw far ships first so near ships end up on top
        foreach (var s in _world.Ships.OrderBy(s => s.Lane))
        {
            var sprite = s.Type switch
            {
                ShipType.PatrolBoat => _patrolBoatSprite,
                ShipType.Freighter => _freighterSprite,
                _ => _battleshipSprite
            };

            // Ships further away are slightly faded, like haze over the sea
            double opacity = s.Lane switch
            {
                Lane.Far => 0.7,
                Lane.Middle => 0.85,
                _ => 1.0
            };

            using (context.PushOpacity(opacity))
                DrawSprite(context, sprite, s.X, s.Y, s.Width, s.Height, s.MovingRight);
        }
    }

    private void DrawSubmarine(DrawingContext context)
    {
        // Hidden in menus and after game over
        if (!IsRunning) return;

        // Blink while invulnerable: skip every other 0.1 second
        if (_world.IsInvulnerable && (int)(_world.InvulnerableTimeLeft * 10) % 2 == 0)
            return;

        var sub = _world.Player;
        DrawSprite(context, _subSprite, sub.X, sub.Y, sub.Width, sub.Height, sub.FacingRight);
    }

    // Small dark barrels with a yellow band
    private void DrawDepthCharges(DrawingContext context)
    {
        foreach (var c in _world.DepthCharges)
        {
            var body = new Rect(c.X, c.Y, DepthCharge.Size, DepthCharge.Size);
            context.DrawRectangle(ChargeBrush, null, body, 1.5, 1.5);
            context.FillRectangle(ChargeBandBrush,
                new Rect(c.X, c.Y + DepthCharge.Size * 0.4, DepthCharge.Size, 1.2));
        }
    }

    private void DrawTorpedoes(DrawingContext context)
    {
        foreach (var t in _world.Torpedoes)
        {
            // Bubble trail: small fading circles behind the torpedo
            for (int i = 1; i <= 3; i++)
            {
                byte alpha = (byte)(140 - i * 40);
                var bubble = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
                double size = 2.5 - i * 0.4;
                context.DrawEllipse(bubble, null,
                    new Point(t.X + Torpedo.Width / 2, t.Y + Torpedo.Height + i * 4), size, size);
            }

            // Torpedo body
            context.DrawRectangle(TorpedoBrush, null,
                new Rect(t.X, t.Y, Torpedo.Width, Torpedo.Height), 1.5, 1.5);
        }
    }

    // Expanding, fading fireball
    private void DrawExplosions(DrawingContext context)
    {
        foreach (var e in _world.Explosions)
        {
            byte alpha = (byte)(255 * (1 - e.Progress));
            double radius = 4 + e.Progress * 16;

            var outer = new SolidColorBrush(Color.FromArgb(alpha, 255, 140, 0));
            var inner = new SolidColorBrush(Color.FromArgb(alpha, 255, 230, 120));

            context.DrawEllipse(outer, null, new Point(e.X, e.Y), radius, radius);
            context.DrawEllipse(inner, null, new Point(e.X, e.Y), radius * 0.5, radius * 0.5);
        }
    }

    private void DrawHud(DrawingContext context)
    {
        DrawAmmo(context);
        DrawText(context, $"SCORE {_world.Score:D6}", 10, 8, 14);
        DrawText(context, $"LEVEL {_world.Level}", 10, 26, 12);
        DrawText(context, $"LIVES {_world.Lives}", GameWorld.Width - 10, 8, 14, TextAlignment.Right);

        // Big banner in the middle for a moment after leveling up
        if (_world.LevelUpTimeLeft > 0)
            DrawText(context, $"LEVEL {_world.Level}", GameWorld.Width / 2, 150, 28, TextAlignment.Center);
    }

    // Torpedo ammo at the top center, with a reload bar when empty
    private void DrawAmmo(DrawingContext context)
    {
        const double slotWidth = 6;
        const double slotHeight = 14;
        const double gap = 4;

        double totalWidth = GameWorld.MaxTorpedoes * slotWidth + (GameWorld.MaxTorpedoes - 1) * gap;
        double startX = GameWorld.Width / 2 - totalWidth / 2;
        const double y = 8;

        // Dark backing so the HUD is readable on the bright sky
        context.DrawRectangle(HudBackBrush, null,
            new Rect(startX - 6, y - 4, totalWidth + 12, slotHeight + 14), 4, 4);

        for (int i = 0; i < GameWorld.MaxTorpedoes; i++)
        {
            var brush = i < _world.Ammo ? AmmoFullBrush : AmmoEmptyBrush;
            context.DrawRectangle(brush, null,
                new Rect(startX + i * (slotWidth + gap), y, slotWidth, slotHeight), 2, 2);
        }

        if (_world.IsReloading)
        {
            double barY = y + slotHeight + 3;
            context.FillRectangle(AmmoEmptyBrush, new Rect(startX, barY, totalWidth, 3));
            context.FillRectangle(AmmoFullBrush, new Rect(startX, barY, totalWidth * _world.ReloadProgress, 3));
        }
    }

    private static FormattedText MakeText(string text, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, HudFont, size, brush);

    // Text with a dark shadow. x is the left edge, center or right edge depending on align.
    private static void DrawText(DrawingContext context, string text, double x, double y, double size,
        TextAlignment align = TextAlignment.Left)
    {
        var main = MakeText(text, size, Brushes.White);
        var shadow = MakeText(text, size, Brushes.Black);

        double drawX = align switch
        {
            TextAlignment.Center => x - main.Width / 2,
            TextAlignment.Right => x - main.Width,
            _ => x
        };

        context.DrawText(shadow, new Point(drawX + 1, y + 1));
        context.DrawText(main, new Point(drawX, y));
    }
}