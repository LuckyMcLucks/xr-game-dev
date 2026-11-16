using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Radial (wheel) "summon" inventory.
/// Hold openAction (left hand) -> wheel appears in front of the eyes.
/// Right thumbstick            -> highlights a slot.
/// Release on a slot           -> that slot's item is spawned (first time) or teleported
///                                to the right hand from wherever it is, then grabbed.
///                                If the ItemData has a Second Prefab, it is summoned too
///                                and goes to the left hand.
/// Slots never empty: each slot owns persistent objects (one, or two).
/// Turn/move providers listed in disableWhileOpen are disabled while the wheel is open.
/// Also exposes PullWorldObject() to fly any scene object into the right hand.
/// </summary>
public class WheelInventory : MonoBehaviour
{
    [Header("Inventory Slots")]
    [Tooltip("One entry per slot. Empty (None) = unused slot. Use each ItemData in only one slot.")]
    public ItemData[] items;

    [Header("Input")]
    [Tooltip("Hold to open the wheel, e.g. <XRController>{LeftHand}/primaryButton")]
    public InputActionProperty openAction;
    [Tooltip("Thumbstick used to choose a slot, e.g. <XRController>{RightHand}/thumbstick. " +
             "Use your own action, not the XRI default Turn/Move actions (OnDisable disables this action).")]
    public InputActionProperty selectStickAction;

    [Header("References")]
    public Transform leftHand;
    public XRInteractionManager interactionManager;
    public XRBaseInteractor rightInteractor;
    [Tooltip("Needed only for items with a Second Prefab. Receives the second object.")]
    public XRBaseInteractor leftInteractor;
    public GameObject wheelRoot;
    public Image[] slotImages;
    public GameObject[] selectedHighlights;   // one "Selected" object per slot

    [Header("Look")]
    public Color normalColor = new Color(1, 1, 1, 0.6f);
    public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    public float deadZone = 0.4f;
    public Vector3 offsetFromHand = new Vector3(0f, 0.1f, 0.15f);

    [Header("Placement")]
    [Tooltip("How far in front of the eyes the wheel is shown (metres).")]
    public float viewDistance = 0.45f;

    [Header("Summon Animation")]
    public float flyDuration = 0.2f;

    [Header("Spawn Origin")]
    [Tooltip("ON: first-time spawn comes out of the wheel. OFF: out of the left hand.")]
    public bool spawnFromWheel = true;

    [Header("Locomotion Lock")]
    [Tooltip("Drag turn/move providers here. They are disabled while the wheel is open.")]
    public Behaviour[] disableWhileOpen;

    /// <summary>True while the wheel is open.</summary>
    public bool IsOpen => open;

    int hovered = -1;
    bool open;
    Camera cam;

    GameObject[] spawned;    // live copy of the main prefab, per slot
    GameObject[] spawned2;   // live copy of the second prefab, per slot
    int[] pending;           // flights still running per slot (blocks double-triggers)

    // ---------------------------------------------------------------- lifecycle

    void OnEnable()
    {
        openAction.action?.Enable();
        selectStickAction.action?.Enable();
    }

    void OnDisable()
    {
        openAction.action?.Disable();
        selectStickAction.action?.Disable();
        SetLocomotionBlocked(false);   // never leave the player unable to turn/move
    }

    void Start()
    {
        cam = Camera.main;

        int count = items != null ? items.Length : 0;
        spawned = new GameObject[count];
        spawned2 = new GameObject[count];
        pending = new int[count];

        if (wheelRoot) wheelRoot.SetActive(false);
        RefreshAllSlotsUI();
    }

    void Update()
    {
        bool pressed = openAction.action != null && openAction.action.IsPressed();
        if (pressed && !open) OpenWheel();
        else if (!pressed && open) CloseWheel();

        if (open) UpdateHover();
    }

    // ---------------------------------------------------------------- wheel

    void OpenWheel()
    {
        open = true;
        hovered = -1;

        if (wheelRoot == null)
        {
            Debug.LogWarning("[WheelInventory] 'Wheel Root' is not assigned, nothing to show.");
            return;
        }

        SetLocomotionBlocked(true);

        // Show the wheel FIRST, then refresh the icons: a UI refresh must never be
        // able to stop the wheel from appearing.
        PlaceWheel();
        wheelRoot.SetActive(true);
        RefreshAllSlotsUI();
    }

    void CloseWheel()
    {
        open = false;
        SetLocomotionBlocked(false);

        if (wheelRoot != null) wheelRoot.SetActive(false);

        if (hovered >= 0 && items != null && hovered < items.Length)
            HandleSlotAction(hovered);

        hovered = -1;
    }

    void SetLocomotionBlocked(bool blocked)
    {
        if (disableWhileOpen == null) return;
        foreach (var b in disableWhileOpen)
            if (b != null) b.enabled = !blocked;
    }

    void UpdateHover()
    {
        if (cam == null) cam = Camera.main;
        if (wheelRoot == null) return;

        PlaceWheel();

        Vector2 stick = selectStickAction.action != null
            ? selectStickAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        int slotCount = items != null ? items.Length : 0;
        int newHover = -1;
        if (stick.magnitude > deadZone && slotCount > 0)
        {
            // 0 deg = up, increasing clockwise.
            float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            float step = 360f / slotCount;
            newHover = Mathf.FloorToInt(((angle + step * 0.5f) % 360f) / step);
        }

        if (newHover == hovered) return;
        hovered = newHover;
        RefreshAllSlotsUI();
    }

    /// <summary>
    /// Keeps the wheel directly in front of the player's eyes so it is always visible
    /// while the open button is held. Falls back to the left hand when there is no camera.
    /// </summary>
    void PlaceWheel()
    {
        if (wheelRoot == null) return;

        if (cam == null)
        {
            if (leftHand == null) return;
            wheelRoot.transform.SetPositionAndRotation(
                leftHand.TransformPoint(offsetFromHand), leftHand.rotation);
            return;
        }

        Transform head = cam.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = head.forward;
        else forward.Normalize();

        float distance = viewDistance > 0.05f ? viewDistance : 0.45f;
        Vector3 position = head.position + forward * distance;

        wheelRoot.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(position - head.position, Vector3.up));
    }

    // ---------------------------------------------------------------- summon

    void HandleSlotAction(int slot)
    {
        if (items[slot] == null || items[slot].prefab == null) return;
        if (rightInteractor.hasSelection) return;   // hand already full
        SummonItem(slot);
    }

    void SummonItem(int slot)
    {
        if (spawned == null || slot >= spawned.Length) return;
        if (pending[slot] > 0) return;   // still flying from the last summon

        ItemData data = items[slot];

        // Main item -> right hand.
        spawned[slot] = GetOrSpawn(spawned[slot], data.prefab, data);
        StartCoroutine(FlyRoutine(slot, spawned[slot], rightInteractor));

        // Optional second item -> left hand.
        if (data.secondPrefab != null)
        {
            if (leftInteractor == null)
            {
                Debug.LogWarning("[WheelInventory] Item has a Second Prefab but 'Left Interactor' is not assigned.");
                return;
            }

            spawned2[slot] = GetOrSpawn(spawned2[slot], data.secondPrefab, data);
            StartCoroutine(FlyRoutine(slot, spawned2[slot], leftInteractor));
        }
    }

    /// <summary>Returns the existing copy, or spawns a new one if it doesn't exist (yet).</summary>
    GameObject GetOrSpawn(GameObject existing, GameObject prefab, ItemData data)
    {
        if (existing != null) return existing;

        Vector3 startPos = (spawnFromWheel && wheelRoot != null)
            ? wheelRoot.transform.position
            : leftHand.TransformPoint(offsetFromHand);

        var obj = Instantiate(prefab, startPos, rightInteractor.transform.rotation);

        if (!obj.TryGetComponent<ItemInstance>(out var instance))
            instance = obj.AddComponent<ItemInstance>();
        instance.data = data;

        return obj;
    }

    IEnumerator FlyRoutine(int slot, GameObject obj, XRBaseInteractor hand)
    {
        pending[slot]++;
        yield return FlyToHandAndGrab(obj, hand);
        pending[slot]--;
    }

    /// <summary>Fly any scene object (with an XRGrabInteractable) into the right hand.</summary>
    public void PullWorldObject(GameObject obj)
    {
        if (obj == null || rightInteractor.hasSelection) return;
        StartCoroutine(FlyToHandAndGrab(obj, rightInteractor));
    }

    // ---------------------------------------------------------------- flight

    IEnumerator FlyToHandAndGrab(GameObject obj, XRBaseInteractor interactor)
    {
        Transform hand = interactor.attachTransform != null
            ? interactor.attachTransform
            : interactor.transform;

        obj.TryGetComponent<XRGrabInteractable>(out var grab);
        obj.TryGetComponent<Rigidbody>(out var rb);

        // The object may already be held by something else: let go first.
        if (grab && grab.isSelected)
            interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

        // Freeze during flight.
        bool wasKinematic = false;
        if (rb)
        {
            wasKinematic = rb.isKinematic;
            rb.linearVelocity = Vector3.zero;   // use rb.velocity on older Unity versions
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (grab) grab.enabled = false;

        // Grip pose relative to the object, so the grip (not the origin) lands on the hand.
        Transform grip = (grab && grab.attachTransform) ? grab.attachTransform : obj.transform;
        Vector3 gripLocalPos = obj.transform.InverseTransformPoint(grip.position);
        Quaternion gripLocalRot = Quaternion.Inverse(obj.transform.rotation) * grip.rotation;
        Vector3 scale = obj.transform.lossyScale;

        Vector3 fromPos = obj.transform.position;
        Quaternion fromRot = obj.transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            if (obj == null) yield break;

            t += Time.deltaTime / Mathf.Max(0.01f, flyDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            // The hand keeps moving, so recompute the target every frame.
            Quaternion targetRot = hand.rotation * Quaternion.Inverse(gripLocalRot);
            Vector3 targetPos = hand.position - targetRot * Vector3.Scale(gripLocalPos, scale);

            obj.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, targetPos, k),
                Quaternion.Slerp(fromRot, targetRot, k));

            yield return null;
        }

        if (obj == null) yield break;

        // Hand got filled during the flight: leave the item where it is.
        if (interactor.hasSelection)
        {
            if (rb) rb.isKinematic = wasKinematic;
            if (grab) grab.enabled = true;
            yield break;
        }

        if (grab)
        {
            grab.useDynamicAttach = false;
            grab.enabled = true;
        }

        yield return null;   // one frame for the interactable to register
        if (obj == null) yield break;

        if (rb) rb.isKinematic = wasKinematic;

        if (!interactor.hasSelection &&
            obj.TryGetComponent<IXRSelectInteractable>(out var interactable))
            interactionManager.SelectEnter((IXRSelectInteractor)interactor, interactable);
    }

    // ---------------------------------------------------------------- UI

    void RefreshAllSlotsUI()
    {
        if (slotImages == null) return;
        for (int i = 0; i < slotImages.Length; i++)
            UpdateSlotUI(i);
    }

    void UpdateSlotUI(int index)
    {
        if (slotImages == null || index >= slotImages.Length) return;

        var img = slotImages[index];
        if (img == null) return;

        bool filled = items != null && index < items.Length && items[index] != null;

        if (filled && items[index].prefab == null)
            Debug.LogWarning($"[WheelInventory] Slot {index} points at '{items[index].name}', which has no prefab assigned.");

        img.sprite = filled ? items[index].icon : null;
        img.enabled = true;

        if (index == hovered) img.color = highlightColor;
        else img.color = filled ? normalColor : emptySlotColor;

        // Show the "Selected" object only for the hovered slot
        if (selectedHighlights != null && index < selectedHighlights.Length && selectedHighlights[index] != null)
            selectedHighlights[index].SetActive(index == hovered);
    }
}