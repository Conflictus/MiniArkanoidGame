using System.Collections;
using UnityEngine;

namespace MiniIT.ARKANOID
{
    public class Brick : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private int hitPoints = 1;

        [Header("Visuals")]
        [SerializeField] private bool autoOutline = true;
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] [Range(0f, 0.4f)] private float outlineThickness = 0.08f;
        [SerializeField] private AnimationCurve destroyScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private float destroyAnimationDuration = 0.15f;
        [SerializeField] private float hitFlashDuration = 0.08f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private GameObject destroyEffect = null;

        private SpriteRenderer spriteRenderer = null;
        private Color baseColor;
        private bool isDestroying = false;
        private GameManager owner = null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            if (autoOutline)
            {
                CreateOutline();
            }
        }

        private void Start()
        {
            owner = GameManager.Instance;

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }
        }

        public void TakeDamage()
        {
            if (isDestroying)
            {
                return;
            }

            hitPoints--;

            if (hitPoints <= 0)
            {
                StartCoroutine(DestroySequence());
            }
            else if (spriteRenderer != null)
            {
                StopCoroutine(nameof(HitFlash));
                StartCoroutine(nameof(HitFlash));
            }
        }

        private IEnumerator HitFlash()
        {
            float time = 0f;

            while (time < hitFlashDuration)
            {
                time += Time.deltaTime;
                float progress = Mathf.Clamp01(time / hitFlashDuration);
                spriteRenderer.color = Color.Lerp(hitFlashColor, baseColor, progress);
                yield return null;
            }

            spriteRenderer.color = baseColor;
        }

        private IEnumerator DestroySequence()
        {
            isDestroying = true;

            if (destroyEffect != null)
            {
                Instantiate(destroyEffect, transform.position, Quaternion.identity);
            }

            Vector3 originalScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < destroyAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / destroyAnimationDuration);
                float scaleFactor = destroyScaleCurve.Evaluate(progress);
                transform.localScale = originalScale * scaleFactor;
                yield return null;
            }

            owner?.NotifyBrickDestroyed(this);
            owner = null;
            Destroy(gameObject);
        }

        private void CreateOutline()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();

            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            GameObject outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localScale = Vector3.one * (1f + outlineThickness);

            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = renderer.sprite;
            outlineRenderer.color = outlineColor;
            outlineRenderer.sortingLayerID = renderer.sortingLayerID;
            outlineRenderer.sortingOrder = renderer.sortingOrder - 1;
        }
    }
}