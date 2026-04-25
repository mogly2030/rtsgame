using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

namespace RTSEngine.EditorOnly
{
    public abstract class StringDrawerData<T>
    {
        // General
        public int fieldsAmount = 2;

        // Definer
        public int definerIndex = 0;

        // Selection
        public bool searchFoldout = false;

        // Representatives
        public bool showRepresentatives = false;
        public abstract bool AllowMultipleRepresentatives { get; }
        public virtual string MultipleRepresentativesError => String.Empty;
        public abstract bool CanFetchRepresentative();
        public abstract string RepresentativeFetchError { get; }
        public abstract void GetRepresentative(SerializedProperty property, string value, out T representative, out int count);
        public virtual string GetRepresentativeInfo(T representative) => String.Empty;
        public abstract void DrawRepresentative(T representative, ref Rect rect);

        // Suggestions
        public abstract IReadOnlyList<string> GetSuggestionExceptions();
        public abstract IReadOnlyList<string> GetAllSuggestions();
        public abstract string GetStringNoTargetErrorMessage(string value);
    }

    public static class PropertyDrawerHelper
    {
        private static bool OnStringPreDrawer<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData,
            out float propertyHeight, out Rect nextPropertyRect)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            propertyHeight = position.height - EditorGUIUtility.standardVerticalSpacing * stringData.fieldsAmount * 1.5f;
            propertyHeight /= stringData.fieldsAmount;

            nextPropertyRect = new Rect(position.x, position.y, position.width, propertyHeight);

            if (property.propertyType != SerializedPropertyType.String)
            {
                stringData.fieldsAmount = 2;

                nextPropertyRect.height = propertyHeight * 2.0f;

                EditorGUI.HelpBox(nextPropertyRect, $"Use [{attributeName}] with string fields!", MessageType.Error);
                EditorGUI.EndProperty();
                return false; 
            }

            EditorGUI.PropertyField(nextPropertyRect, property, label);

            if (!stringData.CanFetchRepresentative())
            {
                stringData.fieldsAmount = 3;
                nextPropertyRect.y += nextPropertyRect.height + EditorGUIUtility.standardVerticalSpacing;
                nextPropertyRect.height = propertyHeight * 2.0f;

                EditorGUI.HelpBox(nextPropertyRect, stringData.RepresentativeFetchError, MessageType.Error);
                EditorGUI.EndProperty();
                return false;
            }

            nextPropertyRect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;

            return true;
        }

        private static void OnStringDrawerSuggestions<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData,
            string currStringValue, float propertyHeight, ref Rect nextPropertyRect)
        {
            string[] results = RTSEditorHelper.GetMatchingStrings(currStringValue, stringData.GetAllSuggestions(), stringData.GetSuggestionExceptions());

            stringData.searchFoldout = EditorGUI.Foldout(nextPropertyRect, stringData.searchFoldout, $"Suggestions: {results.Length}");

            if (stringData.searchFoldout)
            {
                stringData.fieldsAmount = 4 + results.Length;

                EditorGUI.indentLevel++;

                foreach (string result in results)
                {
                    nextPropertyRect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
                    if (GUI.Button(nextPropertyRect, result))
                        property.stringValue = result;
                }

                EditorGUI.indentLevel--;
            }
            else
                stringData.fieldsAmount = 4;

            nextPropertyRect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            nextPropertyRect.height = propertyHeight * 2.0f;
            EditorGUI.HelpBox(
                nextPropertyRect,
                string.IsNullOrEmpty(currStringValue) ? "Please input the string in the appropriate field or choose one from the suggestions!" : stringData.GetStringNoTargetErrorMessage(currStringValue),
                MessageType.Error);
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        public static void OnSingleStringDefinerDrawer<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData)
        {
            if (!OnStringPreDrawer(position, property, label, attributeName, stringData,
                out float propertyHeight, out Rect nextPropertyRect))
                return;

            string nextStringValue = property.stringValue;

            stringData.GetRepresentative(property, nextStringValue, out T representative, out _);

            // Currently, the representative is not recongized as the same entity prefab where the code is being defined
            /*
            if(representative.IsValid() && !stringData.AllowMultipleRepresentatives)
            {
                stringData.fieldsAmount = 2;
                EditorGUI.HelpBox(nextPropertyRect, stringData.MultipleRepresentativesError, MessageType.Error);
            }
            else
            {
                stringData.fieldsAmount = 1;
            }
            */

            stringData.fieldsAmount = 1;

            EditorGUI.EndProperty();
        }

        public static void OnSingleStringSelectorDrawer<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData)
        {
            if (!OnStringPreDrawer(position, property, label, attributeName, stringData,
                out float propertyHeight, out Rect nextPropertyRect))
                return;

            stringData.GetRepresentative(property, property.stringValue, out T representative, out _);

            if(!representative.IsValid())
            {
                OnStringDrawerSuggestions(position, property, label, attributeName, stringData,
                    property.stringValue, propertyHeight, ref nextPropertyRect);
                return;
            }

            nextPropertyRect.height = propertyHeight * 2;
            stringData.fieldsAmount = 4;
            EditorGUI.HelpBox(nextPropertyRect, stringData.GetRepresentativeInfo(representative), MessageType.Info);

            GUI.enabled = false;
            nextPropertyRect.height = propertyHeight;
            nextPropertyRect.y += (propertyHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
            stringData.DrawRepresentative(representative, ref nextPropertyRect);
            GUI.enabled = true;

            EditorGUI.EndProperty();
        }

        public static void OnMultiStringDefinerDrawer<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData)
        {
            if (!OnStringPreDrawer(position, property, label, attributeName, stringData,
                out float propertyHeight, out Rect nextPropertyRect))
                return;

            string[] stringValues = property.stringValue.Split(',');
            string nextStringValue = property.stringValue;

            if (stringData.definerIndex.IsValidIndex(stringValues))
                nextStringValue = stringValues[stringData.definerIndex];
            else
                stringData.definerIndex = 0;

            EditorGUI.indentLevel++;

            stringData.GetRepresentative(property, nextStringValue, out T representatives, out int representativesCount);

            float lastHeight = nextPropertyRect.height;
            nextPropertyRect.height += propertyHeight;

            stringData.definerIndex = EditorGUI.Popup(
                new Rect(nextPropertyRect.x, nextPropertyRect.y, nextPropertyRect.width, nextPropertyRect.height / 2),
                $"Values:", stringData.definerIndex, stringValues);

            nextPropertyRect.y += nextPropertyRect.height * 0.5f + EditorGUIUtility.standardVerticalSpacing;
            nextPropertyRect.height = lastHeight;

            stringData.showRepresentatives = EditorGUI.Foldout(nextPropertyRect, stringData.showRepresentatives, new GUIContent($"Display '{nextStringValue}' representatives (Count: {representativesCount})"));

            if (stringData.showRepresentatives)
            {
                stringData.fieldsAmount = 3 + representativesCount;

                EditorGUI.indentLevel++;
                GUI.enabled = false;

                stringData.DrawRepresentative(representatives, ref nextPropertyRect);

                GUI.enabled = true;
                EditorGUI.indentLevel--;
            }
            else
                stringData.fieldsAmount = 3;

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();

        }

        public static void OnMultiStringSelectorDrawer<T>(Rect position, SerializedProperty property, GUIContent label, string attributeName, StringDrawerData<T> stringData)
        {
            if (!OnStringPreDrawer(position, property, label, attributeName, stringData,
                out float propertyHeight, out Rect nextPropertyRect))
                return;

            string nextStringValue = property.stringValue;

            EditorGUI.indentLevel++;

            stringData.GetRepresentative(property, nextStringValue, out T representatives, out int representativesCount);

            if(representativesCount == 0)
            {
                OnStringDrawerSuggestions(position, property, label, attributeName, stringData,
                    nextStringValue, propertyHeight, ref nextPropertyRect);
                return;
            }

            float lastHeight = nextPropertyRect.height;
            nextPropertyRect.height += propertyHeight;

            nextPropertyRect.y += EditorGUIUtility.standardVerticalSpacing;
            nextPropertyRect.height = lastHeight;

            stringData.showRepresentatives = EditorGUI.Foldout(nextPropertyRect, stringData.showRepresentatives, new GUIContent($"Display '{nextStringValue}' representatives (Count: {representativesCount})"));

            if (stringData.showRepresentatives)
            {
                stringData.fieldsAmount = 2 + representativesCount;

                EditorGUI.indentLevel++;
                GUI.enabled = false;

                stringData.DrawRepresentative(representatives, ref nextPropertyRect);

                GUI.enabled = true;
                EditorGUI.indentLevel--;
            }
            else
                stringData.fieldsAmount = 2;

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();

        }
    }
}
