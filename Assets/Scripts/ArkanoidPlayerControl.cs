using UnityEngine;

namespace MiniIT.ARKANOID
{
    public class ArkanoidPlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float movementSpeed = 10f;
        [SerializeField] private float padding = 0.05f;

        [Header("Camera Reference")]
        [SerializeField] private Camera mainCamera = null;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        [Header("Size Settings")]
        [SerializeField] [Range(10f, 50f)] private float screenWidthPercent = 30f;

        private float playerHalfWidth = 0f;
        private float minX = 0f;
        private float maxX = 0f;
        private float playerWidth = 0f;
        private bool boundsCalculated = false;
        private GameManager gameManager = null;
        private SpriteRenderer spriteRenderer = null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mainCamera = Camera.main;
            ResizePlayer();
        }

        private void Start()
        {
            FindCamera();
            CalculateBounds();
            gameManager = GameManager.Instance;
        }

        private void Update()
        {
            if (!boundsCalculated)
            {
                CalculateBounds();
            }

            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager != null && gameManager.IsInputLocked)
            {
                return;
            }

            if (boundsCalculated)
            {
                HandleInput();
            }
        }

        private void ResizePlayer()
        {
            if (spriteRenderer == null || mainCamera == null)
            {
                return;
            }

            float screenWidth = mainCamera.orthographicSize * 2f * mainCamera.aspect;
            float desiredWidth = screenWidth * (screenWidthPercent / 100f);
            float originalWidth = spriteRenderer.sprite.bounds.size.x;
            float scaleX = desiredWidth / originalWidth;

            transform.localScale = new Vector3(scaleX, transform.localScale.y, 1f);
            playerHalfWidth = desiredWidth * 0.5f;

            Debug.Log($"Player resized to {screenWidthPercent}% of screen ({desiredWidth:F2} units)");
        }

        private void FindCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                mainCamera = FindAnyObjectByType<Camera>();
            }

            if (mainCamera == null)
            {
                Debug.LogError("Camera not found!");
            }
        }

        private void CalculateBounds()
        {
            if (mainCamera == null || spriteRenderer == null)
            {
                return;
            }

            playerWidth = spriteRenderer.bounds.size.x * 0.5f;
            float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

            Vector2 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distanceFromCamera));
            Vector2 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distanceFromCamera));

            minX = bottomLeft.x + playerWidth + padding;
            maxX = topRight.x - playerWidth - padding;

            boundsCalculated = true;

            if (showDebugGizmos)
            {
                Debug.Log($"Bounds calculated: minX={minX:F2}, maxX={maxX:F2}, playerWidth={playerWidth:F2}");
            }
        }

        private void HandleInput()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                Vector3 touchWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, distanceFromCamera));
                MoveToPosition(touchWorldPos);
            }

#if UNITY_EDITOR
            if (Input.GetMouseButton(0))
            {
                float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));
                MoveToPosition(mouseWorldPos);
            }

            float horizontal = Input.GetAxis("Horizontal");

            if (!Mathf.Approximately(horizontal, 0f))
            {
                Vector3 newPos = transform.position + Vector3.right * horizontal * movementSpeed * Time.deltaTime;
                MoveToPosition(newPos);
            }
#endif
        }

        private void MoveToPosition(Vector3 targetPosition)
        {
            if (!boundsCalculated)
            {
                return;
            }

            float clampedX = Mathf.Clamp(targetPosition.x, minX, maxX);
            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(playerWidth * 2f, 0.5f, 0f));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(minX, transform.position.y - 2f, 0f), new Vector3(minX, transform.position.y + 2f, 0f));
            Gizmos.DrawLine(new Vector3(maxX, transform.position.y - 2f, 0f), new Vector3(maxX, transform.position.y + 2f, 0f));
        }

        private void OnRectTransformDimensionsChange()
        {
            if (mainCamera != null)
            {
                CalculateBounds();
            }
        }
    }
}