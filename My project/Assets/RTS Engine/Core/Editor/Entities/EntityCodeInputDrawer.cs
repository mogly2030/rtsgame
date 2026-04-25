using RTSEngine.Entities;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly.Entities
{
    public class EntityCodeStringDrawerData : StringDrawerData<IEntity>
    {
        public override bool AllowMultipleRepresentatives => false;
        public override string MultipleRepresentativesError => $"Entity code has been defined for another entity!";

        public override bool CanFetchRepresentative()
            => RTSEditorHelper.GetEntities() != null;

        public override string RepresentativeFetchError => "Can not fetch the entities placed under the '*/Resources/Prefabs/' path.";

        public override void GetRepresentative(SerializedProperty property, string value,
            out IEntity representative, out int count)
        {
            representative = null;
            IEntity sourceEntity = (property.serializedObject.targetObject as IEntity);
            // Currently, the representative is not recongized as the same entity prefab where the code is being defined
            if (GetSuggestionExceptions().Contains(value)
                || (RTSEditorHelper.GetEntities().TryGetValue(value, out representative)
                    && (!sourceEntity.IsValid() || representative.gameObject != sourceEntity.gameObject)))
            {
                count = 1;
                return;
            }

            count = 0;
        }

        public override string GetRepresentativeInfo(IEntity entity)
            => $"Entity code: '{entity.Code}' is defined for a valid entity:\n(Name: '{entity.Name}', Category: '{string.Join(", ", entity.Category)}'), Radius: '{entity.Radius}'.";
        public override void DrawRepresentative(IEntity representative, ref Rect rect)
        {
            EditorGUI.ObjectField(rect, representative.gameObject, typeof(IEntity), false);
        }

        public override IReadOnlyList<string> GetSuggestionExceptions() => new string[] { 
            "new_unit",
            "new_building",
            "new_resource"
        };

        public override IReadOnlyList<string> GetAllSuggestions()
        {
            return RTSEditorHelper.GetEntities().Select(entity => entity.Key).ToArray();
        }

        public override string GetStringNoTargetErrorMessage(string value)
            => $"Entity code: '{value}' has not been defined for any entity prefab that exists under the '*/Resources/Prefabs' path.";

    }


    [CustomPropertyDrawer(typeof(EntityCodeInputAttribute))]
    public class EntityCodeInputDrawer : PropertyDrawer
    {
        private Dictionary<string, EntityCodeStringDrawerData> stringDataDic = new Dictionary<string, EntityCodeStringDrawerData>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!stringDataDic.TryGetValue(property.propertyPath, out EntityCodeStringDrawerData codeStringData))
            {
                codeStringData = new EntityCodeStringDrawerData();
                stringDataDic.Add(property.propertyPath, codeStringData);
            }

            EntityCodeInputAttribute customAttribute = attribute as EntityCodeInputAttribute;
            string attributeName = customAttribute.GetType().Name;

            if (customAttribute.IsDefiner)
                PropertyDrawerHelper.OnSingleStringDefinerDrawer(position, property, label, attributeName, codeStringData);
            else
                PropertyDrawerHelper.OnSingleStringSelectorDrawer(position, property, label, attributeName, codeStringData);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!stringDataDic.TryGetValue(property.propertyPath, out EntityCodeStringDrawerData viewData))
                stringDataDic.Add(property.propertyPath, new EntityCodeStringDrawerData());

            return stringDataDic[property.propertyPath].fieldsAmount * (base.GetPropertyHeight(property, label) + EditorGUIUtility.standardVerticalSpacing * 1.5f);
        }
    }
}
