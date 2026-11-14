using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;

/// <summary>
/// Mobile-first combat controls:
/// - Left side: drag to move freely in 3D (X/Y) with a gentle Z input from vertical speed.
/// - Right side: hold to fire, swipe left/right to switch weapons.
///
/// Keyboard support remains for fast iteration in-editor.
/// </summary>
public sealed class CombatPlayerInputSource : MonoBehaviour
{
    private CombatPlayerController player;
    private CombatWeaponController weapons;
    private CombatSpecialAbilityController specialAbility;
    private readonly HashSet<int> ignoredUiFingers = new HashSet<int>();

    private int moveFingerId = -1;
    private Vector2 moveStart;
    private Vector2 moveCurrent;
    private float moveMagnitude;

    private int fireFingerId = -1;
    private Vector2 fireStart;
    private bool fireSwipeConsumed;

    private const float FireTapMaxSeconds = 0.25f;
    private float fireStartTime;
#if UNITY_EDITOR
    private bool visualValidationFireHeld;

    public void SetVisualValidationFireHeld(bool held)
    {
        visualValidationFireHeld = held;
    }
#endif

    public void SetPlayer(CombatPlayerController controller)
    {
        player = controller;
        weapons = GetComponent<CombatWeaponController>();
        specialAbility = GetComponent<CombatSpecialAbilityController>();
    }

    private void Awake()
    {
        if (!player)
        {
            player = GetComponent<CombatPlayerController>();
        }

        weapons = GetComponent<CombatWeaponController>();
        specialAbility = GetComponent<CombatSpecialAbilityController>();
    }

    private void Update()
    {
        if (!player)
        {
            return;
        }

        Vector3 inputMove = ReadKeyboardMove();
        bool fireHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.F);
#if UNITY_EDITOR
        fireHeld |= visualValidationFireHeld;
#endif
        bool switchNext = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E);
        bool switchPrev = Input.GetKeyDown(KeyCode.Q);

        ReadTouch(ref inputMove, ref fireHeld, ref switchNext, ref switchPrev);

        player.SetRequestedMove(inputMove);

        if (weapons)
        {
            if (switchNext)
            {
                weapons.SelectNext();
            }
            else if (switchPrev)
            {
                weapons.SelectPrevious();
            }

            weapons.SetFireHeld(fireHeld);
        }

        if (specialAbility && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.X)))
        {
            specialAbility.Activate();
        }
    }

    private static Vector3 ReadKeyboardMove()
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        if (Input.GetKey(KeyCode.R)) z += 1f;
        if (Input.GetKey(KeyCode.C)) z -= 1f;

        return new Vector3(x, y, z);
    }

    private void ReadTouch(ref Vector3 inputMove, ref bool fireHeld, ref bool switchNext, ref bool switchPrev)
    {
        if (Input.touchCount == 0)
        {
            moveFingerId = -1;
            fireFingerId = -1;
            moveMagnitude = 0f;
            ignoredUiFingers.Clear();
            return;
        }

        float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        float moveScale = shortSide * 0.22f;
        float swipeThreshold = Mathf.Max(40f, shortSide * 0.09f);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && EventSystem.current && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                ignoredUiFingers.Add(touch.fingerId);
            }

            if (ignoredUiFingers.Contains(touch.fingerId))
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    ignoredUiFingers.Remove(touch.fingerId);
                }

                continue;
            }

            bool isLeftSide = touch.position.x < Screen.width * 0.55f;

            if (touch.phase == TouchPhase.Began)
            {
                if (isLeftSide && moveFingerId == -1)
                {
                    moveFingerId = touch.fingerId;
                    moveStart = touch.position;
                    moveCurrent = moveStart;
                }
                else if (!isLeftSide && fireFingerId == -1)
                {
                    fireFingerId = touch.fingerId;
                    fireStart = touch.position;
                    fireSwipeConsumed = false;
                    fireStartTime = Time.unscaledTime;
                }
            }

            if (touch.fingerId == moveFingerId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    moveCurrent = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    moveFingerId = -1;
                    moveMagnitude = 0f;
                }
            }

            if (touch.fingerId == fireFingerId)
            {
                if (touch.phase == TouchPhase.Moved && !fireSwipeConsumed)
                {
                    Vector2 delta = touch.position - fireStart;
                    if (Mathf.Abs(delta.x) > swipeThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.1f)
                    {
                        fireSwipeConsumed = true;
                        if (delta.x > 0f)
                        {
                            switchNext = true;
                        }
                        else
                        {
                            switchPrev = true;
                        }
                    }
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    fireFingerId = -1;
                    fireSwipeConsumed = false;
                }
            }
        }

        if (moveFingerId != -1)
        {
            Vector2 delta = moveCurrent - moveStart;
            Vector2 axis = delta / Mathf.Max(1f, moveScale);
            axis = Vector2.ClampMagnitude(axis, 1f);
            moveMagnitude = Mathf.Lerp(moveMagnitude, axis.magnitude, 10f * Time.deltaTime);
            axis = axis.normalized * moveMagnitude;

            // Map screen drag into ship axes; a little Z from vertical magnitude helps 3D feel.
            inputMove += new Vector3(axis.x, axis.y, axis.y * 0.35f);
        }

        if (fireFingerId != -1 && !fireSwipeConsumed)
        {
            fireHeld = true;
        }
    }
}
