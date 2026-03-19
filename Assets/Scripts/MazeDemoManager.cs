using System;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Main controller for the AI Maze Runner demo scene.
    /// Manages Agent lifecycle, user interaction, and UI state.
    /// Attach this to an empty GameObject in the MazeDemo scene.
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
        [Tooltip("The message sent to the AI to start maze exploration.")]
        [TextArea(3, 5)]
        public string startMessage = "红色标记是迷宫终点，走到终点。";

        [Header("References")]
        [Tooltip("Optional: MazeAgentUI component. If null, will try to find one in scene.")]
        public MazeAgentUI agentUI;

        // Internal state
        private AgentScriptManager agent;
        private bool isExploring;
        private bool isInitialized;

        private enum DemoState
        {
            Uninitialized,
            Initializing,
            Ready,
            Exploring,
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
        /// Start the maze exploration. Called by UI button or programmatically.
        /// </summary>
        public void StartExploration()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[MazeDemoManager] Agent not yet initialized.");
                return;
            }

            if (isExploring)
            {
                Debug.LogWarning("[MazeDemoManager] Already exploring.");
                return;
            }

            if (!agent.IsAgentConfigured())
            {
                Debug.LogError("[MazeDemoManager] Agent not configured. Please set API key.");
                SetState(DemoState.Error);
                agentUI?.SetStatus("Error: API not configured");
                return;
            }

            isExploring = true;
            SetState(DemoState.Exploring);
            Debug.Log("[MazeDemoManager] Starting maze exploration...");

            agentUI?.ShowThinking();

            agent.SendMessageAsync(
                startMessage,
                "", // no image attachment
                (response, isError) =>
                {
                    // Called when the AI finishes its full response
                    agentUI?.HideThinking();
                    isExploring = false;

                    if (isError)
                    {
                        Debug.LogError($"[MazeDemoManager] Agent error: {response}");
                        SetState(DemoState.Error);
                        agentUI?.SetStatus($"Error: {response}");
                    }
                    else
                    {
                        Debug.Log($"[MazeDemoManager] Agent response: {response}");

                        // Check if the maze was actually completed by querying the goal detector
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
                            agentUI?.SetStatus("Exploration paused (step limit reached)");
                        }
                    }
                },
                (progressText) =>
                {
                    // Progress callback — AI is streaming/thinking
                    // Keep showing thinking bubble during progress
                    if (!string.IsNullOrEmpty(progressText))
                    {
                        Debug.Log($"[MazeDemoManager] Progress: {progressText.Substring(0, Math.Min(100, progressText.Length))}...");
                    }
                }
            );
        }

        /// <summary>
        /// Stop the current exploration (abort generation).
        /// </summary>
        public void StopExploration()
        {
            if (agent != null && isExploring)
            {
                agent.AbortGeneration();
                isExploring = false;
                agentUI?.HideThinking();
                SetState(DemoState.Ready);
                Debug.Log("[MazeDemoManager] Exploration stopped by user.");
            }
        }

        /// <summary>
        /// Reset the maze: clear history, reset player position, etc.
        /// </summary>
        public void ResetMaze()
        {
            if (agent != null)
            {
                agent.ClearHistory();
            }

            isExploring = false;
            agentUI?.ResetUI();

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
                    agentUI?.SetStatus("Ready — Press Start");
                    break;
                case DemoState.Exploring:
                    agentUI?.SetStatus("Exploring...");
                    break;
                case DemoState.Completed:
                    agentUI?.SetStatus("Maze Completed!");
                    break;
                case DemoState.Error:
                    // Status already set by caller
                    break;
            }
        }

        // --- OnGUI: Simple buttons for demo control ---

        private void OnGUI()
        {
            float btnWidth = 140f;
            float btnHeight = 36f;
            float padding = 10f;
            float startX = Screen.width - btnWidth - padding;
            float startY = padding;

            GUI.skin.button.fontSize = 14;

            switch (currentState)
            {
                case DemoState.Ready:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "▶ Start Exploration"))
                    {
                        StartExploration();
                    }
                    startY += btnHeight + 5;
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Reset"))
                    {
                        ResetMaze();
                    }
                    break;

                case DemoState.Exploring:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "⏹ Stop"))
                    {
                        StopExploration();
                    }
                    break;

                case DemoState.Completed:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Play Again"))
                    {
                        ResetMaze();
                    }
                    break;

                case DemoState.Error:
                    if (GUI.Button(new Rect(startX, startY, btnWidth, btnHeight), "🔄 Retry"))
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
