using System;
using System.Collections;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Maze-demo-specific screen capture that renders ONLY the 3D scene via Camera.Render,
    /// excluding any IMGUI overlays (chat panel, status text, etc.).
    /// Returns JSON in the same format as ScreenCaptureBridge for drop-in compatibility.
    /// </summary>
    public static class MazeScreenCapture
    {
        private class CaptureRunner : MonoBehaviour
        {
            private static CaptureRunner _instance;

            public static CaptureRunner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("[MazeScreenCaptureRunner]");
                        go.hideFlags = HideFlags.HideAndDontSave;
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _instance = go.AddComponent<CaptureRunner>();
                    }
                    return _instance;
                }
            }

            public void Capture(int maxWidth, int maxHeight, Action<string> onComplete)
            {
                StartCoroutine(CaptureCoroutine(maxWidth, maxHeight, onComplete));
            }

            private IEnumerator CaptureCoroutine(int maxWidth, int maxHeight, Action<string> onComplete)
            {
                // Wait until the frame is fully rendered
                yield return new WaitForEndOfFrame();

                string result;
                try
                {
                    var cam = Camera.main;
                    if (cam == null)
                        cam = UnityEngine.Object.FindObjectOfType<Camera>();

                    if (cam == null)
                    {
                        result = BuildErrorJson("No camera found for screenshot.");
                    }
                    else
                    {
                        // Determine capture resolution
                        int captureWidth = Screen.width;
                        int captureHeight = Screen.height;
                        if (maxWidth > 0 && maxHeight > 0)
                        {
                            float scale = Mathf.Min((float)maxWidth / captureWidth, (float)maxHeight / captureHeight);
                            if (scale < 1f)
                            {
                                captureWidth = Mathf.Max(1, Mathf.RoundToInt(captureWidth * scale));
                                captureHeight = Mathf.Max(1, Mathf.RoundToInt(captureHeight * scale));
                            }
                        }

                        // Render camera to a RenderTexture (no IMGUI)
                        RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
                        rt.Create();

                        RenderTexture prevTarget = cam.targetTexture;
                        RenderTexture prevActive = RenderTexture.active;

                        cam.targetTexture = rt;
                        cam.Render();

                        RenderTexture.active = rt;
                        var tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                        tex.Apply();

                        cam.targetTexture = prevTarget;
                        RenderTexture.active = prevActive;
                        rt.Release();
                        UnityEngine.Object.Destroy(rt);

                        byte[] pngBytes = tex.EncodeToPNG();
                        string base64 = System.Convert.ToBase64String(pngBytes);
                        Debug.Log($"[MazeScreenCapture] Camera render screenshot: {captureWidth}x{captureHeight}, {pngBytes.Length} bytes");
                        UnityEngine.Object.Destroy(tex);

                        result = BuildSuccessJson(base64, captureWidth, captureHeight);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MazeScreenCapture] Capture failed: {ex.Message}");
                    result = BuildErrorJson(ex.Message);
                }

                onComplete?.Invoke(result);
            }
        }

        /// <summary>
        /// Capture the 3D scene (no IMGUI) and return as base64-encoded PNG via callback.
        /// JSON format matches ScreenCaptureBridge output for compatibility.
        /// </summary>
        public static void CaptureAsync(int maxWidth, int maxHeight, Action<string> callback)
        {
            if (callback == null)
            {
                Debug.LogError("[MazeScreenCapture] Callback is null");
                return;
            }

            if (!Application.isPlaying)
            {
                callback.Invoke(BuildErrorJson("MazeScreenCapture only works in Play Mode."));
                return;
            }

            try
            {
                CaptureRunner.Instance.Capture(maxWidth, maxHeight, callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MazeScreenCapture] Capture failed: {ex.Message}");
                callback.Invoke(BuildErrorJson(ex.Message));
            }
        }

        private static string BuildSuccessJson(string base64, int width, int height)
        {
            return $"{{\"success\":true,\"width\":{width},\"height\":{height},\"base64\":\"{base64}\"}}";
        }

        private static string BuildErrorJson(string errorMessage)
        {
            string escaped = EscapeJson(errorMessage);
            return $"{{\"success\":false,\"error\":\"{escaped}\"}}";
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            var sb = new System.Text.StringBuilder(str.Length);
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:x4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
