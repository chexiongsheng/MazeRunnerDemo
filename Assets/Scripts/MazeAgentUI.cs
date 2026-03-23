using System;
using System.Collections.Generic;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Chat-panel UI for the Maze AI Agent.
    /// Displays a scrollable conversation panel on the right side of the screen (30% width),
    /// with an input field at the bottom. Also shows a "Thinking..." bubble above the player
    /// and status messages.
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

        [Header("Chat Panel Settings")]
        [Tooltip("Width fraction of screen for the chat panel (0..1).")]
        [Range(0.2f, 0.5f)]
        public float panelWidthFraction = 0.3f;

        [Header("Status Panel Settings")]
        [Tooltip("Show status panel in the top-left corner.")]
        public bool showStatusPanel = true;

        // --- Chat message model ---
        public enum MessageRole { User, Assistant, System, Progress }

        [Serializable]
        public class ChatMessage
        {
            public MessageRole role;
            public string text;
            public float timestamp;

            public ChatMessage(MessageRole role, string text)
            {
                this.role = role;
                this.text = text;
                this.timestamp = Time.time;
            }
        }

        // Internal state
        private bool isThinking;
        private string statusMessage = "Initializing...";
        private string thinkingDots = "";
        private float dotTimer;
        private int dotCount;
        private Camera mainCam;

        // Chat state
        private List<ChatMessage> chatMessages = new List<ChatMessage>();
        private string inputText = "";
        private Vector2 scrollPosition;
        private bool scrollToBottom = false;
        private bool inputFocusRequested = false;

        // Callback for sending messages
        public Action<string> onSendMessage;

        // GUI styles (created once in OnGUI)
        private GUIStyle bubbleStyle;
        private GUIStyle statusStyle;
        private GUIStyle statusBgStyle;
        private GUIStyle successStyle;
        private GUIStyle userMsgStyle;
        private GUIStyle assistantMsgStyle;
        private GUIStyle systemMsgStyle;
        private GUIStyle progressMsgStyle;
        private GUIStyle inputFieldStyle;
        private GUIStyle sendButtonStyle;
        private GUIStyle panelBgStyle;
        private GUIStyle chatLabelStyle;
        private bool stylesInitialized;

        private bool mazeCompleted;

        // Scrollbar tracking
        private float lastContentHeight = 0f;

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

        /// <summary>Show the thinking bubble above the player.</summary>
        public void ShowThinking()
        {
            isThinking = true;
            dotCount = 0;
            dotTimer = 0f;
            thinkingDots = "";
        }

        /// <summary>Hide the thinking bubble.</summary>
        public void HideThinking()
        {
            isThinking = false;
        }

        /// <summary>Update the status message displayed in the top-left corner.</summary>
        public void SetStatus(string message)
        {
            statusMessage = message;
        }

        /// <summary>Show the maze completion success screen.</summary>
        public void ShowMazeCompleted()
        {
            mazeCompleted = true;
            isThinking = false;
            statusMessage = "Maze Completed!";
            AddMessage(MessageRole.System, "🎉 Maze challenge completed!");
        }

        /// <summary>Reset the UI state for a new maze run.</summary>
        public void ResetUI()
        {
            mazeCompleted = false;
            isThinking = false;
            statusMessage = "Ready";
            dotCount = 0;
            chatMessages.Clear();
            inputText = "";
            scrollPosition = Vector2.zero;
        }

        /// <summary>Add a chat message to the panel.</summary>
        public void AddMessage(MessageRole role, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            chatMessages.Add(new ChatMessage(role, text));
            scrollToBottom = true;
        }

        /// <summary>
        /// Update or add a progress message. Progress messages replace the last progress message
        /// to avoid flooding the chat with incremental updates.
        /// </summary>
        public void UpdateProgress(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Append as new progress message (don't replace — show the history)
            chatMessages.Add(new ChatMessage(MessageRole.Progress, text));
            scrollToBottom = true;
        }

        /// <summary>Set the initial text in the input field.</summary>
        public void SetInputText(string text)
        {
            inputText = text ?? "";
        }

        /// <summary>Get the current input field text.</summary>
        public string GetInputText()
        {
            return inputText;
        }

        // --- GUI Rendering ---

        private Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            // Thinking bubble
            bubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bubbleStyle.normal.textColor = textColor;
            bubbleStyle.normal.background = MakeTex(bubbleColor);
            bubbleStyle.padding = new RectOffset(12, 12, 6, 6);

            // Status panel
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            statusStyle.normal.textColor = Color.white;

            statusBgStyle = new GUIStyle(GUI.skin.box);
            statusBgStyle.normal.background = MakeTex(new Color(0, 0, 0, 0.6f));

            // Success overlay
            successStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            successStyle.normal.textColor = new Color(0.2f, 1f, 0.2f, 1f);
            successStyle.normal.background = MakeTex(new Color(0, 0, 0, 0.8f));

            // Chat panel background
            panelBgStyle = new GUIStyle(GUI.skin.box);
            panelBgStyle.normal.background = MakeTex(new Color(0.12f, 0.12f, 0.15f, 0.95f));

            // Chat label (title)
            chatLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            chatLabelStyle.normal.textColor = Color.white;

            // User message bubble
            userMsgStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
            userMsgStyle.normal.textColor = Color.white;
            userMsgStyle.normal.background = MakeTex(new Color(0.15f, 0.4f, 0.7f, 0.9f));
            userMsgStyle.padding = new RectOffset(8, 8, 6, 6);

            // Assistant message bubble
            assistantMsgStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
            assistantMsgStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            assistantMsgStyle.normal.background = MakeTex(new Color(0.2f, 0.22f, 0.25f, 0.9f));
            assistantMsgStyle.padding = new RectOffset(8, 8, 6, 6);

            // System message
            systemMsgStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            systemMsgStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            systemMsgStyle.normal.background = MakeTex(new Color(0.3f, 0.25f, 0.1f, 0.7f));
            systemMsgStyle.padding = new RectOffset(8, 8, 4, 4);

            // Progress message (tool calls)
            progressMsgStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
            progressMsgStyle.normal.textColor = new Color(0.7f, 0.8f, 0.7f);
            progressMsgStyle.normal.background = MakeTex(new Color(0.15f, 0.2f, 0.15f, 0.8f));
            progressMsgStyle.padding = new RectOffset(8, 8, 4, 4);

            // Input field
            inputFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                wordWrap = true
            };
            inputFieldStyle.normal.textColor = Color.white;
            inputFieldStyle.normal.background = MakeTex(new Color(0.2f, 0.2f, 0.25f, 1f));
            inputFieldStyle.focused.textColor = Color.white;
            inputFieldStyle.focused.background = MakeTex(new Color(0.25f, 0.25f, 0.3f, 1f));
            inputFieldStyle.padding = new RectOffset(8, 8, 6, 6);

            // Send button
            sendButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            sendButtonStyle.normal.textColor = Color.white;
            sendButtonStyle.normal.background = MakeTex(new Color(0.2f, 0.5f, 0.8f, 1f));
            sendButtonStyle.hover.background = MakeTex(new Color(0.25f, 0.6f, 0.9f, 1f));
            sendButtonStyle.active.background = MakeTex(new Color(0.15f, 0.4f, 0.7f, 1f));

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Thinking bubble above player
            DrawThinkingBubble();

            // 2. Status panel (top-left)
            DrawStatusPanel();

            // 3. Chat panel (right side)
            DrawChatPanel();

            // 4. Maze completion overlay
            DrawCompletionOverlay();
        }

        private void DrawThinkingBubble()
        {
            if (!isThinking || playerTransform == null || mainCam == null) return;

            Vector3 worldPos = playerTransform.position + Vector3.up * bubbleOffsetY;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0)
            {
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

        private void DrawStatusPanel()
        {
            if (!showStatusPanel || string.IsNullOrEmpty(statusMessage)) return;

            Rect bgRect = new Rect(10, 10, 300, 30);
            GUI.Box(bgRect, "", statusBgStyle);
            GUI.Label(new Rect(15, 13, 290, 24), $"🤖 AI Status: {statusMessage}", statusStyle);
        }

        private void DrawChatPanel()
        {
            float panelWidth = Screen.width * panelWidthFraction;
            float panelX = Screen.width - panelWidth;
            float panelY = 0;
            float panelHeight = Screen.height;

            // Panel background
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), "", panelBgStyle);

            // Title bar
            float titleHeight = 36f;
            GUI.Label(new Rect(panelX, panelY + 4, panelWidth, titleHeight), "AI Chat", chatLabelStyle);

            // Input area at bottom
            float inputAreaHeight = 80f;
            float sendBtnWidth = 60f;
            float inputPadding = 8f;
            float inputY = panelY + panelHeight - inputAreaHeight;
            float inputFieldHeight = inputAreaHeight - inputPadding * 2;

            // Separator line above input
            GUI.Box(new Rect(panelX + 4, inputY - 1, panelWidth - 8, 1), "", statusBgStyle);

            // Input field
            GUI.SetNextControlName("ChatInput");
            inputText = GUI.TextField(
                new Rect(panelX + inputPadding, inputY + inputPadding, panelWidth - sendBtnWidth - inputPadding * 3, inputFieldHeight),
                inputText,
                inputFieldStyle
            );

            // Send button
            bool sendClicked = GUI.Button(
                new Rect(panelX + panelWidth - sendBtnWidth - inputPadding, inputY + inputPadding, sendBtnWidth, inputFieldHeight),
                "Send",
                sendButtonStyle
            );

            // Handle Enter key (when input is focused)
            bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "ChatInput";

            if ((sendClicked || enterPressed) && !string.IsNullOrWhiteSpace(inputText))
            {
                string msg = inputText.Trim();
                inputText = "";
                onSendMessage?.Invoke(msg);

                if (enterPressed)
                {
                    Event.current.Use();
                }
            }

            if (inputFocusRequested)
            {
                GUI.FocusControl("ChatInput");
                inputFocusRequested = false;
            }

            // Chat messages scroll area
            float chatAreaY = panelY + titleHeight;
            float chatAreaHeight = inputY - chatAreaY - 4;
            float msgPadding = 6f;
            float msgMaxWidth = panelWidth - 32f;

            // Calculate content height
            float contentHeight = 0;
            foreach (var msg in chatMessages)
            {
                GUIStyle style = GetStyleForRole(msg.role);
                float msgWidth = msg.role == MessageRole.System ? msgMaxWidth : msgMaxWidth * 0.85f;
                float h = style.CalcHeight(new GUIContent(msg.text), msgWidth);
                contentHeight += h + msgPadding;
            }
            contentHeight += msgPadding; // Bottom padding

            // Auto-scroll to bottom
            if (scrollToBottom)
            {
                scrollPosition.y = Mathf.Max(0, contentHeight - chatAreaHeight);
                scrollToBottom = false;
            }

            // Draw scroll view
            Rect chatViewRect = new Rect(panelX + 4, chatAreaY, panelWidth - 8, chatAreaHeight);
            Rect chatContentRect = new Rect(0, 0, panelWidth - 24, contentHeight);

            scrollPosition = GUI.BeginScrollView(chatViewRect, scrollPosition, chatContentRect);

            float yPos = msgPadding;
            foreach (var msg in chatMessages)
            {
                GUIStyle style = GetStyleForRole(msg.role);
                float msgWidth = msg.role == MessageRole.System ? msgMaxWidth : msgMaxWidth * 0.85f;
                float h = style.CalcHeight(new GUIContent(msg.text), msgWidth);

                float xPos;
                if (msg.role == MessageRole.User)
                {
                    // Right-aligned
                    xPos = chatContentRect.width - msgWidth - 4;
                }
                else if (msg.role == MessageRole.System)
                {
                    // Centered
                    xPos = (chatContentRect.width - msgWidth) / 2f;
                }
                else
                {
                    // Left-aligned (Assistant, Progress)
                    xPos = 4;
                }

                GUI.Box(new Rect(xPos, yPos, msgWidth, h), msg.text, style);
                yPos += h + msgPadding;
            }

            GUI.EndScrollView();
        }

        private void DrawCompletionOverlay()
        {
            if (!mazeCompleted) return;

            float w = 500, h = 80;
            // Place in the left portion (maze area), not covering chat panel
            float mazeAreaWidth = Screen.width * (1f - panelWidthFraction);
            Rect centerRect = new Rect(
                (mazeAreaWidth - w) / 2f,
                (Screen.height - h) / 2f - 50,
                w, h
            );
            GUI.Box(centerRect, "🎉 Maze Challenge Completed!", successStyle);
        }

        private GUIStyle GetStyleForRole(MessageRole role)
        {
            switch (role)
            {
                case MessageRole.User: return userMsgStyle;
                case MessageRole.Assistant: return assistantMsgStyle;
                case MessageRole.System: return systemMsgStyle;
                case MessageRole.Progress: return progressMsgStyle;
                default: return assistantMsgStyle;
            }
        }
    }
}
