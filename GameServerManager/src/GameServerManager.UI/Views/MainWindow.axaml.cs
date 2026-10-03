using Avalonia.Controls;
using Avalonia.Input;
using GameServerManager.UI.ViewModels;

namespace GameServerManager.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Terminal_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            string[]? lines = textBox.Text?.Split('\n');
            string lastLine = lines?.Length > 0 ? lines[^1] : string.Empty;
            
            // Remove prompt if present
            string command = lastLine.StartsWith("> ") ? lastLine[2..] : lastLine;

            if (!string.IsNullOrWhiteSpace(command) && DataContext is MainWindowViewModel vm)
            {
                vm.CommandInput = command;
                await vm.ExecuteCommandCommand.ExecuteAsync(null);
            }
            e.Handled = true;
        }
    }

    private async void ExecInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            string command = textBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(command) && DataContext is MainWindowViewModel vm)
            {
                textBox.Text = string.Empty;
                await vm.RunExecCommandAsync(command);
            }
            e.Handled = true;
        }
    }

    private async void ExecTerminal_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            string text = textBox.Text ?? string.Empty;
            int lastPromptIndex = text.LastIndexOf("# ");
            string command = string.Empty;
            if (lastPromptIndex >= 0)
            {
                command = text[(lastPromptIndex + 2)..].Trim();
            }
            else
            {
                command = text.Trim();
            }

            if (!string.IsNullOrWhiteSpace(command) && DataContext is MainWindowViewModel vm)
            {
                textBox.Text = text + "\n";
                await vm.RunExecCommandAsync(command);
            }
            e.Handled = true;
        }
    }
}
