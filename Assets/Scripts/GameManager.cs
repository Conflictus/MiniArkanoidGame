using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

namespace MiniIT.ARKANOID
{
    public class GameManager : MonoBehaviour
    {
        [Header("Brick Settings")]
        [SerializeField] private Brick brickPrefab = null;
        [SerializeField] private Transform bricksParent = null;
        [SerializeField] [Min(1)] private int columns = 11;
        [SerializeField] [Min(1)] private int rows = 8;

        [Header("Layout Margins (world units)")]
        [SerializeField] private float sideMargin = 0.5f;
        [SerializeField] private float topMargin = 1.0f;
        [SerializeField] private float bottomMargin = 2.5f;

        [Header("Layout Coverage (relative to screen)")]
        [SerializeField] [Range(0.1f, 1f)] private float layoutWidthPercent = 0.9f;
        [SerializeField] [Range(0.1f, 0.8f)] private float layoutHeightPercent = 0.45f;

        [Header("Spacing (relative to available area)")]
        [SerializeField] [Range(0f, 0.25f)] private float horizontalSpacingPercent = 0.05f;
        [SerializeField] [Range(0f, 0.25f)] private float verticalSpacingPercent = 0.05f;

        [Header("Scaling")]
        [SerializeField] private bool preserveBrickAspect = true;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] [Range(0.75f, 1f)] private float brickFillPercent = 0.92f;

        [Header("Visuals")]
        [SerializeField] private bool tintBricksByRow = true;
        [SerializeField] private Color[] rowColors =
        {
            new Color(0.94f, 0.80f, 0.46f),
            new Color(0.98f, 0.56f, 0.19f),
            new Color(0.24f, 0.68f, 0.91f),
            new Color(0.32f, 0.88f, 0.53f),
            new Color(0.88f, 0.27f, 0.46f),
            new Color(0.55f, 0.34f, 0.80f),
        };

        [Header("Shape Preset")]
        [SerializeField] private LayoutShape layoutShape = LayoutShape.Rectangle;

        [Header("Gameplay References")]
        [SerializeField] private BallController ballController = null;

        [Header("UI")]
        [SerializeField] private Canvas pauseMenuCanvas = null;
        [SerializeField] private TextMeshProUGUI uiText = null;
        [SerializeField] private Button scoreText = null;
        [SerializeField] private string introMessage = "Нажми, чтобы начать";
        [SerializeField] private string winMessage = "Все блоки уничтожены!";
        [SerializeField] private string loseMessage = "Мяч вылетел. Попробуй снова.";
        [SerializeField] private string playButtonLabel = "Играть";
        [SerializeField] private string restartButtonLabel = "Перезапуск";
        [SerializeField] private bool randomizeLayoutOnRestart = true;

        private List<Brick> spawnedBricks = null;
        private Camera mainCamera = null;
        private TMP_Text buttonLabelText = null;
        private int bricksRemaining = 0;
        private bool menuVisible = false;

        private enum LayoutShape
        {
            Rectangle = 0,
            StairLeft = 1,
            StairRight = 2,
        }

        private enum GameState
        {
            Intro = 0,
            Playing = 1,
            Won = 2,
            Lost = 3,
        }

        public static GameManager Instance { get; private set; }
        private GameState currentState = GameState.Intro;

        public bool IsInputLocked
        {
            get
            {
                return menuVisible || currentState != GameState.Playing;
            }
        }

        private List<Brick> SpawnedBricks
        {
            get
            {
                if (spawnedBricks == null)
                {
                    spawnedBricks = new List<Brick>();
                }

                return spawnedBricks;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            mainCamera = Camera.main;

            if (ballController == null)
            {
                ballController = FindAnyObjectByType<BallController>();
            }

            if (bricksParent == null)
            {
                GameObject container = new GameObject("Bricks");
                bricksParent = container.transform;
                bricksParent.SetParent(transform);
            }

            if (pauseMenuCanvas != null)
            {
                pauseMenuCanvas.enabled = false;
            }

            menuVisible = false;
        }

        private void Start()
        {
            if (generateOnStart)
            {
                RegenerateBricks();
            }

            ShowIntroMenu();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            if (!Application.isPlaying && generateOnStart)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    RegenerateBricks();
                };
            }
        }
#endif

        [ContextMenu("Regenerate Bricks")]
        public void RegenerateBricks()
        {
            ClearExistingBricks();
            bricksRemaining = 0;

            if (brickPrefab == null)
            {
                Debug.LogWarning("Brick prefab is not assigned.");
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Debug.LogWarning("Camera not found. Cannot align bricks to screen.");
                return;
            }

            GenerateGrid();
        }

        private void ClearExistingBricks()
        {
            List<Brick> bricks = SpawnedBricks;

            for (int i = bricks.Count - 1; i >= 0; i--)
            {
                Brick brick = bricks[i];
                if (brick == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(brick.gameObject);
                }
                else
#endif
                {
                    Destroy(brick.gameObject);
                }
            }

            bricks.Clear();

            if (bricksParent == null)
            {
                return;
            }

            for (int i = bricksParent.childCount - 1; i >= 0; i--)
            {
                Transform child = bricksParent.GetChild(i);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                }
                else
#endif
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void GenerateGrid()
        {
            float camHeight = mainCamera.orthographicSize * 2f;
            float camWidth = camHeight * mainCamera.aspect;

            float usableWidth = Mathf.Max(0.1f, camWidth * layoutWidthPercent);
            float usableHeight = Mathf.Max(0.1f, camHeight * layoutHeightPercent);

            float gridWidth = Mathf.Max(0.1f, usableWidth - sideMargin * 2f);
            float gridHeight = Mathf.Max(0.1f, usableHeight - topMargin - bottomMargin);

            float spacingX = gridWidth * horizontalSpacingPercent;
            float spacingY = gridHeight * verticalSpacingPercent;

            float totalSpacingX = spacingX * (columns - 1);
            float totalSpacingY = spacingY * (rows - 1);

            float cellWidth = (gridWidth - totalSpacingX) / columns;
            float cellHeight = (gridHeight - totalSpacingY) / rows;

            Vector2 prefabSize = GetBrickPrefabSize();
            Vector3 targetScale = Vector3.one;

            if (prefabSize.x > 0f && prefabSize.y > 0f)
            {
                float scaleX = cellWidth / prefabSize.x;
                float scaleY = cellHeight / prefabSize.y;

                if (preserveBrickAspect)
                {
                    float uniformScale = Mathf.Min(scaleX, scaleY);
                    targetScale = new Vector3(uniformScale, uniformScale, 1f);
                }
                else
                {
                    targetScale = new Vector3(scaleX, scaleY, 1f);
                }
            }

            targetScale *= brickFillPercent;

            float leftEdge = -camWidth * 0.5f + (camWidth - gridWidth) * 0.5f + sideMargin;
            float topEdge = camHeight * 0.5f - topMargin;
            float startX = leftEdge + cellWidth * 0.5f;
            float startY = topEdge - cellHeight * 0.5f;

            int bricksSpawned = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (!ShouldSpawnBrick(row, col))
                    {
                        continue;
                    }

                    Vector3 position = new Vector3(
                        startX + col * (cellWidth + spacingX),
                        startY - row * (cellHeight + spacingY),
                        0f);

                    Brick brickInstance = Instantiate(brickPrefab, position, Quaternion.identity, bricksParent);
                    brickInstance.transform.localScale = targetScale;
                    ApplyRowVisuals(brickInstance, row);

                    SpawnedBricks.Add(brickInstance);
                    bricksSpawned++;
                }
            }

            bricksRemaining = bricksSpawned;

            if (currentState == GameState.Playing && bricksRemaining == 0)
            {
                HandleAllBricksDestroyed();
            }
        }

        private bool ShouldSpawnBrick(int row, int column)
        {
            switch (layoutShape)
            {
                case LayoutShape.StairLeft:
                    return ShouldSpawnStairLeft(row, column);
                case LayoutShape.StairRight:
                    return ShouldSpawnStairRight(row, column);
                default:
                    return true;
            }
        }

        private bool ShouldSpawnStairLeft(int row, int column)
        {
            int bricksInRow = Mathf.Clamp(columns - row, 1, columns);
            return column < bricksInRow;
        }

        private bool ShouldSpawnStairRight(int row, int column)
        {
            int bricksInRow = Mathf.Clamp(columns - row, 1, columns);
            int startColumn = Mathf.Max(0, columns - bricksInRow);
            return column >= startColumn;
        }

        private void ApplyRowVisuals(Brick brick, int rowIndex)
        {
            if (!tintBricksByRow || rowColors == null || rowColors.Length == 0)
            {
                return;
            }

            SpriteRenderer renderer = brick.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            Color targetColor = rowColors[rowIndex % rowColors.Length];
            renderer.color = targetColor;
        }

        private Vector2 GetBrickPrefabSize()
        {
            if (brickPrefab == null)
            {
                return Vector2.zero;
            }

            SpriteRenderer spriteRenderer = brickPrefab.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                return spriteRenderer.bounds.size;
            }

            Collider2D collider = brickPrefab.GetComponent<Collider2D>();
            if (collider != null)
            {
                return collider.bounds.size;
            }

            return Vector2.one;
        }

        private void ShowIntroMenu()
        {
            currentState = GameState.Intro;
            ballController?.ResetBall();
            ShowMenu(introMessage, playButtonLabel);
        }

        private void ShowMenu(string message, string buttonLabel)
        {
            if (pauseMenuCanvas == null || uiText == null || scoreText == null)
            {
                return;
            }

            uiText.text = message;

            if (buttonLabelText == null && scoreText != null)
            {
                buttonLabelText = scoreText.GetComponentInChildren<TMP_Text>(true);
            }

            if (buttonLabelText != null)
            {
                buttonLabelText.text = buttonLabel;
            }

            scoreText.onClick.RemoveListener(RestartGame);
            scoreText.onClick.AddListener(RestartGame);

            pauseMenuCanvas.enabled = true;
            pauseMenuCanvas.gameObject.SetActive(true);
            menuVisible = true;
        }

        private void HideMenu()
        {
            if (pauseMenuCanvas == null)
            {
                return;
            }

            pauseMenuCanvas.enabled = false;
            pauseMenuCanvas.gameObject.SetActive(false);
            menuVisible = false;
        }

        private void RestartGame()
        {
            if (!menuVisible)
            {
                return;
            }

            HideMenu();
            RandomizeLayoutShape();
            ballController?.ResetBall();
            RegenerateBricks();
            currentState = GameState.Playing;
        }

        public void NotifyBrickDestroyed(Brick brick)
        {
            if (brick == null)
            {
                return;
            }

            SpawnedBricks.Remove(brick);
            bricksRemaining = Mathf.Max(0, bricksRemaining - 1);

            if (bricksRemaining == 0 && currentState == GameState.Playing)
            {
                HandleAllBricksDestroyed();
            }
        }

        private void HandleAllBricksDestroyed()
        {
            currentState = GameState.Won;
            ballController?.ResetBall();
            ShowMenu(winMessage, restartButtonLabel);
        }

        public void HandleBallLost()
        {
            if (currentState != GameState.Playing)
            {
                return;
            }

            currentState = GameState.Lost;
            ballController?.ResetBall();
            ShowMenu(loseMessage, restartButtonLabel);
        }

        private void RandomizeLayoutShape()
        {
            if (!randomizeLayoutOnRestart)
            {
                return;
            }

            LayoutShape[] shapes = (LayoutShape[])Enum.GetValues(typeof(LayoutShape));
            if (shapes == null || shapes.Length == 0)
            {
                return;
            }

            layoutShape = shapes[Random.Range(0, shapes.Length)];
        }
    }
}
