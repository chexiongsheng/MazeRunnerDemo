using System;
using System.Collections;
using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Bridge class for maze player character control.
    /// Provides static methods callable from JS/TS via PuerTS to move and query the player.
    /// Uses absolute directions (north/south/east/west) for a fixed-camera setup.
    /// All methods use the callback pattern (Action&lt;string&gt;) to return JSON results.
    /// </summary>
    public static class MazePlayerBridge
    {
        private const string PlayerTag = "Player";
        private const float GoalReachThreshold = 1.5f;
        private const string GoalTag = "Goal";
        private const float CellSize = 2f; // Must match MazeSceneGenerator.cellSize

        /// <summary>
        /// Hidden MonoBehaviour singleton that drives coroutines for movement.
        /// </summary>
        private class MazePlayerRunner : MonoBehaviour
        {
            private static MazePlayerRunner _instance;

            public static MazePlayerRunner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("[MazePlayerRunner]");
                        go.hideFlags = HideFlags.HideAndDontSave;
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _instance = go.AddComponent<MazePlayerRunner>();
                    }
                    return _instance;
                }
            }

            public void RunMoveDirection(Transform player, CharacterController cc, Vector3 direction, float distance, Action<string> callback)
            {
                StartCoroutine(MoveDirectionCoroutine(player, cc, direction, distance, callback));
            }

            public void RunMoveSequence(Transform player, CharacterController cc, string[] directions, float distance, Action<string> callback)
            {
                // Convert single distance to per-step array
                float[] distances = new float[directions.Length];
                for (int i = 0; i < distances.Length; i++) distances[i] = distance;
                StartCoroutine(MoveSequenceCoroutine(player, cc, directions, distances, callback));
            }

            public void RunMoveSequenceV2(Transform player, CharacterController cc, string[] directions, float[] distances, Action<string> callback)
            {
                StartCoroutine(MoveSequenceCoroutine(player, cc, directions, distances, callback));
            }

            private IEnumerator MoveSequenceCoroutine(Transform player, CharacterController cc, string[] directions, float[] distances, Action<string> callback)
            {
                Vector3 startPos = player.position;
                int stepsCompleted = 0;
                bool blocked = false;
                bool reachedGoal = false;
                float totalDistanceMoved = 0f;

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3? dir = ParseDirection(directions[i]);
                    if (dir == null)
                    {
                        string errorResult = JsonUtility.ToJson(new MoveSequenceResult
                        {
                            success = false,
                            stepsRequested = directions.Length,
                            stepsCompleted = stepsCompleted,
                            blocked = false,
                            reachedGoal = false,
                            totalDistanceMoved = totalDistanceMoved,
                            position = FormatVector3(player.position),
                            message = $"Invalid direction '{directions[i]}' at step {i + 1}. Stopped after {stepsCompleted} steps."
                        });
                        callback?.Invoke(errorResult);
                        yield break;
                    }

                    // distances[i] is in grid cells, convert to meters
                    float stepDistanceMeters = distances[i] * CellSize;

                    // Rotate the player to face the movement direction
                    player.forward = dir.Value;

                    // Raycast to check for obstacles ahead
                    float rayDistance = stepDistanceMeters + 0.3f;
                    float actualMoveDistance = stepDistanceMeters;
                    bool stepBlocked = false;

                    if (Physics.Raycast(player.position + Vector3.up * 0.5f, dir.Value, out RaycastHit hit, rayDistance))
                    {
                        float distToObstacle = hit.distance - 0.3f;
                        if (distToObstacle < stepDistanceMeters)
                        {
                            stepBlocked = true;
                            // Snap to the nearest cell center before the wall
                            float cellsCanMove = Mathf.Floor(distToObstacle / CellSize);
                            actualMoveDistance = cellsCanMove * CellSize;
                        }
                    }

                    // Move in small steps for smooth movement
                    float moved = 0f;
                    float moveSpeed = 8f;
                    while (moved < actualMoveDistance)
                    {
                        float step = Mathf.Min(moveSpeed * Time.deltaTime, actualMoveDistance - moved);
                        if (cc != null)
                        {
                            cc.Move(dir.Value * step);
                        }
                        else
                        {
                            player.position += dir.Value * step;
                        }
                        moved += step;
                        yield return null;
                    }

                    // Snap to nearest cell center after movement
                    SnapToCellCenter(player);

                    totalDistanceMoved = Vector3.Distance(startPos, player.position);
                    stepsCompleted++;

                    // Check goal
                    reachedGoal = CheckGoalReached(player);
                    if (reachedGoal) break;

                    // If blocked, stop sequence
                    if (stepBlocked)
                    {
                        blocked = true;
                        break;
                    }

                    // Small pause between steps for visual clarity
                    yield return new WaitForSeconds(0.1f);
                }

                string result = JsonUtility.ToJson(new MoveSequenceResult
                {
                    success = true,
                    stepsRequested = directions.Length,
                    stepsCompleted = stepsCompleted,
                    blocked = blocked,
                    reachedGoal = reachedGoal,
                    totalDistanceMoved = Mathf.Round(totalDistanceMoved * 10f) / 10f,
                    position = FormatVector3(player.position),
                    message = reachedGoal
                        ? $"Reached the goal after {stepsCompleted} steps! Maze completed!"
                        : blocked
                            ? $"Completed {stepsCompleted}/{directions.Length} steps. Blocked by a wall on step {stepsCompleted} (direction: {directions[stepsCompleted - 1]})."
                            : $"Completed all {stepsCompleted} steps successfully."
                });

                callback?.Invoke(result);
            }

            private IEnumerator MoveDirectionCoroutine(Transform player, CharacterController cc, Vector3 direction, float distance, Action<string> callback)
            {
                Vector3 startPos = player.position;

                // Raycast to check for obstacles ahead
                float rayDistance = distance + 0.3f;
                bool blocked = false;
                float actualMoveDistance = distance;

                if (Physics.Raycast(player.position + Vector3.up * 0.5f, direction, out RaycastHit hit, rayDistance))
                {
                    float distToObstacle = hit.distance - 0.3f;
                    if (distToObstacle < distance)
                    {
                        blocked = true;
                        actualMoveDistance = Mathf.Max(0f, distToObstacle);
                    }
                }

                // Move in small steps for smooth movement
                float moved = 0f;
                float moveSpeed = 5f;
                while (moved < actualMoveDistance)
                {
                    float step = Mathf.Min(moveSpeed * Time.deltaTime, actualMoveDistance - moved);
                    if (cc != null)
                    {
                        cc.Move(direction * step);
                    }
                    else
                    {
                        player.position += direction * step;
                    }
                    moved += step;
                    yield return null;
                }

                Vector3 endPos = player.position;
                bool reachedGoal = CheckGoalReached(player);

                string result = JsonUtility.ToJson(new MoveResult
                {
                    success = !blocked || actualMoveDistance > 0.1f,
                    blocked = blocked,
                    distanceMoved = Vector3.Distance(startPos, endPos),
                    position = FormatVector3(endPos),
                    reachedGoal = reachedGoal,
                    message = blocked
                        ? (actualMoveDistance > 0.1f
                            ? $"Partially moved {actualMoveDistance:F1}m before hitting a wall."
                            : "Blocked by a wall. Cannot move in this direction.")
                        : $"Moved {actualMoveDistance:F1}m successfully."
                });

                callback?.Invoke(result);
            }
        }

        // --- JSON result structures ---

        [Serializable]
        private class MoveResult
        {
            public bool success;
            public bool blocked;
            public float distanceMoved;
            public string position;
            public bool reachedGoal;
            public string message;
        }

        [Serializable]
        private class MoveSequenceResult
        {
            public bool success;
            public int stepsRequested;
            public int stepsCompleted;
            public bool blocked;
            public bool reachedGoal;
            public float totalDistanceMoved;
            public string position;
            public string message;
        }

        [Serializable]
        private class PlayerStatus
        {
            public bool success;
            public string position;
            public float northDistance;
            public float southDistance;
            public float eastDistance;
            public float westDistance;
            public bool reachedGoal;
            public string message;
        }

        [Serializable]
        private class ErrorResult
        {
            public bool success;
            public string error;
        }

        // --- Direction helpers ---

        private static readonly Vector3 DirNorth = Vector3.forward;   // +Z
        private static readonly Vector3 DirSouth = Vector3.back;      // -Z
        private static readonly Vector3 DirEast = Vector3.right;      // +X
        private static readonly Vector3 DirWest = Vector3.left;       // -X

        private static Vector3? ParseDirection(string dirName)
        {
            if (string.IsNullOrEmpty(dirName)) return null;
            switch (dirName.ToLower().Trim())
            {
                case "north": case "n": return DirNorth;
                case "south": case "s": return DirSouth;
                case "east": case "e": return DirEast;
                case "west": case "w": return DirWest;
                default: return null;
            }
        }

        // --- Public static methods (callable from JS) ---

        /// <summary>
        /// Move the player along a sequence of directions (multi-step path).
        /// Stops early if blocked by a wall or if the goal is reached.
        /// </summary>
        /// <param name="directionsJson">JSON array of direction strings, e.g. ["north","north","east"]</param>
        /// <param name="distance">Distance per step in meters (0.5 ~ 20.0)</param>
        /// <param name="callback">Callback invoked with JSON result</param>
        public static void MoveSequence(string directionsJson, float distance, Action<string> callback)
        {
            if (callback == null) return;

            if (string.IsNullOrEmpty(directionsJson))
            {
                callback.Invoke(BuildErrorJson("directionsJson is null or empty."));
                return;
            }

            // Parse JSON array manually (Unity's JsonUtility doesn't handle raw arrays well)
            string[] directions;
            try
            {
                // Simple JSON array parser: ["north","east","north"]
                directionsJson = directionsJson.Trim();
                if (!directionsJson.StartsWith("[") || !directionsJson.EndsWith("]"))
                {
                    callback.Invoke(BuildErrorJson("directionsJson must be a JSON array, e.g. [\"north\",\"east\"]."));
                    return;
                }

                string inner = directionsJson.Substring(1, directionsJson.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                {
                    callback.Invoke(BuildErrorJson("Direction array is empty."));
                    return;
                }

                string[] parts = inner.Split(',');
                directions = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    directions[i] = parts[i].Trim().Trim('"').Trim('\'');
                }
            }
            catch (Exception e)
            {
                callback.Invoke(BuildErrorJson($"Failed to parse directionsJson: {e.Message}"));
                return;
            }

            if (directions.Length == 0)
            {
                callback.Invoke(BuildErrorJson("Direction array is empty."));
                return;
            }

            if (directions.Length > 20)
            {
                callback.Invoke(BuildErrorJson("Too many steps in sequence (max 20). Plan shorter paths and re-observe."));
                return;
            }

            var (player, cc, error) = FindPlayer();
            if (player == null)
            {
                callback.Invoke(BuildErrorJson(error));
                return;
            }

            if (!Application.isPlaying)
            {
                callback.Invoke(BuildErrorJson("MoveSequence is only available in Play Mode."));
                return;
            }

            MazePlayerRunner.Instance.RunMoveSequence(player, cc, directions, distance, callback);
        }

        /// <summary>
        /// Move the player along a sequence of directions with per-step distances.
        /// Each step can have its own distance.
        /// </summary>
        /// <param name="directionsJson">JSON array of direction strings, e.g. ["north","east"]</param>
        /// <param name="distancesJson">JSON array of distances in grid cells, e.g. [3, 2]</param>
        /// <param name="callback">Callback invoked with JSON result</param>
        public static void MoveSequenceV2(string directionsJson, string distancesJson, Action<string> callback)
        {
            if (callback == null) return;

            if (string.IsNullOrEmpty(directionsJson))
            {
                callback.Invoke(BuildErrorJson("directionsJson is null or empty."));
                return;
            }

            // Parse directions JSON array
            string[] directions;
            try
            {
                directionsJson = directionsJson.Trim();
                if (!directionsJson.StartsWith("[") || !directionsJson.EndsWith("]"))
                {
                    callback.Invoke(BuildErrorJson("directionsJson must be a JSON array."));
                    return;
                }
                string inner = directionsJson.Substring(1, directionsJson.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                {
                    callback.Invoke(BuildErrorJson("Direction array is empty."));
                    return;
                }
                string[] parts = inner.Split(',');
                directions = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    directions[i] = parts[i].Trim().Trim('"').Trim('\'');
                }
            }
            catch (Exception e)
            {
                callback.Invoke(BuildErrorJson($"Failed to parse directionsJson: {e.Message}"));
                return;
            }

            // Parse distances JSON array
            float[] distances;
            try
            {
                distancesJson = distancesJson.Trim();
                if (!distancesJson.StartsWith("[") || !distancesJson.EndsWith("]"))
                {
                    callback.Invoke(BuildErrorJson("distancesJson must be a JSON array."));
                    return;
                }
                string inner = distancesJson.Substring(1, distancesJson.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                {
                    callback.Invoke(BuildErrorJson("Distances array is empty."));
                    return;
                }
                string[] parts = inner.Split(',');
                distances = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out distances[i]))
                    {
                        callback.Invoke(BuildErrorJson($"Invalid distance value '{parts[i].Trim()}' at index {i}."));
                        return;
                    }
                    if (distances[i] < 1 || distances[i] > 10)
                    {
                        callback.Invoke(BuildErrorJson($"Distance at index {i} must be between 1 and 10 grid cells (got {distances[i]})."));
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                callback.Invoke(BuildErrorJson($"Failed to parse distancesJson: {e.Message}"));
                return;
            }

            if (directions.Length != distances.Length)
            {
                callback.Invoke(BuildErrorJson($"directions length ({directions.Length}) must match distances length ({distances.Length})."));
                return;
            }

            if (directions.Length == 0)
            {
                callback.Invoke(BuildErrorJson("Direction array is empty."));
                return;
            }

            if (directions.Length > 20)
            {
                callback.Invoke(BuildErrorJson("Too many steps in sequence (max 20)."));
                return;
            }

            var (player, cc, error) = FindPlayer();
            if (player == null)
            {
                callback.Invoke(BuildErrorJson(error));
                return;
            }

            if (!Application.isPlaying)
            {
                callback.Invoke(BuildErrorJson("MoveSequenceV2 is only available in Play Mode."));
                return;
            }

            MazePlayerRunner.Instance.RunMoveSequenceV2(player, cc, directions, distances, callback);
        }

        /// <summary>
        /// Move the player in an absolute direction (north/south/east/west).
        /// </summary>
        /// <param name="direction">Direction name: "north", "south", "east", or "west"</param>
        /// <param name="distance">Distance in meters (0.5 ~ 5.0)</param>
        /// <param name="callback">Callback invoked with JSON result</param>
        public static void MoveDirection(string direction, float distance, Action<string> callback)
        {
            if (callback == null) return;

            Vector3? dir = ParseDirection(direction);
            if (dir == null)
            {
                callback.Invoke(BuildErrorJson($"Invalid direction '{direction}'. Use 'north', 'south', 'east', or 'west'."));
                return;
            }

            var (player, cc, error) = FindPlayer();
            if (player == null)
            {
                callback.Invoke(BuildErrorJson(error));
                return;
            }

            if (!Application.isPlaying)
            {
                callback.Invoke(BuildErrorJson("MoveDirection is only available in Play Mode."));
                return;
            }

            // Rotate the player to face the movement direction (visual feedback)
            player.forward = dir.Value;

            MazePlayerRunner.Instance.RunMoveDirection(player, cc, dir.Value, distance, callback);
        }

        /// <summary>
        /// Move the player forward along its current facing direction (legacy support).
        /// </summary>
        public static void MoveForward(float distance, Action<string> callback)
        {
            if (callback == null) return;

            var (player, cc, error) = FindPlayer();
            if (player == null)
            {
                callback.Invoke(BuildErrorJson(error));
                return;
            }

            if (!Application.isPlaying)
            {
                callback.Invoke(BuildErrorJson("MoveForward is only available in Play Mode."));
                return;
            }

            MazePlayerRunner.Instance.RunMoveDirection(player, cc, player.forward, distance, callback);
        }

        /// <summary>
        /// Get the player's current status including position and obstacle distances in all 4 directions.
        /// </summary>
        /// <param name="callback">Callback invoked with JSON result</param>
        public static void GetPlayerStatus(Action<string> callback)
        {
            if (callback == null) return;

            var (player, _, error) = FindPlayer();
            if (player == null)
            {
                callback.Invoke(BuildErrorJson(error));
                return;
            }

            Vector3 pos = player.position;
            Vector3 rayOrigin = pos + Vector3.up * 0.5f;

            // Raycast in all 4 absolute directions, return distances in grid cells
            float northDist = RaycastDistance(rayOrigin, DirNorth) / CellSize;
            float southDist = RaycastDistance(rayOrigin, DirSouth) / CellSize;
            float eastDist = RaycastDistance(rayOrigin, DirEast) / CellSize;
            float westDist = RaycastDistance(rayOrigin, DirWest) / CellSize;

            bool reachedGoal = CheckGoalReached(player);

            callback.Invoke(JsonUtility.ToJson(new PlayerStatus
            {
                success = true,
                position = FormatVector3(pos),
                northDistance = Mathf.Round(northDist * 10f) / 10f,
                southDistance = Mathf.Round(southDist * 10f) / 10f,
                eastDistance = Mathf.Round(eastDist * 10f) / 10f,
                westDistance = Mathf.Round(westDist * 10f) / 10f,
                reachedGoal = reachedGoal,
                message = reachedGoal
                    ? "You have reached the goal! Maze completed!"
                    : $"Position: {FormatVector3(pos)}. Cell: ({Mathf.FloorToInt(pos.x / CellSize)},{Mathf.FloorToInt(pos.z / CellSize)}). " +
                      $"North: {northDist:F1} cells, South: {southDist:F1} cells, East: {eastDist:F1} cells, West: {westDist:F1} cells."
            }));
        }

        // --- Internal helpers ---

        private static (Transform player, CharacterController cc, string error) FindPlayer()
        {
            GameObject playerObj = GameObject.FindWithTag(PlayerTag);
            if (playerObj == null)
            {
                return (null, null, $"No GameObject with tag '{PlayerTag}' found in the scene. Make sure the player is tagged correctly.");
            }

            CharacterController cc = playerObj.GetComponent<CharacterController>();
            return (playerObj.transform, cc, null);
        }

        private static bool CheckGoalReached(Transform player)
        {
            // Method 1: Check by tag
            GameObject goal = GameObject.FindWithTag(GoalTag);
            if (goal != null)
            {
                float dist = Vector3.Distance(player.position, goal.transform.position);
                return dist < GoalReachThreshold;
            }

            // Method 2: Check trigger via a flag set by OnTriggerEnter
            var goalDetector = player.GetComponent<MazeGoalDetector>();
            if (goalDetector != null)
            {
                return goalDetector.HasReachedGoal;
            }

            return false;
        }

        private static float RaycastDistance(Vector3 origin, Vector3 direction, float maxDist = 20f)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist))
            {
                return hit.distance;
            }
            return maxDist;
        }

        private static string FormatVector3(Vector3 v)
        {
            return $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
        }

        /// <summary>
        /// Snap the player position to the nearest cell center on the XZ plane.
        /// Cell centers are at (cellSize/2 + x*cellSize, y, cellSize/2 + z*cellSize).
        /// </summary>
        private static void SnapToCellCenter(Transform player)
        {
            Vector3 pos = player.position;
            float snappedX = Mathf.Round((pos.x - CellSize / 2f) / CellSize) * CellSize + CellSize / 2f;
            float snappedZ = Mathf.Round((pos.z - CellSize / 2f) / CellSize) * CellSize + CellSize / 2f;
            player.position = new Vector3(snappedX, pos.y, snappedZ);
        }

        private static string BuildErrorJson(string message)
        {
            return JsonUtility.ToJson(new ErrorResult
            {
                success = false,
                error = message
            });
        }
    }

    /// <summary>
    /// Attach this component to the Player to detect goal trigger collisions.
    /// The goal object should have a Collider with isTrigger = true and tag "Goal".
    /// </summary>
    public class MazeGoalDetector : MonoBehaviour
    {
        public bool HasReachedGoal { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Goal"))
            {
                HasReachedGoal = true;
                Debug.Log("[MazeGoalDetector] Player reached the goal!");
            }
        }

        /// <summary>
        /// Reset the goal detection state (e.g. for replaying the maze).
        /// </summary>
        public void ResetGoal()
        {
            HasReachedGoal = false;
        }
    }
}
