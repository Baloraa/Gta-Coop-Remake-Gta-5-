using System.Windows;
using System.Windows.Controls;

namespace GTACoop.Remake.Wpf.Pages;

/// <summary>Henüz taşınmamış bölümler için başlık + açıklama kartı.</summary>
public sealed class PlaceholderPage : StackPanel
{
    public PlaceholderPage(string title, string hint)
    {
        Margin = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Top;

        Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.Resources["H1"],
            Margin = new Thickness(0, 0, 0, 6)
        });

        Children.Add(new TextBlock
        {
            Text = hint,
            Style = (Style)Application.Current.Resources["Muted"],
            Margin = new Thickness(0, 0, 0, 16)
        });

        var card = new Border { Style = (Style)Application.Current.Resources["Card"] };
        card.Child = new TextBlock
        {
            Text = "Bu bölüm WinForms sürümünde çalışmaya devam ediyor; WPF'e sırayla taşınıyor.",
            Style = (Style)Application.Current.Resources["Muted"]
        };

        Children.Add(card);
    }
}
