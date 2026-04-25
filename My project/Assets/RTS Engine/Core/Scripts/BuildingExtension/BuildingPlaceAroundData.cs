using RTSEngine.Entities;
using RTSEngine.EntityComponent;
using UnityEngine;

namespace RTSEngine.BuildingExtension
{
    [System.Serializable]
    public struct BuildingPlaceAroundData
    {
        [Tooltip("Define the type of entities that the building can be placed around, type can be defined using the entities' codes or categories.")]
        public CodeCategoryField entityType;
        [SerializeField, Tooltip("How far can the building be from the entity it is supposed to be placed around?")]
        public FloatRange range;

        public ErrorMessage IsValidType (SetTargetInputData data)
        {
            return data.target.instance.IsValid() && entityType.Contains(data.target.instance.Code, data.target.instance.Category) ? ErrorMessage.none : ErrorMessage.invalid;
        }
    }
}
