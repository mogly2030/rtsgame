namespace RTSEngine.Game
{
    public interface IPreRunGamePriorityService : IPreRunGameService 
    {
        int ServicePriority { get; }
    }
}
