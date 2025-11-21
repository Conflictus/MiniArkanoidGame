using UnityEngine;

namespace MiniIT.ARKANOID
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(AudioSource))]
    public class BallController : MonoBehaviour
    {
        [Header("Ball Settings")]
        [SerializeField] private float initialSpeed = 5f;
        [SerializeField] private float maxSpeed = 15f;
        [SerializeField] private float speedIncrease = 0.1f;
        [SerializeField] [Range(0.05f, 0.45f)] private float minVerticalRatio = 0.2f;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip bounceSound = null;
        [SerializeField] private AudioClip brickSound = null;
        [SerializeField] private AudioClip playerBounceSound = null;
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;

        [Header("Boundary Settings")]
        [SerializeField] private float verticalPadding = 0f;
        [SerializeField] private float horizontalPadding = 0f;

        [Header("References")]
        [SerializeField] private Transform playerTransform = null;

        private Rigidbody2D rb = null;
        private CircleCollider2D circleCollider = null;
        private AudioSource audioSource = null;
        private Camera mainCamera = null;
        private float minX = 0f;
        private float maxX = 0f;
        private float minY = 0f;
        private float maxY = 0f;
        private bool isLaunched = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = false;

            mainCamera = Camera.main;

            if (playerTransform == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

                if (playerObject != null)
                {
                    playerTransform = playerObject.transform;
                }
            }
        }

        private void Start()
        {
            CalculateBounds();
            ResetBall();
        }

        private void Update()
        {
            if (!isLaunched)
            {
                FollowPlayer();

                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                {
                    LaunchBall();
                }
            }
            else
            {
                MaintainSpeedAndBounds();
            }
        }

        public void LaunchBall()
        {
            if (isLaunched)
            {
                return;
            }

            Vector2 randomDirection = new Vector2(Random.Range(-0.5f, 0.5f), 1f).normalized;
            rb.linearVelocity = randomDirection * initialSpeed;
            isLaunched = true;
        }

        public void ResetBall()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();

                if (rb == null)
                {
                    Debug.LogWarning("BallController: Rigidbody2D missing, cannot reset ball.");
                    return;
                }
            }

            isLaunched = false;
            rb.linearVelocity = Vector2.zero;
            CalculateBounds();
        }

        private void FollowPlayer()
        {
            if (playerTransform == null)
            {
                return;
            }

            Vector3 playerPos = playerTransform.position;
            transform.position = new Vector3(playerPos.x, playerPos.y + 0.5f, playerPos.z);
        }

        private void CalculateBounds()
        {
            if (mainCamera == null || circleCollider == null)
            {
                return;
            }

            float ballRadius = circleCollider.bounds.extents.x;

            Vector2 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, mainCamera.nearClipPlane));
            Vector2 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, mainCamera.nearClipPlane));

            minX = bottomLeft.x + ballRadius + horizontalPadding;
            maxX = topRight.x - ballRadius - horizontalPadding;
            minY = bottomLeft.y + ballRadius + verticalPadding;
            maxY = topRight.y - ballRadius - verticalPadding;
        }

        private void MaintainSpeedAndBounds()
        {
            if (rb.linearVelocity.magnitude < maxSpeed)
            {
                float newMagnitude = rb.linearVelocity.magnitude + speedIncrease * Time.deltaTime;
                rb.linearVelocity = rb.linearVelocity.normalized * Mathf.Min(newMagnitude, maxSpeed);
            }

            EnsureVerticalComponent();
            ClampPosition();
        }

        private void ClampPosition()
        {
            Vector3 currentPos = transform.position;
            float clampedX = Mathf.Clamp(currentPos.x, minX, maxX);
            float clampedY = Mathf.Clamp(currentPos.y, minY, maxY);

            if (Mathf.Approximately(currentPos.x, clampedX) && Mathf.Approximately(currentPos.y, clampedY))
            {
                return;
            }

            transform.position = new Vector3(clampedX, clampedY, currentPos.z);

            if (!Mathf.Approximately(currentPos.x, clampedX))
            {
                rb.linearVelocity = new Vector2(-rb.linearVelocity.x, rb.linearVelocity.y);
            }

            if (!Mathf.Approximately(currentPos.y, clampedY))
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y);
            }

            EnsureVerticalComponent();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayBounceSound();

            if (collision.gameObject.CompareTag("Player"))
            {
                float hitFactor = CalculateHitFactor(transform.position, collision.transform);
                Vector2 direction = new Vector2(hitFactor, 1f).normalized;
                rb.linearVelocity = direction * rb.linearVelocity.magnitude;
                PlaySound(playerBounceSound);
            }
            else if (collision.gameObject.CompareTag("Brick"))
            {
                PlaySound(brickSound);
                Brick brick = collision.gameObject.GetComponent<Brick>();

                if (brick != null)
                {
                    brick.TakeDamage();
                }
            }
            else if (collision.gameObject.CompareTag("Wall"))
            {
                PlaySound(bounceSound);
            }

            EnsureVerticalComponent();
        }

        private float CalculateHitFactor(Vector2 ballPos, Transform platform)
        {
            float platformWidth = platform.localScale.x * circleCollider.bounds.size.x;
            float relativeHitPosition = (ballPos.x - platform.position.x) / platformWidth;
            return Mathf.Clamp(relativeHitPosition * 2f, -1f, 1f);
        }

        private void PlayBounceSound()
        {
            if (bounceSound == null || audioSource == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(bounceSound);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || audioSource == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip);
        }

        private void EnsureVerticalComponent()
        {
            Vector2 velocity = rb.linearVelocity;

            if (velocity == Vector2.zero)
            {
                return;
            }

            float minVertical = Mathf.Abs(velocity.magnitude) * minVerticalRatio;

            if (Mathf.Abs(velocity.y) >= minVertical)
            {
                return;
            }

            float ySign = Mathf.Sign(velocity.y);

            if (Mathf.Approximately(ySign, 0f))
            {
                ySign = Random.value > 0.5f ? 1f : -1f;
            }

            float newY = minVertical * ySign;
            float remainingSq = Mathf.Max(velocity.sqrMagnitude - newY * newY, 0.01f);
            float newX = Mathf.Sqrt(remainingSq) * Mathf.Sign(Mathf.Approximately(velocity.x, 0f) ? (Random.value > 0.5f ? 1f : -1f) : velocity.x);

            rb.linearVelocity = new Vector2(newX, newY);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("DeathZone"))
            {
                GameManager.Instance?.HandleBallLost();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            CalculateBounds();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                new Vector3(maxX - minX, maxY - minY, 0f));
        }
    }
}