using System;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Main controller for the AI Maze Runner demo scene.
    /// Manages Agent lifecycle, user interaction, and UI state.
    /// Supports a chat-based interaction model where the user can send messages
    /// to the AI, the AI can explore the maze, and the user can interrupt at any time.
    /// </summary>
    public class MazeDemoManager : MonoBehaviour
    {
        [Header("Agent Settings")]
        [Tooltip("Resource root for the maze-runner agent.")]
        public string agentResourceRoot = "maze-runner";

        [Tooltip("API Key for the LLM service.")]
        public string apiKey = "";

        [Tooltip("Base URL for the LLM API (leave empty for default).")]
        public string baseURL = "";

        [Tooltip("Model name (leave empty for default).")]
        public string model = "";

        [Tooltip("Maximum tool-call steps per generation. 0 or negative = unlimited.")]
        public int maxSteps = 0;

        [Header("Maze Settings")]
        [Tooltip("The default first message sent to the AI to start maze exploration.")]
        [TextArea(3, 5)]
        public string startMessage = "The red marker indicates the end of the maze; proceed to the finish.";

        [Header("References")]
        [Tooltip("Optional: MazeAgentUI component. If null, will try to find one in scene.")]
        public MazeAgentUI agentUI;

        // Internal state
        private AgentScriptManager agent;
        private bool isGenerating;
        private bool isInitialized;

        private enum DemoState
        {
            Uninitialized,
            Initializing,
            Ready,
            Generating,
            Completed,
            Error
        }

        private DemoState currentState = DemoState.Uninitialized;

        private void Awake()
        {
            Application.runInBackground = true;

            if (agentUI == null)
            {
                agentUI = FindObjectOfType<MazeAgentUI>();
            }
        }

        private void Start()
        {
            // Wire up the chat panel's send callback
            if (agentUI != null)
            {
                agentUI.onSendMessage = OnUserSendMessage;
                // Pre-fill the input with startMessage
                agentUI.SetInputText(startMessage);
            }

            InitializeAgent();
        }

        private void OnDestroy()
        {
            if (agent != null)
            {
                agent.Dispose();
                agent = null;
            }
        }

        /// <summary>
        /// Initialize the Agent and load all modules.
        /// </summary>
        private void InitializeAgent()
        {
            SetState(DemoState.Initializing);
            Debug.Log("[MazeDemoManager] Initializing maze-runner agent...");

            agent = new AgentScriptManager();
            agent.Initialize(agentResourceRoot, () =>
            {
                Debug.Log("[MazeDemoManager] Agent initialized successfully.");

                // Configure with API key if provided
                if (!string.IsNullOrEmpty(apiKey))
                {
                    string configResult = agent.ConfigureAgent(apiKey, baseURL, model, maxSteps);
                    Debug.Log($"[MazeDemoManager] Agent configured: {configResult}");
                }

                isInitialized = true;
                SetState(DemoState.Ready);
            });
        }

        /// <summary>
        /// Called when the user sends a message from the chat panel.
        /// </summary>
        private void OnUserSendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // If currently generating, abort first then send the new message
            if (isGenerating)
            {
                Debug.Log("[MazeDemoManager] Aborting current generation to send new message...");
                agent.AbortGeneration();
                // AbortGeneration triggers the callback with abort message.
                // We'll queue the new message to be sent after abort completes.
                pendingMessage = message;
                return;
            }

            SendChatMessage(message);
        }

        private string pendingMessage = null;

        /// <summary>
        /// Send a chat message to the AI agent.
        /// </summary>
        private void SendChatMessage(string message)
        {
            if (!isInitialized)
            {
                agentUI?.AddMessage(MazeAgentUI.MessageRole.System, "Agent not yet initialized. Please wait...");
                return;
            }

            if (!agent.IsAgentConfigured())
            {
                agentUI?.AddMessage(MazeAgentUI.MessageRole.System, "Error: API not configured. Please set API key.");
                SetState(DemoState.Error);
                return;
            }

            // Add user message to chat
            agentUI?.AddMessage(MazeAgentUI.MessageRole.User, message);

            isGenerating = true;
            SetState(DemoState.Generating);
            agentUI?.ShowThinking();

            agent.SendMessageAsync(
                message,
                "", // no image attachment
                (response, isError) =>
                {
                    // Called when the AI finishes its full response
                    agentUI?.HideThinking();
                    bool hadStreaming = agentUI != null && agentUI.ResetStreaming();
                    isGenerating = false;

                    // Check if there's a pending message (user sent while generating)
                    string pending = pendingMessage;
                    pendingMessage = null;

                    if (isError)
                    {
                        Debug.LogError($"[MazeDemoManager] Agent error: {response}");
                        SetState(DemoState.Error);

                        // Show error in chat but allow user to continue
                        string errorDisplay = response;
                        if (response.Contains("Generation stopped by user"))
                        {
                            errorDisplay = "⏹ Generation stopped.";
                        }
                        agentUI?.AddMessage(MazeAgentUI.MessageRole.System, errorDisplay);

                        // If there's a pending message, send it now
                        if (!string.IsNullOrEmpty(pending))
                        {
                            SendChatMessage(pending);
                            return;
                        }

                        // Go back to ready so user can continue
                        SetState(DemoState.Ready);
                    }
                    else
                    {
                        Debug.Log($"[MazeDemoManager] Agent response: {response}");

                        // Add AI response to chat (skip if streaming already displayed it)
                        if (!hadStreaming && !string.IsNullOrEmpty(response))
                        {
                            agentUI?.AddMessage(MazeAgentUI.MessageRole.Assistant, response);
                        }

                        // Check if the maze was actually completed
                        bool mazeActuallyCompleted = false;
                        var playerObj = GameObject.FindWithTag("Player");
                        if (playerObj != null)
                        {
                            var goalDetector = playerObj.GetComponent<MazeGoalDetector>();
                            if (goalDetector != null)
                            {
                                mazeActuallyCompleted = goalDetector.HasReachedGoal;
                            }
                        }

                        if (mazeActuallyCompleted)
                        {
                            SetState(DemoState.Completed);
                            agentUI?.ShowMazeCompleted();
                        }
                        else
                        {
                            SetState(DemoState.Ready);
                        }

                        // If there's a pending message, send it now
                        if (!string.IsNullOrEmpty(pending))
                        {
                            SendChatMessage(pending);
                        }
                    }
                },
                (progressText) =>
                {
                    // Progress callback — AI is streaming/thinking
                    if (!string.IsNullOrEmpty(progressText))
                    {
                        //Debug.Log($"[MazeDemoManager] Progress: {progressText.Substring(0, Math.Min(100, progressText.Length))}...");
                        agentUI?.UpdateProgress(progressText);
                    }
                }
            );
        }

        /// <summary>
        /// Stop the current exploration (abort generation).
        /// </summary>
        public void StopExploration()
        {
            if (agent != null && isGenerating)
            {
                agent.AbortGeneration();
                // The abort will be handled in the callback
                Debug.Log("[MazeDemoManager] Exploration stop requested by user.");
            }
        }

        /// <summary>
        /// Reset the maze: clear history, reset player position, etc.
        /// </summary>
        public void ResetMaze()
        {
            if (agent != null)
            {
                if (isGenerating)
                {
                    agent.AbortGeneration();
                    isGenerating = false;
                }
                agent.ClearHistory();
            }

            pendingMessage = null;
            agentUI?.HideThinking();
            agentUI?.ResetUI();
            agentUI?.SetInputText(startMessage);

            // Reset goal detector on player
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                var goalDetector = playerObj.GetComponent<MazeGoalDetector>();
                if (goalDetector != null)
                {
                    goalDetector.ResetGoal();
                }
            }

            SetState(DemoState.Ready);
            Debug.Log("[MazeDemoManager] Maze reset.");
        }

        private void SetState(DemoState newState)
        {
            currentState = newState;
            switch (newState)
            {
                case DemoState.Initializing:
                    agentUI?.SetStatus("Initializing...");
                    break;
                case DemoState.Ready:
                    agentUI?.SetStatus("Ready");
                    break;
                case DemoState.Generating:
                    agentUI?.SetStatus("Thinking...");
                    break;
                case DemoState.Completed:
                    agentUI?.SetStatus("Maze Completed!");
                    break;
                case DemoState.Error:
                    // Status already set by caller
                    break;
            }
        }

        // --- OnGUI: Simple buttons for demo control (in the maze area, not the chat panel) ---

        private void OnGUI()
        {
            // Place buttons in the top-left area (within the maze view, not the chat panel)
            float btnWidth = 100f;
            float btnHeight = 30f;
            float padding = 10f;
            float startX = padding;
            float startY = 50f; // Below status panel

            GUI.skin.button.fontSize = 12;

            switch (currentState)
            {
                case DemoState.Generating:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "⏹ Stop"))
                    {
                        StopExploration();
                    }
                    startY += btnHeight + 5;
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Reset"))
                    {
                        ResetMaze();
                    }
                    break;

                case DemoState.Ready:
                case DemoState.Error:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Reset"))
                    {
                        ResetMaze();
                    }
                    break;

                case DemoState.Completed:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Play Again"))
                    {
                        ResetMaze();
                    }
                    break;

                case DemoState.Initializing:
                    GUI.Label(new Rect(startX, startY, btnWidth, btnHeight), "Loading...");
                    break;
            }
        }
    }
}
