using CommunityToolkit.Mvvm.Input;
using ProxyServer.GUI.Models;

namespace ProxyServer.GUI.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}