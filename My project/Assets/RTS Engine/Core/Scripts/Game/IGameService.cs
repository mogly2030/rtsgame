using RTSEngine.Service;

namespace RTSEngine.Game
{
    // Empty interface that used on game services to note that they are not required
    // So that when GetService() fails to load them, no error would be triggered
    public interface IOptionalGameService { }

    public interface IGameService : IServiceComponent<IGameManager>, IMonoBehaviour { }
}