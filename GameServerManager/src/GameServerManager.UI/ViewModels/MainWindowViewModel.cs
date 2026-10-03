using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerManager.Application.Models;

namespace GameServerManager.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:5056/")
    };

    [ObservableProperty]
    private string _imageName = "nginx:alpine";

    [ObservableProperty]
    private string _containerName = string.Empty;

    [ObservableProperty]
    private string _port = "8080";

    [ObservableProperty]
    private string _statusMessage = "Готов к работе.";

    [ObservableProperty]
    private string _containerId = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ContainerModel> _containers = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    private List<ContainerModel> _allContainers = new();

    [ObservableProperty]
    private ContainerModel? _selectedContainer;

    [ObservableProperty]
    private string _containerLogs = string.Empty;

    [ObservableProperty]
    private string _commandInput = string.Empty;

    [ObservableProperty]
    private string _terminalOutput = "Терминал\n";

    [ObservableProperty]
    private string _cpuUsage = "0.00%";

    [ObservableProperty]
    private string _memoryUsage = "0.00 MB";

    [ObservableProperty]
    private bool _isLiveStatsEnabled;

    private CancellationTokenSource? _liveStatsCts;

    // Единственный конструктор для автоматической загрузки списка контейнеров при запуске
    public MainWindowViewModel()
    {
        _ = LoadContainersAsync();
    }

    private bool IsContainerSelected() => SelectedContainer is not null;

    [ObservableProperty]
    private bool _isContainersViewActive = true;

    [ObservableProperty]
    private bool _isLogsViewActive = false;

    [ObservableProperty]
    private ContainerModel? _selectedLogContainer;

    [ObservableProperty]
    private bool _isStatsViewActive = false;

    [ObservableProperty]
    private ContainerModel? _selectedStatsContainer;

    [ObservableProperty]
    private bool _isImagesViewActive = false;

    [ObservableProperty]
    private bool _isExecViewActive = false;

    private readonly Dictionary<string, string> _execSessions = new();

    [ObservableProperty]
    private string _currentExecOutput = string.Empty;

    [ObservableProperty]
    private ContainerModel? _selectedExecContainer;

    [ObservableProperty]
    private string _execCommandInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ImageModel> _images = new();

    [ObservableProperty]
    private ImageModel? _selectedImage;

    [ObservableProperty]
    private string _statsCpuUsage = "0.00%";

    [ObservableProperty]
    private string _statsMemoryUsage = "0.00 MB";

    partial void OnSelectedContainerChanged(ContainerModel? value)
    {
        CpuUsage = "0.00%";
        MemoryUsage = "0.00 MB";
        if (value is not null)
        {
            ContainerId = value.Id;
        }

        StopServerCommand.NotifyCanExecuteChanged();
        StartExistingCommand.NotifyCanExecuteChanged();
        RemoveContainerCommand.NotifyCanExecuteChanged();
        GetStatsCommand.NotifyCanExecuteChanged();
        GetLogsCommand.NotifyCanExecuteChanged();
        ExecuteCommandCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLogContainerChanged(ContainerModel? value)
    {
        if (value is not null)
        {
            _ = GetLogsForContainerAsync(value);
        }
        else
        {
            ContainerLogs = string.Empty;
        }
    }

    partial void OnSelectedStatsContainerChanged(ContainerModel? value)
    {
        if (value is not null)
        {
            _ = GetStatsForContainerAsync(value);
        }
        else
        {
            StatsCpuUsage = "0.00%";
            StatsMemoryUsage = "0.00 MB";
        }
    }

    [RelayCommand]
    private void ShowContainersView()
    {
        IsContainersViewActive = true;
        IsLogsViewActive = false;
        IsStatsViewActive = false;
        IsImagesViewActive = false;
        IsExecViewActive = false;
    }

    [RelayCommand]
    private void ShowLogsView()
    {
        IsContainersViewActive = false;
        IsLogsViewActive = true;
        IsStatsViewActive = false;
        IsImagesViewActive = false;
        IsExecViewActive = false;
        if (SelectedLogContainer is null && Containers.Count > 0)
        {
            SelectedLogContainer = Containers[0];
        }
        else if (SelectedLogContainer is not null)
        {
            _ = GetLogsForContainerAsync(SelectedLogContainer);
        }
    }

    [RelayCommand]
    private void ShowStatsView()
    {
        IsContainersViewActive = false;
        IsLogsViewActive = false;
        IsStatsViewActive = true;
        IsImagesViewActive = false;
        IsExecViewActive = false;
        if (SelectedStatsContainer is null && Containers.Count > 0)
        {
            SelectedStatsContainer = Containers[0];
        }
        else if (SelectedStatsContainer is not null)
        {
            _ = GetStatsForContainerAsync(SelectedStatsContainer);
        }
    }

    [RelayCommand]
    private void ShowImagesView()
    {
        IsContainersViewActive = false;
        IsLogsViewActive = false;
        IsStatsViewActive = false;
        IsImagesViewActive = true;
        IsExecViewActive = false;
        _ = LoadImagesAsync();
    }

    [RelayCommand]
    private void ShowExecView()
    {
        IsContainersViewActive = false;
        IsLogsViewActive = false;
        IsStatsViewActive = false;
        IsImagesViewActive = false;
        IsExecViewActive = true;
        if (SelectedExecContainer is null && Containers.Count > 0)
        {
            SelectedExecContainer = Containers[0];
        }
    }

    partial void OnSelectedExecContainerChanged(ContainerModel? value)
    {
        if (value is not null)
        {
            if (!_execSessions.ContainsKey(value.Id))
            {
                _execSessions[value.Id] = $"ServerPulse Interactive Shell [Container: {value.Names}]\nType commands below.\n\n# ";
            }
            CurrentExecOutput = _execSessions[value.Id];
        }
        else
        {
            CurrentExecOutput = "No container selected.\n\n# ";
        }
    }

    [RelayCommand]
    private async Task ExecuteExecCommandAsync()
    {
        ContainerModel? target = SelectedExecContainer;
        if (target is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ExecCommandInput))
        {
            return;
        }

        string cmd = ExecCommandInput.Trim();
        ExecCommandInput = string.Empty;

        string currentText = _execSessions.TryGetValue(target.Id, out string? sessionText) ? sessionText : string.Empty;
        if (!string.IsNullOrEmpty(currentText) && !currentText.EndsWith("\n"))
        {
            currentText += "\n";
        }
        currentText += $"# {cmd}\n";
        _execSessions[target.Id] = currentText;
        CurrentExecOutput = currentText;

        if (!target.IsRunning)
        {
            currentText += $"Ошибка: контейнер '{target.Names}' остановлен. Команда exec поддерживается только для работающих (running) контейнеров.\n\n";
            _execSessions[target.Id] = currentText;
            CurrentExecOutput = currentText;
            return;
        }

        try
        {
            string targetId = target.Id;
            Dictionary<string, string> requestData = new Dictionary<string, string> { { "command", cmd } };
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/servers/{targetId}/exec", requestData);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                currentText += $"{result}\n";
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                currentText += $"Ошибка выполнения: {response.StatusCode} - {error}\n";
            }
        }
        catch (Exception ex)
        {
            currentText += $"Ошибка подключения к бэкенду: {ex.Message}\n";
        }

        _execSessions[target.Id] = currentText;
        CurrentExecOutput = currentText;
    }

    public async Task RunExecCommandAsync(string cmd)
    {
        await ExecuteExecCommandAsync();
    }

    [RelayCommand]
    public async Task LoadImagesAsync()
    {
        try
        {
            IEnumerable<ImageModel>? imagesList = await _httpClient.GetFromJsonAsync<IEnumerable<ImageModel>>("api/servers/images");
            if (imagesList is not null)
            {
                Images = new ObservableCollection<ImageModel>(imagesList);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки образов: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveImageAsync(ImageModel? image = null)
    {
        ImageModel? target = image ?? SelectedImage;
        if (target is null)
        {
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Удаление образа...";

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/servers/images/{targetId}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Образ успешно удален.";
                await LoadImagesAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка удаления образа: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    partial void OnIsLiveStatsEnabledChanged(bool value)
    {
        if (value)
        {
            _liveStatsCts = new CancellationTokenSource();
            _ = PollLiveStatsAsync(_liveStatsCts.Token);
        }
        else
        {
            _liveStatsCts?.Cancel();
            _liveStatsCts?.Dispose();
            _liveStatsCts = null;
        }
    }

    private async Task PollLiveStatsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (SelectedContainer is not null)
                {
                    DockerContainerStats? stats = await _httpClient.GetFromJsonAsync<DockerContainerStats>($"api/servers/{SelectedContainer.Id}/stats", token);
                    if (stats is not null)
                    {
                        CpuUsage = $"{stats.CpuPercentage:F2}%";
                        MemoryUsage = $"{stats.MemoryBytes / (1024.0 * 1024.0):F2} MB";
                    }
                }
            }
            catch
            {
                // Тихо игнорируем ошибки пуллинга статистики, чтобы не засорять UI
            }

            await Task.Delay(1500, token);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        List<ContainerModel> filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allContainers
            : _allContainers.Where(c =>
                (c.Names is not null && c.Names.ToLowerInvariant().Contains(SearchText.Trim().ToLowerInvariant())) ||
                (c.Image is not null && c.Image.ToLowerInvariant().Contains(SearchText.Trim().ToLowerInvariant()))
              ).ToList();

        List<string> filteredIds = filtered.Select(c => c.Id).ToList();
        List<ContainerModel> toRemove = Containers.Where(c => !filteredIds.Contains(c.Id)).ToList();
        foreach (ContainerModel item in toRemove)
        {
            Containers.Remove(item);
        }

        foreach (ContainerModel item in filtered)
        {
            if (!Containers.Contains(item))
            {
                Containers.Add(item);
            }
        }

        for (int i = 0; i < filtered.Count; i++)
        {
            ContainerModel item = filtered[i];
            int currentIndex = Containers.IndexOf(item);
            if (currentIndex >= 0 && currentIndex != i)
            {
                Containers.Move(currentIndex, i);
            }
        }
    }

    [RelayCommand]
    public async Task LoadContainersAsync()
    {
        try
        {
            IEnumerable<ContainerModel>? containersList = await _httpClient.GetFromJsonAsync<IEnumerable<ContainerModel>>("api/servers");
            if (containersList is not null)
            {
                List<ContainerModel> newList = new List<ContainerModel>(containersList);

                Dictionary<string, ContainerModel> allDict = _allContainers.ToDictionary(c => c.Id);
                List<ContainerModel> updatedAll = new List<ContainerModel>();
                foreach (ContainerModel newItem in newList)
                {
                    if (allDict.TryGetValue(newItem.Id, out ContainerModel? existingItem))
                    {
                        existingItem.State = newItem.State;
                        existingItem.Status = newItem.Status;
                        existingItem.Names = newItem.Names;
                        existingItem.Ports = newItem.Ports;
                        existingItem.Image = newItem.Image;
                        updatedAll.Add(existingItem);
                    }
                    else
                    {
                        updatedAll.Add(newItem);
                    }
                }
                _allContainers = updatedAll;
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки списка: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        try
        {
            StatusMessage = "Запуск контейнера...";

            if (!int.TryParse(Port, out int portNumber))
            {
                StatusMessage = "Ошибка: порт должен быть числом!";
                return;
            }

            Dictionary<string, object?> requestData = new Dictionary<string, object?>
            {
                { "imageName", ImageName },
                { "containerName", string.IsNullOrWhiteSpace(ContainerName) ? null : ContainerName },
                { "port", portNumber }
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/servers/start", requestData);

            if (response.IsSuccessStatusCode)
            {
                JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
                string? id = result.GetProperty("id").GetString();
                ContainerId = id ?? string.Empty;
                StatusMessage = $"Успешно запущен!\nID: {ContainerId}";

                await LoadContainersAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка запуска: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopServerAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedContainer;
        if (target is null)
        {
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Остановка контейнера...";

            HttpResponseMessage response = await _httpClient.PostAsync($"api/servers/{targetId}/stop", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = $"Контейнер {targetId[..Math.Min(12, targetId.Length)]} остановлен.";
                await LoadContainersAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка остановки: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartExistingAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedContainer;
        if (target is null)
        {
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Запуск существующего контейнера...";

            HttpResponseMessage response = await _httpClient.PostAsync($"api/servers/{targetId}/start-existing", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Контейнер успешно запущен!";
                await LoadContainersAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка запуска: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveContainerAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedContainer;
        if (target is null)
        {
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Удаление контейнера...";

            HttpResponseMessage response = await _httpClient.DeleteAsync($"api/servers/{targetId}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Контейнер полностью удален.";
                if (SelectedContainer?.Id == targetId)
                {
                    ContainerId = string.Empty;
                    SelectedContainer = null;
                }
                await LoadContainersAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка удаления: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PruneContainersAsync()
    {
        try
        {
            StatusMessage = "Очистка остановленных контейнеров...";

            HttpResponseMessage response = await _httpClient.PostAsync("api/servers/prune", null);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Очистка завершена успешно.";
                await LoadContainersAsync();
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                StatusMessage = $"Ошибка очистки: {response.StatusCode}\n{error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подключения к бэкенду: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsContainerSelected))]
    private async Task GetStatsAsync()
    {
        try
        {
            string targetId = SelectedContainer!.Id;

            StatusMessage = "Запрос статистики...";

            DockerContainerStats? stats = await _httpClient.GetFromJsonAsync<DockerContainerStats>($"api/servers/{targetId}/stats");

            if (stats is not null)
            {
                double memoryMb = stats.MemoryBytes / (1024.0 * 1024.0);

                StatusMessage = $"Мониторинг [ID: {targetId[..Math.Min(8, targetId.Length)]}]:\n" +
                                $"CPU: {stats.CpuPercentage:F2}%\n" +
                                $"RAM: {memoryMb:F2} MB";
            }
            else
            {
                StatusMessage = "Не удалось получить статистику.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка получения статистики: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GetStatsForContainerAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedStatsContainer;
        if (target is null)
        {
            StatsCpuUsage = "0.00%";
            StatsMemoryUsage = "0.00 MB";
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Запрос статистики...";

            DockerContainerStats? stats = await _httpClient.GetFromJsonAsync<DockerContainerStats>($"api/servers/{targetId}/stats");

            if (stats is not null)
            {
                double memoryMb = stats.MemoryBytes / (1024.0 * 1024.0);
                StatsCpuUsage = $"{stats.CpuPercentage:F2}%";
                StatsMemoryUsage = $"{memoryMb:F2} MB";
                StatusMessage = "Статистика успешно получена.";
            }
            else
            {
                StatsCpuUsage = "0.00%";
                StatsMemoryUsage = "0.00 MB";
                StatusMessage = "Не удалось получить статистику.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка получения статистики: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GetLogsForContainerAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedLogContainer;
        if (target is null)
        {
            ContainerLogs = "Выберите контейнер для просмотра логов.";
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Загрузка логов...";

            string logs = await _httpClient.GetStringAsync($"api/servers/{targetId}/logs");

            ContainerLogs = string.IsNullOrWhiteSpace(logs) ? "Логи пусты." : logs;
            StatusMessage = "Логи успешно получены.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка получения логов: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GetLogsAsync(ContainerModel? container = null)
    {
        ContainerModel? target = container ?? SelectedContainer;
        if (target is null)
        {
            return;
        }

        try
        {
            string targetId = target.Id;

            StatusMessage = "Загрузка логов...";

            string logs = await _httpClient.GetStringAsync($"api/servers/{targetId}/logs");

            ContainerLogs = string.IsNullOrWhiteSpace(logs) ? "Логи пусты." : logs;
            StatusMessage = "Логи успешно получены.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка получения логов: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsContainerSelected))]
    private async Task ExecuteCommandAsync()
    {
        string targetId = SelectedContainer!.Id;

        if (string.IsNullOrWhiteSpace(CommandInput))
            return;

        string cmd = CommandInput;
        CommandInput = string.Empty;

        TerminalOutput += $"\n> {cmd}\n";

        try
        {
            Dictionary<string, string> requestData = new Dictionary<string, string> { { "command", cmd } };
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"api/servers/{targetId}/exec", requestData);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                TerminalOutput += result;
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                TerminalOutput += $"Ошибка выполнения: {response.StatusCode} - {error}\n";
            }
        }
        catch (Exception ex)
        {
            TerminalOutput += $"Ошибка подключения к бэкенду: {ex.Message}\n";
        }
    }
}