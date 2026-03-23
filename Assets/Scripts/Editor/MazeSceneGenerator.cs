using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LLMAgent.Editor
{
    /// <summary>
    /// Editor utility to automatically generate a maze scene with all required components.
    /// Access via menu: Tools > Maze Runner > Generate Maze Scene
    /// </summary>
    public class MazeSceneGenerator : EditorWindow
    {
        private int mazeWidth = 8;
        private int mazeHeight = 8;
        private float cellSize = 2f;
        private float wallHeight = 1.2f;
        private float wallThickness = 0.2f;

        [MenuItem("Tools/Maze Runner/Generate Maze Scene")]
        public static void ShowWindow()
        {
            GetWindow<MazeSceneGenerator>("Maze Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Maze Scene Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            mazeWidth = EditorGUILayout.IntSlider("Maze Width", mazeWidth, 4, 16);
            mazeHeight = EditorGUILayout.IntSlider("Maze Height", mazeHeight, 4, 16);
            cellSize = EditorGUILayout.Slider("Cell Size (m)", cellSize, 1.5f, 4f);
            wallHeight = EditorGUILayout.Slider("Wall Height (m)", wallHeight, 0.5f, 5f);
            wallThickness = EditorGUILayout.Slider("Wall Thickness (m)", wallThickness, 0.1f, 0.5f);

            GUILayout.Space(10);

            if (GUILayout.Button("Generate Maze Scene", GUILayout.Height(40)))
            {
                GenerateMazeScene();
            }

            GUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "This will create a new scene with:\n" +
                "• A procedural maze using recursive backtracking\n" +
                "• Player with CharacterController at start\n" +
                "• Goal marker at maze exit\n" +
                "• Third-person follow camera\n" +
                "• MazeDemoManager and MazeAgentUI components\n" +
                "• Directional light",
                MessageType.Info);
        }

        private void GenerateMazeScene()
        {
            // Ensure "Goal" tag exists in Tag Manager
            EnsureTagExists("Goal");

            // Create a new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Root container ---
            var mazeRoot = new GameObject("Maze");

            // --- Generate maze grid ---
            bool[,] visited;
            bool[,,] walls; // walls[x, y, direction]: 0=North, 1=East, 2=South, 3=West
            GenerateMazeData(mazeWidth, mazeHeight, out visited, out walls);

            // --- Build floor ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.parent = mazeRoot.transform;
            float totalWidth = mazeWidth * cellSize;
            float totalHeight = mazeHeight * cellSize;
            floor.transform.position = new Vector3(totalWidth / 2f, 0, totalHeight / 2f);
            floor.transform.localScale = new Vector3(totalWidth / 10f, 1, totalHeight / 10f);

            // Set floor material
            var floorRenderer = floor.GetComponent<Renderer>();
            var floorMat = new Material(Shader.Find("Standard"));
            floorMat.color = new Color(0.55f, 0.55f, 0.6f, 1f); // Medium-dark floor for grid line contrast
            floorRenderer.sharedMaterial = floorMat;

            // --- Build grid lines (green lines showing cell boundaries) ---
            var gridParent = new GameObject("GridLines");
            gridParent.transform.parent = mazeRoot.transform;

            var gridMat = new Material(Shader.Find("Unlit/Color"));
            gridMat.color = new Color(0.1f, 0.9f, 0.1f, 1f); // Bright green (Unlit, not affected by lighting)

            float gridY = 0.03f; // Above floor to avoid z-fighting
            float gridLineWidth = 0.05f; // Visible thin lines

            // Vertical grid lines (along Z axis, spaced by cellSize along X)
            for (int i = 0; i <= mazeWidth; i++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"GridLine_V_{i}";
                line.transform.parent = gridParent.transform;
                line.transform.position = new Vector3(i * cellSize, gridY, totalHeight / 2f);
                line.transform.localScale = new Vector3(gridLineWidth, 0.01f, totalHeight);
                line.GetComponent<Renderer>().sharedMaterial = gridMat;
                DestroyImmediate(line.GetComponent<Collider>()); // No collision
                line.isStatic = true;
            }

            // Horizontal grid lines (along X axis, spaced by cellSize along Z)
            for (int j = 0; j <= mazeHeight; j++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"GridLine_H_{j}";
                line.transform.parent = gridParent.transform;
                line.transform.position = new Vector3(totalWidth / 2f, gridY, j * cellSize);
                line.transform.localScale = new Vector3(totalWidth, 0.01f, gridLineWidth);
                line.GetComponent<Renderer>().sharedMaterial = gridMat;
                DestroyImmediate(line.GetComponent<Collider>()); // No collision
                line.isStatic = true;
            }

            // --- Build walls ---
            var wallsParent = new GameObject("Walls");
            wallsParent.transform.parent = mazeRoot.transform;

            var wallMat = new Material(Shader.Find("Standard"));
            wallMat.color = new Color(0.4f, 0.35f, 0.3f, 1f);

            int wallCount = 0;

            for (int x = 0; x < mazeWidth; x++)
            {
                for (int y = 0; y < mazeHeight; y++)
                {
                    float cx = x * cellSize + cellSize / 2f;
                    float cy = y * cellSize + cellSize / 2f;

                    // North wall (z+)
                    if (walls[x, y, 0])
                    {
                        CreateWall(wallsParent.transform, wallMat,
                            new Vector3(cx, wallHeight / 2f, cy + cellSize / 2f),
                            new Vector3(cellSize + wallThickness, wallHeight, wallThickness),
                            $"Wall_N_{x}_{y}");
                        wallCount++;
                    }

                    // East wall (x+)
                    if (walls[x, y, 1])
                    {
                        CreateWall(wallsParent.transform, wallMat,
                            new Vector3(cx + cellSize / 2f, wallHeight / 2f, cy),
                            new Vector3(wallThickness, wallHeight, cellSize + wallThickness),
                            $"Wall_E_{x}_{y}");
                        wallCount++;
                    }

                    // South wall (z-) — only for y == 0 boundary
                    if (y == 0 && walls[x, y, 2])
                    {
                        CreateWall(wallsParent.transform, wallMat,
                            new Vector3(cx, wallHeight / 2f, cy - cellSize / 2f),
                            new Vector3(cellSize + wallThickness, wallHeight, wallThickness),
                            $"Wall_S_{x}_{y}");
                        wallCount++;
                    }

                    // West wall (x-) — only for x == 0 boundary
                    if (x == 0 && walls[x, y, 3])
                    {
                        CreateWall(wallsParent.transform, wallMat,
                            new Vector3(cx - cellSize / 2f, wallHeight / 2f, cy),
                            new Vector3(wallThickness, wallHeight, cellSize + wallThickness),
                            $"Wall_W_{x}_{y}");
                        wallCount++;
                    }
                }
            }

            // --- Player ---
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(cellSize / 2f, 0.05f, cellSize / 2f);
            player.transform.rotation = Quaternion.Euler(0, 0, 0); // Facing North (z+)

            // Visual representation (capsule)
            var playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerVisual.name = "PlayerVisual";
            playerVisual.transform.parent = player.transform;
            playerVisual.transform.localPosition = new Vector3(0, 1f, 0);
            playerVisual.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            DestroyImmediate(playerVisual.GetComponent<Collider>()); // Remove collider from visual

            var playerMat = new Material(Shader.Find("Standard"));
            playerMat.color = new Color(0.2f, 0.6f, 1f, 1f); // Blue
            playerVisual.GetComponent<Renderer>().sharedMaterial = playerMat;

            // Direction indicator (small cube pointing forward)
            var dirIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dirIndicator.name = "DirectionIndicator";
            dirIndicator.transform.parent = player.transform;
            dirIndicator.transform.localPosition = new Vector3(0, 1.5f, 0.4f);
            dirIndicator.transform.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            DestroyImmediate(dirIndicator.GetComponent<Collider>());
            var dirMat = new Material(Shader.Find("Standard"));
            dirMat.color = Color.yellow;
            dirIndicator.GetComponent<Renderer>().sharedMaterial = dirMat;

            // CharacterController
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 1f, 0);

            // MazeGoalDetector
            player.AddComponent<MazeGoalDetector>();

            // --- Goal ---
            var goal = new GameObject("Goal");
            goal.tag = "Goal";
            // Place goal at the far corner (top-right)
            goal.transform.position = new Vector3(
                (mazeWidth - 0.5f) * cellSize,
                0f,
                (mazeHeight - 0.5f) * cellSize
            );

            // Goal material: bright RED with emission (highly visible)
            var goalMat = new Material(Shader.Find("Standard"));
            goalMat.color = new Color(1f, 0.1f, 0.1f, 1f); // Bright Red
            goalMat.EnableKeyword("_EMISSION");
            goalMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f, 1f) * 3f);

            // Goal pillar: red cylinder, slightly taller than walls for visibility
            var goalPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goalPillar.name = "GoalPillar";
            goalPillar.transform.parent = goal.transform;
            goalPillar.transform.localPosition = new Vector3(0, wallHeight * 0.5f, 0);
            goalPillar.transform.localScale = new Vector3(0.5f, wallHeight * 0.5f, 0.5f);
            DestroyImmediate(goalPillar.GetComponent<Collider>());
            goalPillar.GetComponent<Renderer>().sharedMaterial = goalMat;

            // Goal floating sphere: red sphere on top of pillar
            var goalSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goalSphere.name = "GoalSphere";
            goalSphere.transform.parent = goal.transform;
            goalSphere.transform.localPosition = new Vector3(0, wallHeight + 0.5f, 0); // Above pillar
            goalSphere.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            DestroyImmediate(goalSphere.GetComponent<Collider>());
            goalSphere.GetComponent<Renderer>().sharedMaterial = goalMat;

            // Goal ground ring: flat red cylinder as a "glow ring" on the floor
            var goalRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goalRing.name = "GoalGroundRing";
            goalRing.transform.parent = goal.transform;
            goalRing.transform.localPosition = new Vector3(0, 0.02f, 0);
            goalRing.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            DestroyImmediate(goalRing.GetComponent<Collider>());
            var ringMat = new Material(Shader.Find("Standard"));
            ringMat.color = new Color(1f, 0.2f, 0.1f, 1f);
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.05f, 1f) * 2f);
            goalRing.GetComponent<Renderer>().sharedMaterial = ringMat;

            // Goal trigger collider
            var goalTrigger = goal.AddComponent<BoxCollider>();
            goalTrigger.isTrigger = true;
            goalTrigger.size = new Vector3(cellSize * 0.8f, 3f, cellSize * 0.8f);
            goalTrigger.center = new Vector3(0, 1.5f, 0);

            // --- Camera (fixed top-down overhead view showing entire maze) ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60;
            camGo.AddComponent<AudioListener>();

            // Add fixed camera script — positions itself to show the full maze
            // in the left portion of the screen (right side is reserved for chat panel)
            var followCam = camGo.AddComponent<MazeFollowCamera>();
            followCam.target = player.transform; // Kept as fallback reference
            followCam.offset = new Vector3(0f, 20f, 0f);
            followCam.mazeViewWidthFraction = 0.7f;
            followCam.padding = 2f;

            // --- Lighting (straight down, no shadows for clean top-down view) ---
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 1f, 1f, 1f);
            light.intensity = 0.8f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(90, 0, 0); // Straight down — no shadows

            // Ambient light (brighter to compensate for no shadows)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f, 1f);

            // --- Manager & UI ---
            var managerGo = new GameObject("MazeDemoManager");
            var manager = managerGo.AddComponent<MazeDemoManager>();

            var uiGo = new GameObject("MazeAgentUI");
            var agentUI = uiGo.AddComponent<MazeAgentUI>();
            agentUI.playerTransform = player.transform;

            manager.agentUI = agentUI;

            // --- Save scene ---
            string scenePath = "Assets/Scenes/MazeDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[MazeSceneGenerator] Maze scene generated and saved to {scenePath}");
            Debug.Log($"[MazeSceneGenerator] Maze: {mazeWidth}x{mazeHeight}, {wallCount} wall segments, Cell size: {cellSize}m");

            EditorUtility.DisplayDialog("Maze Generator",
                $"Maze scene generated successfully!\n\n" +
                $"Size: {mazeWidth}×{mazeHeight}\n" +
                $"Wall segments: {wallCount}\n" +
                $"Cell size: {cellSize}m\n\n" +
                $"Saved to: {scenePath}\n\n" +
                "Remember to set your API key in MazeDemoManager inspector.",
                "OK");
        }

        private void CreateWall(Transform parent, Material mat, Vector3 position, Vector3 scale, string name)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = parent;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
            wall.isStatic = true;
        }

        /// <summary>
        /// Generate maze data using recursive backtracking (DFS) algorithm.
        /// </summary>
        private void GenerateMazeData(int width, int height, out bool[,] visited, out bool[,,] walls)
        {
            visited = new bool[width, height];
            // walls[x, y, dir]: true = wall exists. dir: 0=N, 1=E, 2=S, 3=W
            walls = new bool[width, height, 4];

            // Initialize all walls
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        walls[x, y, d] = true;
                    }
                }
            }

            // DFS maze generation
            var stack = new System.Collections.Generic.Stack<(int x, int y)>();
            int startX = 0, startY = 0;
            visited[startX, startY] = true;
            stack.Push((startX, startY));

            // Direction vectors: N(0,1), E(1,0), S(0,-1), W(-1,0)
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { 1, 0, -1, 0 };
            int[] opposite = { 2, 3, 0, 1 };

            var rand = new System.Random(42); // Fixed seed for reproducibility

            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Peek();

                // Find unvisited neighbors
                var neighbors = new System.Collections.Generic.List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny])
                    {
                        neighbors.Add(d);
                    }
                }

                if (neighbors.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                // Pick random neighbor
                int chosenDir = neighbors[rand.Next(neighbors.Count)];
                int newX = cx + dx[chosenDir];
                int newY = cy + dy[chosenDir];

                // Remove wall between current and chosen neighbor
                walls[cx, cy, chosenDir] = false;
                walls[newX, newY, opposite[chosenDir]] = false;

                visited[newX, newY] = true;
                stack.Push((newX, newY));
            }
        }

        /// <summary>
        /// Ensure a tag exists in the Unity Tag Manager. Adds it if not already defined.
        /// </summary>
        private void EnsureTagExists(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            // Check if tag already exists
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return; // Tag already exists
            }

            // Add the new tag
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();

            Debug.Log($"[MazeSceneGenerator] Added tag '{tag}' to Tag Manager.");
        }
    }
}
