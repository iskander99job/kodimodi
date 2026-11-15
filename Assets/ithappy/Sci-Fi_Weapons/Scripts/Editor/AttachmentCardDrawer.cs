using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class AttachmentCardDrawer
    {
        private readonly GunCustomizationWindow _window;

        public AttachmentCardDrawer(GunCustomizationWindow window)
        {
            _window = window;
        }

        private const float AssetPreviewZoom = 1.37f;

        private static void DrawPreview(Rect container, Texture texture, bool isFramedIcon)
        {
            var scale = Mathf.Min(container.width / texture.width, container.height / texture.height);
            var size = new Vector2(texture.width * scale, texture.height * scale);
            var target = new Rect(
                container.x + (container.width - size.x) * 0.5f,
                container.y + (container.height - size.y) * 0.5f,
                size.x,
                size.y);

            if (isFramedIcon)
            {
                GUI.DrawTexture(target, texture, ScaleMode.StretchToFill);
                return;
            }

            // Zoom by cropping the source UVs so the image can never grow past its rect. Scaling the
            // GUI matrix instead drew outside it and spilled over the card title.
            var visible = 1f / AssetPreviewZoom;
            var margin = (1f - visible) * 0.5f;
            GUI.DrawTextureWithTexCoords(target, texture, new Rect(margin, margin, visible, visible));
        }

        public void DrawCard(
            AttachmentType type,
            Dictionary<AttachmentType, List<AttachmentEntry>> attachmentOptions,
            Dictionary<AttachmentType, AttachmentEntry> activeAttachments,
            Dictionary<AttachmentType, int> variantIndices,
            Dictionary<AttachmentType, bool> attachmentEnabled,
            Dictionary<AttachmentType, bool> rayEnabled,
            System.Action<AttachmentType, bool> onRayToggled,
            System.Func<AttachmentType, bool> isForceDisabled = null,
            string skinColorName = null)
        {
            var available = attachmentOptions.ContainsKey(type);

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
                EditorGUI.BeginDisabledGroup(!available);

                GUILayout.Label(type.ToString(), centeredTitle);
                GUILayout.Space(6);

                Texture bg = null;
                var isFramedIcon = false;
                if (available)
                {
                    var entry = activeAttachments[type];
                    if (entry != null)
                    {
                        if (entry.Icon != null)
                        {
                            var skinned = AttachmentSpriteAssigner.GetIconForColor(entry.Icon, skinColorName);
                            bg = (skinned ? skinned : entry.Icon).texture;
                            isFramedIcon = true;
                        }
                        else
                        {
                            var prefab = entry.Elements?.FirstOrDefault(e => e.Prefab)?.Prefab;
                            if (prefab != null)
                            {
                                var tex = (Texture)AttachmentIconRenderer.GetIcon(prefab, skinColorName);
                                isFramedIcon = tex != null;
                                if (tex == null)
                                {
                                    tex = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
                                }

                                if (tex != null)
                                {
                                    bg = tex;
                                }
                            }
                        }
                    }
                }

                var previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(141.5f), GUILayout.ExpandWidth(true));

                if (bg != null)
                {
                    DrawPreview(previewRect, bg, isFramedIcon);
                }
                else
                {
                    var noneStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12
                    };
                    EditorGUI.LabelField(previewRect, "— None —", noneStyle);
                }

                GUILayout.Space(4);

                var isBarrel = type == AttachmentType.Barrel;
                var enabled = attachmentEnabled.ContainsKey(type) && attachmentEnabled[type];
                var isRayType = type is AttachmentType.Laser or AttachmentType.AimingSystem;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(isBarrel || (isForceDisabled?.Invoke(type) ?? false));
                    var newEnabled = EditorGUILayout.ToggleLeft("Enabled", enabled && available, GUILayout.Width(70));
                    EditorGUI.EndDisabledGroup();

                    if (!isBarrel && available && newEnabled != enabled)
                    {
                        _window.SetAttachmentEnabled(type, newEnabled);
                    }

                    GUILayout.FlexibleSpace();
                    if (isRayType)
                    {
                        var rayOn = rayEnabled.TryGetValue(type, out var val) && val;
                        var label = rayOn ? "On" : "Off";

                        EditorGUI.BeginDisabledGroup(!available);
                        GUILayout.Label(label, GUILayout.Width(25));
                        var newRayOn = EditorGUILayout.Toggle(rayOn, GUILayout.Width(20));
                        EditorGUI.EndDisabledGroup();

                        if (newRayOn != rayOn && available)
                        {
                            onRayToggled?.Invoke(type, newRayOn);
                        }
                    }
                }

                GUILayout.FlexibleSpace();

                EditorGUI.BeginDisabledGroup(!available);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var total = available ? attachmentOptions[type].Count : 0;
                    var index = available ? variantIndices[type] : 0;

                    if (GUILayout.Button(EditorGUIUtility.IconContent("back"), GUILayout.Width(50), GUILayout.Height(18)))
                    {
                        index = (index - 1 + total) % total;
                        _window.SetAttachment(type, attachmentOptions[type][index], index);
                    }

                    GUILayout.FlexibleSpace();

                    var counterText = total == 0 ? "0/0" : $"{index + 1}/{total}";
                    GUILayout.Label(counterText, centeredText, GUILayout.Width(50));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(EditorGUIUtility.IconContent("forward"), GUILayout.Width(50), GUILayout.Height(18)))
                    {
                        index = (index + 1) % total;
                        _window.SetAttachment(type, attachmentOptions[type][index], index);
                    }
                }

                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}
