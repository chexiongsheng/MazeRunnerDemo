using System;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// UI feedback component for the Maze AI Agent.
    /// Displays a "Thinking..." bubble above the player when the AI is processing,
    /// and shows status messages on screen.
    /// </summary>
    public class MazeAgentUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player transform. If null, will search by 'Player' tag.")]
        public Transform playerTransform;

        [Header("Thinking Bubble Settings")]
        [Tooltip("Vertical offset above the player for the thinking bubble.")]
        public float bubbleOffsetY = 2.5f;

        [Tooltip("Thinking bubble background color.")]
        public Color bubbleColor = new Color(0f, 0f, 0f, 0.75f);

        [Tooltip("Thinking text color.")]
        public Color textColor = Color.white;

        [Header("Status Panel Settings")]
        [Tooltip("Show status panel in the top-left corner.")]
        public bool showStatusPanel = true;

        // Internal state
        private bool isThinking;
        private string statusMessage = "Initializing...";
        private string thinkingDots = "";
        private float dotTimer;
        private int dotCount;
        private Camera mainCam;

        // GUI styles (created once in OnGUI)
        private GUIStyle bubbleStyle;
        private GUIStyle statusStyle;
        private GUIStyle statusBgStyle;
        private GUIStyle successStyle;
        private bool stylesInitialized;

        private bool mazeCompleted;

        private void Start()
        {
            mainCam = Camera.main;
            if (playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    playerTransform = playerObj.transform;
            }
        }

        private void Update()
        {
            // Animate thinking dots
            if (isThinking)
            {
                dotTimer += Time.deltaTime;
                if (dotTimer >= 0.5f)
                {
                    dotTimer = 0f;
                    dotCount = (dotCount + 1) % 4;
                    thinkingDots = new string('.', dotCount);
                }
            }
        }

        // --- Public API (called by MazeDemoManager) ---

        /// <summary>
        /// Show the thinking bubble above the player.
        /// </summary>
        public void ShowThinking()
        {
            isThinking = true;
            dotCount = 0;
            dotTimer = 0f;
            thinkingDots = "";
        }

        /// <summary>
        /// Hide the thinking bubble.
        /// </summary>
        public void HideThinking()
        {
            isThinking = false;
        }

        /// <summary>
        /// Update the status message displayed in the top-left corner.
        /// </summary>
        public void SetStatus(string message)
        {
            statusMessage = message;
        }

        /// <summary>
        /// Show the maze completion success screen.
        /// </summary>
        public void ShowMazeCompleted()
        {
            mazeCompleted = true;
            isThinking = false;
            statusMessage = "Maze Completed!";
        }

        /// <summary>
        /// Reset the UI state for a new maze run.
        /// </summary>
        public void ResetUI()
        {
            mazeCompleted = false;
            isThinking = false;
            statusMessage = "Ready";
            dotCount = 0;
        }

        // --- GUI Rendering ---

        private void InitStyles()
        {
            if (stylesInitialized) return;

            bubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bubbleStyle.normal.textColor = textColor;

            // Create a solid texture for bubble background
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, bubbleColor);
            bgTex.Apply();
            bubbleStyle.normal.background = bgTex;
            bubbleStyle.padding = new RectOffset(12, 12, 6, 6);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            statusStyle.normal.textColor = Color.white;

            statusBgStyle = new GUIStyle(GUI.skin.box);
            var statusBgTex = new Texture2D(1, 1);
            statusBgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
            statusBgTex.Apply();
            statusBgStyle.normal.background = statusBgTex;

            successStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            successStyle.normal.textColor = new Color(0.2f, 1f, 0.2f, 1f);
            var successBgTex = new Texture2D(1, 1);
            successBgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
            successBgTex.Apply();
            successStyle.normal.background = successBgTex;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Thinking bubble above player
            if (isThinking && playerTransform != null && mainCam != null)
            {
                Vector3 worldPos = playerTransform.position + Vector3.up * bubbleOffsetY;
                Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

                // Only show if in front of camera
                if (screenPos.z > 0)
                {
                    // Convert from Unity screen coords (bottom-left origin) to GUI coords (top-left origin)
                    float guiY = Screen.height - screenPos.y;
                    string text = $"Thinking{thinkingDots}";
                    Vector2 size = bubbleStyle.CalcSize(new GUIContent(text));
                    size.x = Mathf.Max(size.x + 20, 120);
                    size.y = Mathf.Max(size.y + 10, 36);

                    Rect rect = new Rect(
                        screenPos.x - size.x / 2f,
                        guiY - size.y,
                        size.x,
                        size.y
                    );

                    GUI.Box(rect, text, bubbleStyle);
                }
            }

            // 2. Status panel (top-left)
            if (showStatusPanel && !string.IsNullOrEmpty(statusMessage))
            {
                Rect bgRect = new Rect(10, 10, 300, 30);
                GUI.Box(bgRect, "", statusBgStyle);
                GUI.Label(new Rect(15, 13, 290, 24), $"🤖 AI Status: {statusMessage}", statusStyle);
            }

            // 3. Maze completion overlay
            if (mazeCompleted)
            {
                float w = 500, h = 80;
                Rect centerRect = new Rect(
                    (Screen.width - w) / 2f,
                    (Screen.height - h) / 2f - 50,
                    w, h
                );
                GUI.Box(centerRect, "🎉 迷宫挑战成功！", successStyle);
            }
        }
    }
}
