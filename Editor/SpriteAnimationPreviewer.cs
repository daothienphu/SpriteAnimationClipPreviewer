using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpookyCore.Editor.EntitySystem
{
    public class SpriteAnimationPreviewer : EditorWindow
    {
        #region Fields

        private AnimationClip _clip;
        private AnimationClip _lastClip;
        private readonly List<ObjectReferenceKeyframe> _frames = new();
        
        private float _time;
        private float _frameTimer;
        private float _frameTime;
        private double _lastTime;
        
        private bool _clipFromSpriteSheet = true;
        private int _fps = 12;
        private float _sliderTimePercent;
        private bool _isPlaying = true;
        
        private bool _showFrameInfo = true;

        #endregion

        #region Life Cycle

        [MenuItem("Tools/Sprite Animation Clip Previewer")]
        public static void ShowWindow()
        {
            GetWindow<SpriteAnimationPreviewer>("Sprite Animation Clip Previewer");
        }

        private void OnGUI()
        {
            if (!TryGetClip())
            {
                return;
            }
            
            if (_frames.Count == 0)
            {
                EditorGUILayout.HelpBox("No sprite keyframes found in this clip.", MessageType.Warning);
                return;
            }
            
            _clipFromSpriteSheet = EditorGUILayout.Toggle("Made from spritesheet", _clipFromSpriteSheet);
            
            EditorGUILayout.Space();
            GUILayout.Label("Preview Settings", EditorStyles.boldLabel);
            _showFrameInfo = EditorGUILayout.Toggle("Show Frame Info", _showFrameInfo);
            
            HandleFPS();
            HandleTimeSeek();
            HandlePreviewFrame();
        }

        #endregion

        #region Private Methods

        private bool TryGetClip()
        {
            EditorGUILayout.Space();
            _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);
            if (_clip != _lastClip)
            {
                _lastClip = _clip;
                ExtractSpriteKeyframes(_clip);
                _time = 0;
                _sliderTimePercent = 0;
                _lastTime = EditorApplication.timeSinceStartup;
                _frameTimer = 0;
                EditorApplication.update -= Repaint;
                if (_clip)
                {
                    EditorApplication.update += Repaint;
                }
            }
            
            if (!_clip)
            {
                _frames.Clear();
                return false;
            }

            return true;
        }
        private void HandleFPS()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("FPS", GUILayout.Width(40));
            _fps = (int)GUILayout.HorizontalSlider(_fps, 1, 60);
            _fps = EditorGUILayout.IntField(_fps, GUILayout.Width(40));
            _fps = Mathf.Clamp(_fps, 1, 60);
            EditorGUILayout.EndHorizontal();
            _frameTime = 1f / _fps;
        }
        private void HandleTimeSeek()
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField($"{_time:F2}", GUILayout.Width(40));
            
            EditorGUI.BeginChangeCheck();
            _sliderTimePercent = GUILayout.HorizontalSlider(_sliderTimePercent, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _isPlaying = false;
                _time = _clip.length * _sliderTimePercent;
                _lastTime = EditorApplication.timeSinceStartup;
            }

            //prev
            if (GUILayout.Button("◀", GUILayout.Width(30)))
            {
                StepFrame(-1);
                _isPlaying = false;
            }
            //play/pause
            if (GUILayout.Button(_isPlaying ? "❚❚" : "▶", GUILayout.Width(30)))
            {
                _isPlaying = !_isPlaying;
                _lastTime = EditorApplication.timeSinceStartup;
            }
            //next
            if (GUILayout.Button("▶", GUILayout.Width(30)))
            {
                StepFrame(1);
                _isPlaying = false;
            }

            EditorGUILayout.EndHorizontal();
        }
        private void HandlePreviewFrame()
        {
            if (_isPlaying)
            {
                var now = EditorApplication.timeSinceStartup;
                var delta = (float)(now - _lastTime);
                _lastTime = now;

                _frameTimer += delta;

                if (_frameTimer >= _frameTime)
                {
                    _frameTimer -= _frameTime;
                    StepFrame(1);
                }
            }

            GetSpriteAtTime(_time, out var sprite, out var frameIndex);
            if (sprite)
            {
                DrawSprite(sprite, _clipFromSpriteSheet, frameIndex);
            }
        }
        
        private void ExtractSpriteKeyframes(AnimationClip clip)
        {
            _frames.Clear();
            if (!clip) return;

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.propertyName != "m_Sprite") continue;

                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                foreach (var keyframe in keyframes)
                {
                    if (keyframe.value is Sprite sprite)
                    {
                        _frames.Add(new ObjectReferenceKeyframe
                        {
                            time = keyframe.time,
                            value = sprite
                        });
                    }
                }

                _frames.Sort((a, b) => a.time.CompareTo(b.time));
                break;
            }
        }
        private void GetSpriteAtTime(float time, out Sprite sprite, out int index)
        {
            if (_frames.Count == 0)
            {
                index = -1;
                sprite = null;
                return;
            }

            for (var i = 0; i < _frames.Count - 1; i++)
            {
                if (time >= _frames[i].time && time < _frames[i + 1].time)
                {
                    sprite = _frames[i].value as Sprite;
                    index = i;
                    return;
                }
            }

            sprite = _frames[^1].value as Sprite;
            index = _frames.Count - 1;
        }
        private int GetClipFrameIndexAtTime(float time)
        {
            if (_frames.Count == 0) return 0;

            for (var i = 0; i < _frames.Count - 1; i++)
            {
                if (time >= _frames[i].time && time < _frames[i + 1].time)
                {
                    return i;
                }
            }

            return _frames.Count - 1;
        }
        private void StepFrame(int direction)
        {
            if (_frames.Count == 0) return;

            var currentFrame = GetClipFrameIndexAtTime(_time);
            var nextFrame = (_frames.Count + currentFrame + direction) % _frames.Count;

            _time = _frames[nextFrame].time;
            _sliderTimePercent = _time / _clip.length;
        }
        private void DrawSprite(Sprite sprite, bool fromSheet, int frameIndex = -1)
        {
            var texture = sprite.texture;
            var rect = sprite.rect;
            var aspect = rect.width / rect.height;
            var drawRect = GUILayoutUtility.GetAspectRect(aspect);

            if (fromSheet)
            {
                var uv = new Rect(
                    rect.x / texture.width,
                    rect.y / texture.height,
                    rect.width / texture.width,
                    rect.height / texture.height
                );
                GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
            }
            else
            {
                EditorGUI.DrawPreviewTexture(drawRect, texture, null, ScaleMode.ScaleToFit);
            }
            
            if (_showFrameInfo)
            {
                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                var bgStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    padding = new RectOffset(4, 4, 2, 2),
                };

                var label = $"Frame: {frameIndex + 1}/{_frames.Count}\nName: {sprite.name}";
                
                var size = labelStyle.CalcSize(new GUIContent(label));
                var labelRect = new Rect(drawRect.x + 4, drawRect.y + 4, drawRect.width - 8, size.y + 6);

                GUI.Label(labelRect, label, bgStyle);
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        #endregion
    }
}