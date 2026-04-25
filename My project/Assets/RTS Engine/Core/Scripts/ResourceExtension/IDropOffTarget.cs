using RTSEngine.UnitExtension;

namespace RTSEngine.ResourceExtension
{
    public interface IDropOffTarget : IAddableUnit
    {
        bool CanDropResourceType(ResourceTypeInfo resourceType);
    }
}
