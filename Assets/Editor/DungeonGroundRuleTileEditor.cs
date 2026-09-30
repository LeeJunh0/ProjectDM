using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM.Editor
{
    /// <summary>Inspector authoring tool for DungeonGroundRuleTile's 3x3 neighbour rules.</summary>
    [CustomEditor(typeof(DungeonGroundRuleTile))]
    public sealed class DungeonGroundRuleTileEditor : UnityEditor.Editor
    {
        private const int GridWidth = 3;
        private SerializedProperty customRules;
        private SerializedProperty ruleTiles;

        private void OnEnable()
        {
            customRules = serializedObject.FindProperty("customRules");
            ruleTiles = serializedObject.FindProperty("ruleTiles");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "규칙마다 3×3 이웃 조건과 결과 타일을 지정합니다. 0은 무관, 1은 같은 룰타일, X는 다른 타일 또는 빈 칸입니다. 위 규칙이 먼저 적용됩니다.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("규칙 추가"))
            {
                AddRule("New Rule");
            }

            if (GUILayout.Button("기본 외곽 규칙 8개 추가"))
            {
                AddDefaultEdgeRules();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            for (int index = 0; index < customRules.arraySize; index++)
            {
                DrawRule(index);
            }

            EditorGUILayout.Space(8f);
            DrawQuickPalette("3×3 룰타일 팔레트", ruleTiles);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRule(int index)
        {
            SerializedProperty rule = customRules.GetArrayElementAtIndex(index);
            SerializedProperty displayName = rule.FindPropertyRelative("displayName");
            SerializedProperty conditions = rule.FindPropertyRelative("conditions");
            SerializedProperty outputTile = rule.FindPropertyRelative("outputTile");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"규칙 {index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("위로", GUILayout.Width(42f)) && index > 0)
            {
                customRules.MoveArrayElement(index, index - 1);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("아래로", GUILayout.Width(42f)) && index < customRules.arraySize - 1)
            {
                customRules.MoveArrayElement(index, index + 1);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("삭제", GUILayout.Width(42f)))
            {
                customRules.DeleteArrayElementAtIndex(index);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(displayName, new GUIContent("이름"));
            EnsureConditionGrid(conditions);
            DrawConditionGrid(conditions);
            EditorGUILayout.PropertyField(outputTile, new GUIContent("결과 타일"));
            DrawPaletteAssignment(outputTile);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawConditionGrid(SerializedProperty conditions)
        {
            EditorGUILayout.LabelField("이웃 조건", EditorStyles.miniBoldLabel);
            for (int row = 0; row < GridWidth; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int column = 0; column < GridWidth; column++)
                {
                    int conditionIndex = row * GridWidth + column;
                    if (conditionIndex == 4)
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            GUILayout.Button("●", GUILayout.Width(38f), GUILayout.Height(28f));
                        }
                        continue;
                    }

                    SerializedProperty condition = conditions.GetArrayElementAtIndex(conditionIndex);
                    DungeonGroundRuleTile.NeighborCondition value = (DungeonGroundRuleTile.NeighborCondition)condition.enumValueIndex;
                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = ConditionColor(value);
                    if (GUILayout.Button(ConditionLabel(value), GUILayout.Width(38f), GUILayout.Height(28f)))
                    {
                        condition.enumValueIndex = ((int)value + 1) % 3;
                    }
                    GUI.backgroundColor = previousColor;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPaletteAssignment(SerializedProperty outputTile)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("빠른 지정");
            DrawPaletteButtons(outputTile, ruleTiles);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPaletteButtons(SerializedProperty outputTile, SerializedProperty palette)
        {
            if (palette == null || !palette.isArray)
            {
                return;
            }

            for (int index = 0; index < palette.arraySize; index++)
            {
                Object tile = palette.GetArrayElementAtIndex(index).objectReferenceValue;
                if (tile == null)
                {
                    continue;
                }

                GUIContent content = new GUIContent(AssetPreview.GetMiniThumbnail(tile), tile.name);
                if (GUILayout.Button(content, GUILayout.Width(24f), GUILayout.Height(20f)))
                {
                    outputTile.objectReferenceValue = tile;
                }
            }
        }

        private static void DrawQuickPalette(string label, SerializedProperty palette)
        {
            if (palette == null || !palette.isArray)
            {
                return;
            }

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int row = 0; row < GridWidth; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < GridWidth; column++)
                {
                    int index = row * GridWidth + column;
                    if (index < palette.arraySize)
                    {
                        EditorGUILayout.ObjectField(palette.GetArrayElementAtIndex(index), GUIContent.none, GUILayout.Width(76f));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddDefaultEdgeRules()
        {
            AddEdgeRule("Top Left", 0, 0, 1, 2, 3, 2);
            AddEdgeRule("Top", 0, 1, 1, 2, 3, 1, 5, 1);
            AddEdgeRule("Top Right", 0, 2, 1, 2, 5, 2);
            AddEdgeRule("Left", 1, 0, 3, 2, 1, 1, 7, 1);
            AddEdgeRule("Right", 1, 2, 5, 2, 3, 1, 7, 1);
            AddEdgeRule("Bottom Left", 2, 0, 7, 2, 3, 2);
            AddEdgeRule("Bottom", 2, 1, 7, 2, 3, 1, 5, 1);
            AddEdgeRule("Bottom Right", 2, 2, 7, 2, 5, 2);
        }

        private void AddEdgeRule(string name, int borderRow, int borderColumn, params int[] conditions)
        {
            int ruleIndex = AddRule(name);
            SerializedProperty rule = customRules.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty ruleConditions = rule.FindPropertyRelative("conditions");
            for (int index = 0; index < conditions.Length; index += 2)
            {
                ruleConditions.GetArrayElementAtIndex(conditions[index]).enumValueIndex = conditions[index + 1];
            }

            int paletteIndex = borderRow * GridWidth + borderColumn;
            if (paletteIndex < ruleTiles.arraySize)
            {
                rule.FindPropertyRelative("outputTile").objectReferenceValue = ruleTiles.GetArrayElementAtIndex(paletteIndex).objectReferenceValue;
            }
        }

        private int AddRule(string name)
        {
            int index = customRules.arraySize;
            customRules.arraySize++;
            SerializedProperty rule = customRules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative("displayName").stringValue = name;
            SerializedProperty conditions = rule.FindPropertyRelative("conditions");
            conditions.arraySize = 9;
            for (int conditionIndex = 0; conditionIndex < conditions.arraySize; conditionIndex++)
            {
                conditions.GetArrayElementAtIndex(conditionIndex).enumValueIndex = (int)DungeonGroundRuleTile.NeighborCondition.Any;
            }

            rule.FindPropertyRelative("outputTile").objectReferenceValue = null;
            return index;
        }

        private static void EnsureConditionGrid(SerializedProperty conditions)
        {
            if (conditions.arraySize != 9)
            {
                conditions.arraySize = 9;
            }
        }

        private static string ConditionLabel(DungeonGroundRuleTile.NeighborCondition condition)
        {
            return condition switch
            {
                DungeonGroundRuleTile.NeighborCondition.This => "1",
                DungeonGroundRuleTile.NeighborCondition.NotThis => "X",
                _ => "0"
            };
        }

        private static Color ConditionColor(DungeonGroundRuleTile.NeighborCondition condition)
        {
            return condition switch
            {
                DungeonGroundRuleTile.NeighborCondition.This => new Color(.45f, .8f, .54f),
                DungeonGroundRuleTile.NeighborCondition.NotThis => new Color(.9f, .54f, .45f),
                _ => new Color(.72f, .72f, .72f)
            };
        }
    }
}
