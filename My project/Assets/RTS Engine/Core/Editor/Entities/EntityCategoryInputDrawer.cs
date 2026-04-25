using RTSEngine.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly.Entities
{
    public class EntityCategoryStringDrawerData : StringDrawerData<IReadOnlyList<IEntity>>
    {
        public override bool AllowMultipleRepresentatives => true;

        public override bool CanFetchRepresentative()
            => RTSEditorHelper.GetEntityPrefabsPerCategory() != null;

        public override string RepresentativeFetchError => "Can not fetch the entities placed under the '*/Resources/Prefabs/' path.";

        public override void GetRepresentative(SerializedProperty property, string value,
            out IReadOnlyList<IEntity> representative, out int count)
        {
            if(!RTSEditorHelper.GetEntityPrefabsPerCategory().TryGetValue(value, out IEnumerable<IEntity> entities))
            {
                RTSEditorHelper.FetchEntityPrefabs();
                RTSEditorHelper.GetEntityPrefabsPerCategory().TryGetValue(value, out entities);
            }

            representative = entities != null ? entities.ToArray() : new IEntity[0];
            count = representative.Count;
        }

        public override void DrawRepresentative(IReadOnlyList<IEntity> representatives, ref Rect rect)
        {
            for (int i = 0; i < representatives.Count; i++)
            {
                rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.ObjectField(rect, representatives[i].gameObject, typeof(IEntity), false);
            }
        }

        public override IReadOnlyList<string> GetSuggestionExceptions() => new string[] { 
            "new_unit_category",
            "new_building_category",
            "new_resource_category"
        };

        public override IReadOnlyList<string> GetAllSuggestions()
        {
            return RTSEditorHelper.GetEntityPrefabsPerCategory().Keys.ToArray();
        }

        public override string GetStringNoTargetErrorMessage(string value)
            => $"Entity category: '{value}' has not been defined for any entity prefab that exists under the '.../Resources/Prefabs' path.";
    }

    [CustomPropertyDrawer(typeof(EntityCategoryInputAttribute))]
    public class EntityCategoryInputDrawer : PropertyDrawer
    {
        private Dictionary<string, EntityCategoryStringDrawerData> stringDataDic = new Dictionary<string, EntityCategoryStringDrawerData>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!stringDataDic.TryGetValue(property.propertyPath, out EntityCategoryStringDrawerData categoryStringData))
            {
                categoryStringData = new EntityCategoryStringDrawerData();
                stringDataDic.Add(property.propertyPath, categoryStringData);
            }

            EntityCategoryInputAttribute customAttribute = attribute as EntityCategoryInputAttribute;
            string attributeName = customAttribute.GetType().Name;

            if (customAttribute.IsDefiner)
                PropertyDrawerHelper.OnMultiStringDefinerDrawer(position, property, label, attributeName, categoryStringData);
            else
                PropertyDrawerHelper.OnMultiStringSelectorDrawer(position, property, label, attributeName, categoryStringData);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!stringDataDic.TryGetValue(property.propertyPath, out EntityCategoryStringDrawerData viewData))
                stringDataDic.Add(property.propertyPath, new EntityCategoryStringDrawerData());

            return stringDataDic[property.propertyPath].fieldsAmount * (base.GetPropertyHeight(property, label) + EditorGUIUtility.standardVerticalSpacing * 1.5f);
        }
    }
}
