using RTSEngine.EntityComponent;

namespace RTSEngine.Event
{
    public struct SetTargetInputDataEventArgs
    {
        public SetTargetInputData Data { get; }

        public SetTargetInputDataEventArgs (SetTargetInputData data)
        {
            this.Data = data;
        }
    }
}
