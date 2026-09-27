using UnityEngine;

namespace Game.Exploration
{
    [DisallowMultipleComponent]
    public class ExplorationCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 7f, -10f);
        [SerializeField] private Vector3 cameraEulerAngles = new Vector3(35f, 0f, 0f);
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;

        private Vector3 followVelocity;

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("Exploration camera requires a follow target.", this);
                enabled = false;
                return;
            }

            transform.position = target.position + followOffset;
            transform.rotation = Quaternion.Euler(cameraEulerAngles);
        }

        private void LateUpdate()
        {
            Vector3 destination = target.position + followOffset;
            transform.rotation = Quaternion.Euler(cameraEulerAngles);
            if (smoothTime <= 0f)
            {
                transform.position = destination;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, destination,
                ref followVelocity, smoothTime);
        }
    }
}
