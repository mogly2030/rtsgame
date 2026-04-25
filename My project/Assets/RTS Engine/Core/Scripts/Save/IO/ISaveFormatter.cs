using RTSEngine.Game;

namespace RTSEngine.Save.IO
{
    public interface ISaveFormatter : IPreRunGameService, IOptionalGameService
    {
        string ToSaveFormat<T>(T input);
        T FromSaveFormat<T>(string input);
    }
}