using RTSEngine.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly.Game
{
    [CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : EditorBase<GameManager>
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // If there is an initial faction entity assigned but no initial cam look at position
            // set that one initial faction entity to the cam initial look at position
            SO.Update();
            for(int i = 0; i < SO.FindProperty("factionSlots").arraySize; i++)
            {
                var slotProp = SO.FindProperty("factionSlots").GetArrayElementAtIndex(i);
                var lookAtPositionProp = slotProp.FindPropertyRelative("initialCamLookAtPosition");
                var firstInitialFactionEntity = slotProp.FindPropertyRelative("initialFactionEntities.allTypes").arraySize > 0
                    ? slotProp.FindPropertyRelative("initialFactionEntities.allTypes").GetArrayElementAtIndex(0).objectReferenceValue
                    : null;

                if(!lookAtPositionProp.objectReferenceValue.IsValid()
                    && firstInitialFactionEntity.IsValid())
                {
                    lookAtPositionProp.objectReferenceValue = firstInitialFactionEntity;
                }
            }
            SO.ApplyModifiedProperties();
        }
    }
}
