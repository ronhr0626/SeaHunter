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

// Draws the game and runs the game loop
public class GameCanvas : Control
{
    private static readonly IBrush TorpedoBrush = new SolidColorBrush(Color.Parse("#D8DEE9"));
    private static readonly IBrush AmmoFullBrush = new SolidColorBrush(Color.Parse("#FFD34D"));
    private static readonly IBrush AmmoEmptyBrush = new SolidColorBrush(Color.Parse("#40FFFFFF"));
    private static readonly IBrush HudBackBrush = new SolidColorBrush(Color.Parse("#60000000"));

    private static readonly Typeface HudFont = new("Consolas", FontStyle.Normal, FontWeight.Bold);

    private readonly GameWorld _world = new();
    private readonly InputState _input = new();
    private readonly Bitmap _background;
    private readonly Bitmap _subSprite;
    private readonly Bitmap _patrolBoatSprite;
    private readonly Bitmap _freighterSprite;
    private readonly Bitmap _battleshipSprite;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TopLevel? _topLevel;

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
        _input.Press(e.PhysicalKey);
        e.Handled = true; // Stop Space etc. from doing anything else in the window
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e) => _input.Release(e.PhysicalKey);

    private void GameLoop()
    {
        // Time since last frame in seconds
        double dt = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        dt = Math.Min(dt, 0.05); // Avoid huge jumps if the window freezes

        _world.Update(dt, _input);
        _input.EndFrame(); // Forget one-shot presses so they only count once

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
            DrawTorpedoes(context);
            DrawExplosions(context);
            DrawSubmarine(context, _world.Player);
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

    private void DrawSubmarine(DrawingContext context, Submarine sub) =>
        DrawSprite(context, _subSprite, sub.X, sub.Y, sub.Width, sub.Height, sub.FacingRight);

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

    // Text with a dark shadow so it's readable on any background
    private static void DrawText(DrawingContext context, string text, double x, double y, double size)
    {
        var shadow = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            HudFont, size, Brushes.Black);
        var main = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            HudFont, size, Brushes.White);

        context.DrawText(shadow, new Point(x + 1, y + 1));
        context.DrawText(main, new Point(x, y));
    }
}