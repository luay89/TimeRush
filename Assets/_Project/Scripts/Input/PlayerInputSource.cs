using UnityEngine;

/// <summary>
/// Converts keyboard and touch input into PlayerIntent so PlayerController owns only movement.
/// </summary>
public sealed class PlayerInputSource : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameBalanceConfig gameBalanceConfig;
    [SerializeField] private bool allowTouchSwipe = true;

    private Vector2 pointerDownPosition;
    private bool trackingPointer;
    private PlayerIntentBuffer laneIntentBuffer;
    private float laneInputBufferSeconds;

    private void Awake()
    {
        if (!playerController)
        {
            playerController = GetComponent<PlayerController>();
        }

        laneInputBufferSeconds = gameBalanceConfig ? gameBalanceConfig.laneInputBufferSeconds : 0.12f;
        laneIntentBuffer = new PlayerIntentBuffer(laneInputBufferSeconds);
    }

    private void OnEnable()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StateChanged += HandleGameStateChanged;
        }
    }

    private void OnDisable()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StateChanged -= HandleGameStateChanged;
        }
    }

    private void Update()
    {
        if (!playerController)
        {
            return;
        }

        if (!GameStateMachine.IsGameplayInputAllowed)
        {
            laneIntentBuffer.Clear();
            return;
        }

        laneIntentBuffer.SetExpiry(laneInputBufferSeconds);
        ConsumeBufferedLaneIntent();

        PlayerIntent keyboardIntent = PlayerIntent.FromKeyboard(
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A),
            Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D),
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));

        Dispatch(keyboardIntent);
        ReadTouchInput();
    }

    private void ReadTouchInput()
    {
        if (!allowTouchSwipe || Input.touchCount == 0)
        {
            return;
        }

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            pointerDownPosition = touch.position;
            trackingPointer = true;
            return;
        }

        if (!trackingPointer || touch.phase != TouchPhase.Ended)
        {
            return;
        }

        trackingPointer = false;
        float threshold = Mathf.Max(32f, Screen.width * 0.08f);
        Dispatch(PlayerIntent.FromSwipe(touch.position - pointerDownPosition, threshold));
    }

    private void Dispatch(PlayerIntent intent)
    {
        if (intent.IsEmpty)
        {
            return;
        }

        if (intent.HasLaneStep && !playerController.SubmitIntent(new PlayerIntent(intent.LaneStep, 0f)) && playerController.IsLaneTransitioning)
        {
            // Keep one buffered lane intent so a fast player input during a lane transition is not lost, while preventing command queues.
            laneIntentBuffer.TryStoreLaneStep(intent.LaneStep, Time.unscaledTime);
        }

        if (intent.HasDepthAxis || intent.HasDepthStep)
        {
            playerController.SubmitIntent(new PlayerIntent(0, intent.DepthAxis, intent.DepthStep));
        }
    }

    private void ConsumeBufferedLaneIntent()
    {
        if (playerController.IsLaneTransitioning || !laneIntentBuffer.TryConsumeLaneStep(Time.unscaledTime, out int laneStep))
        {
            return;
        }

        playerController.SubmitIntent(new PlayerIntent(laneStep, 0f));
    }

    private void HandleGameStateChanged(GameStateKind previous, GameStateKind current)
    {
        if (RequiresBufferClear(current))
        {
            laneIntentBuffer.Clear();
        }
    }

    public static bool RequiresBufferClear(GameStateKind state)
    {
        return state != GameStateKind.Playing;
    }
}
