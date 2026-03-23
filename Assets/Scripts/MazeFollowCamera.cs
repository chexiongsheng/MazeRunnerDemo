using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Fixed top-down overhead camera for the maze demo.
    /// Positions itself so the entire maze is visible within the left portion of the screen
    /// (leaving room for the chat panel on the right).
    /// Does NOT follow the player — the maze stays fixed on screen.
    /// </summary>
    public class MazeFollowCamera : MonoBehaviour
    {
        [Tooltip("The target to follow (legacy — kept for inspector reference but no longer used for positioning).")]
        public Transform target;

        [Tooltip("World-space offset from the target (legacy — not used when maze is found).")]
        public Vector3 offset = new Vector3(0f, 20f, 0f);

        [Tooltip("How smoothly the camera follows (legacy).")]
        public float smoothSpeed = 8f;

        [Tooltip("Fraction of screen width reserved for the maze view (0..1). The rest is for the chat panel.")]
        [Range(0.5f, 1f)]
        public float mazeViewWidthFraction = 0.7f;

        [Tooltip("Extra padding around the maze (in world units).")]
        public float padding = 2f;

        private bool positioned = false;

        private void Start()
        {
            // Set camera rotation to look straight down
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            PositionOverMaze();
        }

        /// <summary>
        /// Calculate camera position so the entire maze fits in the left portion of the screen.
        /// </summary>
        private void PositionOverMaze()
        {
            // Try to find the maze root to determine bounds
            var mazeRoot = GameObject.Find("Maze");
            if (mazeRoot == null)
            {
                // Fallback: use target (player) + offset if no Maze root found
                if (target != null)
                {
                    transform.position = target.position + offset;
                }
                return;
            }

            // Calculate maze bounds from all renderers under the Maze root
            var renderers = mazeRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                if (target != null)
                    transform.position = target.position + offset;
                return;
            }

            Bounds mazeBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                mazeBounds.Encapsulate(renderers[i].bounds);
            }

            // Maze center (XZ plane)
            Vector3 mazeCenter = mazeBounds.center;
            float mazeExtentX = mazeBounds.extents.x + padding;
            float mazeExtentZ = mazeBounds.extents.z + padding;

            var cam = GetComponent<Camera>();
            if (cam == null) return;

            // Switch to orthographic projection for a clean top-down view
            // (eliminates perspective distortion and uneven wall shading).
            cam.orthographic = true;

            float screenAspect = (float)Screen.width / Screen.height;

            // We want the maze to fit in the left `mazeViewWidthFraction` of the screen.
            // Orthographic size = half of the visible height.
            // Effective aspect for the maze portion:
            float effectiveAspect = screenAspect * mazeViewWidthFraction;

            // Size needed to fit maze vertically
            float sizeForZ = mazeExtentZ;

            // Size needed to fit maze horizontally within the left portion
            float sizeForX = mazeExtentX / effectiveAspect;

            float orthoSize = Mathf.Max(sizeForZ, sizeForX);
            cam.orthographicSize = orthoSize;

            // Offset the camera center so the maze appears in the left portion of the screen.
            // Visible width in world units = orthoSize * 2 * screenAspect
            float visibleWidth = 2f * orthoSize * screenAspect;
            float worldOffsetX = visibleWidth * (1f - mazeViewWidthFraction) / 2f;

            // Place camera above the maze center, shifted right so maze appears on the left
            float cameraHeight = mazeBounds.max.y + 10f; // comfortably above the tallest object
            transform.position = new Vector3(
                mazeCenter.x + worldOffsetX,
                cameraHeight,
                mazeCenter.z
            );

            positioned = true;
        }

        private void LateUpdate()
        {
            // Keep looking straight down
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // If not yet positioned (e.g., maze loaded later), try again
            if (!positioned)
            {
                PositionOverMaze();
            }
        }
    }
}
