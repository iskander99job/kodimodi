using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class LeftPanelDrawer
    {
        private const float ButtonWidth = 70f;
        private const float VariantButtonWidth = 80f;
        private const string SavePath = "Assets/ithappy/Sci-Fi_Weapons/Saved_Weapons";

        private readonly GunCustomizationModel _model;
        private readonly GunCustomizationWindow _window;
        private readonly Dictionary<GunType, bool> _randomTypeToggles;

        private GUIStyle _headerStyle;
        private string _saveName = "";
        private Vector2 _scrollPos;

        public LeftPanelDrawer(GunCustomizationModel model, GunCustomizationWindow window)
        {
            _model = model;
            _window = window;
            _randomTypeToggles = Enum.GetValues(typeof(GunType)).Cast<GunType>().ToDictionary(t => t, t => true);
        }

        // Built on first draw, not in the constructor: this drawer is created from the window's
        // OnEnable, which also runs on assembly reload, and EditorStyles is null outside the GUI loop.
        private GUIStyle HeaderStyle =>
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft };

        public void Draw()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(410)))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPreviewArea();
                GUILayout.Space(5);
                DrawGunTypeSelector();
                GUILayout.Space(5);
                DrawGunVariantSelector();
                GUILayout.Space(5);
                DrawSaveAndRandomizeSection();
            }
        }

        private void DrawPreviewArea()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var rect = GUILayoutUtility.GetRect(400, 400, GUILayout.Width(400), GUILayout.Height(400));
                _model.PreviewManager.RenderPreview(rect);
                _model.PreviewManager.HandleInput(rect, Event.current, _window.Repaint);

                if (!_model.PreviewManager.IsViewDefault)
                {
                    var buttonRect = new Rect(rect.xMax - 28, rect.yMax - 28, 24, 24);
                    var originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1, 1, 1, 0.7f);
                    if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("Refresh"), GUIStyle.none))
                    {
                        _model.PreviewManager.ResetView();
                        _window.Repaint();
                    }

                    GUI.backgroundColor = originalColor;
                }
            }
        }

        private void DrawGunTypeSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Gun Type", HeaderStyle);
                var types = Enum.GetValues(typeof(GunType)).Cast<GunType>().ToList();
                var half = (types.Count + 1) / 2;
                DrawButtonRow(types.Take(half).ToList());
                DrawButtonRow(types.Skip(half).ToList());
            }
        }

        private void DrawButtonRow(List<GunType> types)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (var i = 0; i < types.Count; i++)
                {
                    if (i > 0) GUILayout.Space(0);

                    if (GUILayout.Toggle(_model.SelectedType == types[i], types[i].ToString(), "Button", GUILayout.Width(ButtonWidth), GUILayout.Height(25)))
                    {
                        if (_model.SelectedType != types[i])
                        {
                            _model.SelectedType = types[i];
                            _model.FilterGuns();
                        }
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGunVariantSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(100)))
            {
                GUILayout.Label("Gun Variant", HeaderStyle);
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos, GUILayout.Height(85)))
                {
                    _scrollPos = scroll.scrollPosition;
                    if (_model.FilteredGuns.Count == 0) return;

                    var total = _model.FilteredGuns.Count;
                    const int rows = 3;
                    var baseCount = total / rows;
                    var remainder = total % rows;
                    var start = 0;

                    for (var r = 0; r < rows; r++)
                    {
                        var count = baseCount + (r < remainder ? 1 : 0);
                        if (count == 0) continue;

                        var rowItems = _model.FilteredGuns.Skip(start).Take(count).ToList();
                        DrawVariantRow(rowItems);
                        start += count;
                    }
                }
            }
        }

        private void DrawVariantRow(List<GunEntry> items)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0) GUILayout.Space(2);

                    var selected = items[i] == _model.FilteredGuns[_model.GunIndex];
                    if (GUILayout.Toggle(selected, items[i].name.Replace("GunEntry_", ""), "Button", GUILayout.Width(VariantButtonWidth), GUILayout.Height(25)))
                    {
                        if (!selected)
                        {
                            _model.GunIndex = _model.FilteredGuns.IndexOf(items[i]);
                            _model.SelectGun(items[i], skipHistory: true);
                        }
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSaveAndRandomizeSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Prefab Folder:", HeaderStyle);
                EditorGUILayout.LabelField(SavePath, EditorStyles.boldLabel);
                GUILayout.Space(5);
                GUILayout.Label("Name:", HeaderStyle);
                _saveName = EditorGUILayout.TextField(_saveName);
                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Include in Randomize:", HeaderStyle);
                    DrawRandomTypeToggles();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Skins:", HeaderStyle);
                    DrawSkinToggles();
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Save Prefab", GUILayout.Height(30)))
                {
                    _model.SavePrefab(_saveName, SavePath);
                }

                if (GUILayout.Button("Randomize", GUILayout.Height(30)))
                {
                    _model.RandomizeAll(_randomTypeToggles);
                    _window.Repaint();
                }

                GUI.enabled = _model.HistoryManager.CanUndo;
                if (GUILayout.Button("Last", GUILayout.Height(30)))
                {
                    var state = _model.HistoryManager.Undo();
                    if (state != null)
                    {
                        _model.RestoreState(state);
                        _window.Repaint();
                    }
                }

                GUI.enabled = true;
            }
        }

        private void DrawRandomTypeToggles()
        {
            var types = Enum.GetValues(typeof(GunType)).Cast<GunType>().ToList();
            var half = (types.Count + 1) / 2;
            DrawToggleRow(types.Take(half).ToList());
            DrawToggleRow(types.Skip(half).ToList());
        }

        private void DrawToggleRow(List<GunType> types)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var type in types)
                {
                    var current = _randomTypeToggles[type];
                    var activeCount = _randomTypeToggles.Count(kv => kv.Value);
                    var canDisable = !(activeCount == 1 && current);

                    EditorGUI.BeginDisabledGroup(!canDisable);
                    var newVal = EditorGUILayout.ToggleLeft(type.ToString(), current, GUILayout.Width(85));
                    EditorGUI.EndDisabledGroup();

                    if (newVal != current) _randomTypeToggles[type] = newVal;
                    GUILayout.Space(5);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSkinToggles()
        {
            if (_model.Skins.Count == 0) return;

            while (_model.SkinEnabled.Count < _model.Skins.Count)
                _model.SkinEnabled.Add(true);
            while (_model.SkinEnabled.Count > _model.Skins.Count)
                _model.SkinEnabled.RemoveAt(_model.SkinEnabled.Count - 1);

            var skinNames = _model.Skins.Select((skin, idx) =>
            {
                if (!string.IsNullOrEmpty(skin.DisplayName))
                    return skin.DisplayName;
                var name = skin.name;
                if (name.StartsWith("ColorSet_", StringComparison.OrdinalIgnoreCase))
                {
                    var numPart = name.Substring("ColorSet_".Length);
                    if (int.TryParse(numPart, out var num))
                        return num.ToString();
                    return numPart;
                }

                return name;
            }).ToList();

            var total = skinNames.Count;
            var half = (total + 1) / 2;

            DrawSkinToggleRow(skinNames, 0, half);
            DrawSkinToggleRow(skinNames, half, total);
        }

        private void DrawSkinToggleRow(List<string> names, int start, int end)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = start; i < end; i++)
                {
                    var current = _model.SkinEnabled[i];
                    var activeCount = _model.SkinEnabled.Count(e => e);
                    var canDisable = !(activeCount == 1 && current);

                    EditorGUI.BeginDisabledGroup(!canDisable);
                    var newVal = EditorGUILayout.ToggleLeft(names[i], current, GUILayout.Width(68));
                    EditorGUI.EndDisabledGroup();

                    if (newVal != current)
                        _model.SkinEnabled[i] = newVal;

                    GUILayout.Space(5);
                }

                GUILayout.FlexibleSpace();
            }
        }
    }
}
