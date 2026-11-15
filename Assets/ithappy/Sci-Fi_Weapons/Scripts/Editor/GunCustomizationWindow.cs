using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class GunCustomizationWindow : EditorWindow
    {
        private const string GunEntriesPath = "Assets/ithappy/Sci-Fi_Weapons/Configs";

        private GunCustomizationModel _model;
        private LeftPanelDrawer _leftDrawer;
        private AttachmentCardDrawer _cardDrawer;
        private Vector2 _scrollPosRight;

        [MenuItem("Tools/ithappy/Sci-Fi_Weapons/Sci-Fi Weapons Customization %#&e", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<GunCustomizationWindow>("Gun Customization Tool");
            window.minSize = new Vector2(1250, 945);
            window.maxSize = new Vector2(1250, 945);
            window.Show();
        }

        private void OnEnable()
        {
            _model = new GunCustomizationModel();
            _leftDrawer = new LeftPanelDrawer(_model, this);
            _cardDrawer = new AttachmentCardDrawer(this);

            AttachmentIconRenderer.IconReady += Repaint;
            AttachmentSpriteAssigner.WarmUp();

            _model.RefreshGunList(GunEntriesPath);
            _model.LoadSkins();
            if (_model.AllGuns.Count > 0)
            {
                _model.SelectedType = _model.AllGuns[0].GunType;
                _model.FilterGuns();
                _model.RandomizeAll(new System.Collections.Generic.Dictionary<GunType, bool>(), skipHistory: true);
            }
        }

        private void OnDisable()
        {
            AttachmentIconRenderer.IconReady -= Repaint;
            _model.PreviewManager?.Dispose();
            AttachmentIconRenderer.ClearCache();
        }

        private void OnGUI()
        {
            if (_model.AllGuns.Count == 0)
            {
                EditorGUILayout.HelpBox("No guns found", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _leftDrawer.Draw();
                DrawRightPanel();
            }
        }

        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(937)))
            {
                var attachmentTypes = System.Enum.GetValues(typeof(AttachmentType)).Cast<AttachmentType>().ToList();
                const int columns = 4;

                var hasSkins = _model.Skins.Count > 0;
                var totalCards = attachmentTypes.Count + (hasSkins ? 1 : 0);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosRight, GUILayout.ExpandHeight(true)))
                {
                    _scrollPosRight = scrollView.scrollPosition;

                    for (var i = 0; i < totalCards; i += columns)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (var j = 0; j < columns; j++)
                            {
                                var idx = i + j;
                                if (idx >= totalCards) break;

                                if (idx < attachmentTypes.Count)
                                {
                                    var type = attachmentTypes[idx];
                                    _cardDrawer.DrawCard(
                                        type,
                                        _model.AttachmentOptions,
                                        _model.ActiveAttachments,
                                        _model.VariantIndices,
                                        _model.AttachmentEnabled,
                                        _model.RayEnabled,
                                        SetRayEnabled,
                                        isForceDisabled: t => _model.ForceDisabledTypes.Contains(t),
                                        skinColorName: _model.SelectedSkinColorName);
                                }
                                else if (hasSkins && idx == attachmentTypes.Count)
                                {
                                    DrawSkinCard();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DrawSkinCard()
        {
            if (_model.Skins.Count == 0) return;

            var centeredTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
            };
            var centeredText = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(200), GUILayout.Height(200)))
            {
                GUILayout.Label("Skin", centeredTitle);
                GUILayout.Space(6);

                var previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(141.5f), GUILayout.ExpandWidth(true));
                var skin = _model.Skins[_model.SelectedSkinIndex];
                Texture previewTex = null;
                if (skin.PreviewIcon != null)
                    previewTex = skin.PreviewIcon.texture;
                else if (skin.Material != null && skin.Material.mainTexture != null)
                    previewTex = skin.Material.mainTexture;

                if (previewTex != null)
                {
                    GUI.DrawTexture(previewRect, previewTex, ScaleMode.ScaleToFit);
                }
                else
                {
                    var noneStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12
                    };
                    EditorGUI.LabelField(previewRect, "— No Preview —", noneStyle);
                }

                GUILayout.Space(24);
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("back"), GUILayout.Width(50), GUILayout.Height(18)))
                    {
                        _model.PreviousSkin();
                        Repaint();
                    }

                    GUILayout.FlexibleSpace();

                    var counterText = $"{_model.SelectedSkinIndex + 1}/{_model.Skins.Count}";
                    GUILayout.Label(counterText, centeredText, GUILayout.Width(50));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(EditorGUIUtility.IconContent("forward"), GUILayout.Width(50), GUILayout.Height(18)))
                    {
                        _model.NextSkin();
                        Repaint();
                    }
                }
            }
        }

        public void SetAttachment(AttachmentType type, AttachmentEntry entry, int variant)
        {
            _model.SetAttachment(type, entry, variant);
        }

        public void SetAttachmentEnabled(AttachmentType type, bool enabled)
        {
            _model.SetAttachmentEnabled(type, enabled);
        }

        public void SetRayEnabled(AttachmentType type, bool enabled)
        {
            _model.SetRayEnabled(type, enabled);
        }
    }
}
