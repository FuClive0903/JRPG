using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Exploration
{
    [DisallowMultipleComponent]
    public class ExplorationPlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private Sprite playerSprite;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Vector2 input;
        private Vector2 lastPosition;

        public bool InputEnabled { get; private set; } = true;
        public event Action<float> HorizontalDistanceMoved;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();
            if (body == null)
            {
                Debug.LogError("Exploration player needs Rigidbody2D. Remove conflicting 3D physics components.", this);
                enabled = false;
                return;
            }
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D feet = gameObject.AddComponent<CircleCollider2D>();
                feet.radius = 0.07f;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("Exploration player needs SpriteRenderer. Remove conflicting 3D mesh components.", this);
                enabled = false;
                return;
            }
            spriteRenderer.sprite = playerSprite;
            transform.localScale = Vector3.one * 6f;
            lastPosition = body.position;
        }

        private void OnEnable()
        {
            if (moveAction == null)
            {
                Debug.LogError("Exploration movement requires a Move input action.", this);
                return;
            }

            moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null)
                moveAction.action.Disable();
            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            input = InputEnabled && moveAction != null
                ? Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f)
                : Vector2.zero;

            Vector2 currentPosition = body.position;
            if (InputEnabled && input.sqrMagnitude > Mathf.Epsilon)
            {
                float distance = Vector2.Distance(currentPosition, lastPosition);
                if (distance > Mathf.Epsilon)
                    HorizontalDistanceMoved?.Invoke(distance);
            }
            lastPosition = currentPosition;
            spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
        }

        private void FixedUpdate()
        {
            body.linearVelocity = input * moveSpeed;
        }

        public void SetInputEnabled(bool inputEnabled)
        {
            InputEnabled = inputEnabled;
            if (!inputEnabled)
            {
                input = Vector2.zero;
                body.linearVelocity = Vector2.zero;
            }
            lastPosition = body.position;
        }

        public void ResetTrackedPosition()
        {
            if (body != null)
                lastPosition = body.position;
        }
    }
}
