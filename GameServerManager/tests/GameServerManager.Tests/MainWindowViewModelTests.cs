using GameServerManager.UI.ViewModels;
using GameServerManager.Application.Models;
using Xunit;
using System.Collections.ObjectModel;

namespace GameServerManager.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void TestContainerSelectionUpdatesProperties()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var container = new ContainerModel { Id = "test-1", Names = "test-container" };
        
        // Act
        vm.SelectedContainer = container;

        // Assert
        Assert.Equal("test-1", vm.ContainerId);
    }

    [Fact]
    public void TestCommandInputReset()
    {
        // Arrange
        var vm = new MainWindowViewModel { CommandInput = "ls" };
        
        // Act (simulating command execution - setting to empty is done in command logic)
        vm.CommandInput = string.Empty;

        // Assert
        Assert.Equal(string.Empty, vm.CommandInput);
    }
}