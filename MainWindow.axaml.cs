using Avalonia.Controls;
using Avalonia.Interactivity;
using SeaHunter.Game;

namespace SeaHunter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // The canvas tells us when the player has lost
        Game.GameOver += OnGameOver;

        StartHighScoreList.ItemsSource = HighScoreStore.Format(HighScoreStore.Load());
    }

    private void OnStartClick(object? sender, RoutedEventArgs e) => StartGame();
    private void OnPlayAgainClick(object? sender, RoutedEventArgs e) => StartGame();

    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        GameOverScreen.IsVisible = false;
        StartScreen.IsVisible = true;
        StartHighScoreList.ItemsSource = HighScoreStore.Format(HighScoreStore.Load());
    }

    private void StartGame()
    {
        StartScreen.IsVisible = false;
        GameOverScreen.IsVisible = false;
        Game.StartNewGame();
    }

    private void OnGameOver(int score)
    {
        // Only save real scores
        var top = score > 0 ? HighScoreStore.Add(score) : HighScoreStore.Load();

        FinalScoreText.Text = $"SCORE {score:D6}";
        NewHighScoreText.IsVisible = score > 0 && top[0] == score;
        GameOverHighScoreList.ItemsSource = HighScoreStore.Format(top);

        GameOverScreen.IsVisible = true;
    }
}