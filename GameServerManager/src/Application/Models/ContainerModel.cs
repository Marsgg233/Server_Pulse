using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameServerManager.Application.Models;

public sealed class ContainerModel : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _image = string.Empty;
    private string _state = string.Empty;
    private string _status = string.Empty;
    private string _names = string.Empty;
    private string _ports = string.Empty;
    private bool _isSelected;

    public string Id
    {
        get => _id;
        init => _id = value;
    }

    public string Image
    {
        get => _image;
        set => SetField(ref _image, value);
    }

    public string State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsStopped));
                OnPropertyChanged(nameof(PortColor));
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string Names
    {
        get => _names;
        set => SetField(ref _names, value);
    }

    public string Ports
    {
        get => _ports;
        set => SetField(ref _ports, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsRunning => State?.Equals("running", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsStopped => !IsRunning;
    public string PortColor => IsRunning ? "#3B82F6" : "#E5E7EB";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
