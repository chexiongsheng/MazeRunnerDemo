using UnityEngine;

namespace LLMAgent
{
    /// <summary>
    /// Top-down overhead camera for the maze demo.
    /// Follows the player position from directly above — does NOT rotate with the player.
    /// This provides a clear bird's-eye view so the AI can better recognize maze paths.
    /// </summary>
    public class MazeFollowCamera : MonoBehaviour
    {
        [Tooltip("The target to follow.")]
        public Transform target;

        [Tooltip("World-space offset from the target (fixed direction, not relative to target rotation).")]
        public Vector3 offset = new Vector3(0f, 20f, 0f);

        [Tooltip("How smoothly the camera follows.")]
        public float smoothSpeed = 8f;

        private void Start()
        {
            // Set camera rotation to look straight down
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Fixed world-space offset — camera is directly above the player
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Keep looking straight down (no LookAt to avoid gimbal lock)
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
