using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Radial (wheel) inventory with put & pull.
/// Hold openAction -> wheel appears at the left hand.
/// Thumbstick      -> highlights a slot.
/// Release:
///   * Right hand holding an item + empty slot -> STORE the item.
///   * Right hand empty + filled slot          -> PULL the item (flies to hand, then grabs).
/// Also exposes PullWorldObject() to fly any scene object into the hand.
/// </summary>
public class WheelInventory : MonoBehaviour
{
    [Header("Inventory Slots")]
    [Tooltip("One entry per slot. Empty (None) = empty slot.")]
    public ItemData[] items;

    [Header("Input")]
    [Tooltip("Hold to open the wheel, e.g. <XRController>{LeftHand}/primaryButton")]
    public InputActionProperty openAction;
    [Tooltip("Thumbstick used to choose a slot, e.g. <XRController>{LeftHand}/thumbstick")]
    public InputActionProperty selectStickAction;

    [Header("References")]
    public Transform leftHand;
    public XRInteractionManager interactionManager;
    public XRBaseInteractor rightInteractor;
    public GameObject wheelRoot;
    public Image[] slotImages;
    public GameObject[] selectedHighlights;   // one "Selected" object per slot

    [Header("Look")]
    public Color normalColor = new Color(1, 1, 1, 0.6f);
    public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    public float deadZone = 0.4f;
    public Vector3 offsetFromHand = new Vector3(0f, 0.1f, 0.15f);

    [Header("Pull Animation")]
    public float flyDuration = 0.2f;

    int hovered = -1;
    bool open;
    Camera cam;

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
    }

    void Start()
    {
        cam = Camera.main;
        if (wheelRoot) wheelRoot.SetActive(false);
        RefreshAllSlotsUI();
    }

    void Update()
    {
        bool pressed = openAction.action != null && openAction.action.IsPressed();
        if (openAction.action != null && openAction.action.WasPressedThisFrame())
            Debug.Log("Open button pressed");
        if (pressed && !open) OpenWheel();
        else if (!pressed && open) CloseWheel();

        if (open) UpdateHover();
    }

    // ---------------------------------------------------------------- wheel

    void OpenWheel()
    {
        open = true;
        hovered = -1;
        RefreshAllSlotsUI();
        wheelRoot.SetActive(true);
    }

    void CloseWheel()
    {
        open = false;
        wheelRoot.SetActive(false);

        if (hovered >= 0 && hovered < items.Length)
            HandleSlotAction(hovered);

        hovered = -1;
    }

    void UpdateHover()
    {
        if (cam == null) cam = Camera.main;

        // Keep the wheel at the left hand, facing the player's head.
        wheelRoot.transform.position = leftHand.TransformPoint(offsetFromHand);
        if (cam)
        {
            wheelRoot.transform.rotation = Quaternion.LookRotation(
                wheelRoot.transform.position - cam.transform.position, Vector3.up);
        }

        Vector2 stick = selectStickAction.action != null
            ? selectStickAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        int newHover = -1;
        if (stick.magnitude > deadZone && items.Length > 0)
        {
            // 0 deg = up, increasing clockwise.
            float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            float step = 360f / items.Length;
            newHover = Mathf.FloorToInt(((angle + step * 0.5f) % 360f) / step);
        }

        if (newHover == hovered) return;
        hovered = newHover;
        RefreshAllSlotsUI();
    }

    // ---------------------------------------------------------------- put / pull

    void HandleSlotAction(int slot)
    {
        if (rightInteractor.hasSelection)
        {
            if (items[slot] == null) StoreHeldItem(slot);
        }
        else
        {
            if (items[slot] != null && items[slot].prefab != null) PullItem(slot);
        }
    }

    void StoreHeldItem(int slot)
    {
        var interactable = rightInteractor.firstInteractableSelected;
        if (interactable == null) return;

        var heldObj = interactable.transform.gameObject;
        var instance = heldObj.GetComponentInParent<ItemInstance>();

        if (instance == null || instance.data == null)
        {
            Debug.LogWarning($"'{heldObj.name}' can't be stored: it needs an ItemInstance with ItemData assigned.");
            return;
        }

        items[slot] = instance.data;

        // Release first so XRI doesn't complain when the object is destroyed.
        interactionManager.SelectExit((IXRSelectInteractor)rightInteractor, interactable);
        Destroy(instance.gameObject);

        RefreshAllSlotsUI();
    }

    void PullItem(int slot)
    {
        ItemData data = items[slot];

        // Empty the slot immediately so it can't be pulled twice.
        items[slot] = null;
        RefreshAllSlotsUI();

        StartCoroutine(SpawnAndGrab(data, slot));
    }

    IEnumerator SpawnAndGrab(ItemData data, int slot)
    {
        Vector3 startPos = leftHand.TransformPoint(offsetFromHand);
        var obj = Instantiate(data.prefab, startPos, rightInteractor.transform.rotation);

        if (!obj.TryGetComponent<ItemInstance>(out var instance))
            instance = obj.AddComponent<ItemInstance>();
        instance.data = data;

        yield return FlyToHandAndGrab(obj, () =>
        {
            // Hand got filled mid-flight: return the item to its slot.
            if (items[slot] == null) items[slot] = data;
            else
            {
                int free = System.Array.IndexOf(items, null);
                if (free >= 0) items[free] = data;
            }
            Destroy(obj);
            RefreshAllSlotsUI();
        });
    }

    /// <summary>Fly any scene object (with an XRGrabInteractable) into the right hand.</summary>
    public void PullWorldObject(GameObject obj)
    {
        if (obj == null || rightInteractor.hasSelection) return;
        StartCoroutine(FlyToHandAndGrab(obj, null));
    }

    // ---------------------------------------------------------------- flight

    IEnumerator FlyToHandAndGrab(GameObject obj, System.Action onHandBusy)
    {
        Transform hand = rightInteractor.attachTransform != null
            ? rightInteractor.attachTransform
            : rightInteractor.transform;

        obj.TryGetComponent<XRGrabInteractable>(out var grab);
        obj.TryGetComponent<Rigidbody>(out var rb);

        // World objects may already be held by something else: let go first.
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

        // Hand got filled during the flight.
        if (rightInteractor.hasSelection)
        {
            if (onHandBusy != null)
            {
                onHandBusy();
            }
            else
            {
                // World object: drop it where it is and restore physics.
                if (rb) rb.isKinematic = wasKinematic;
                if (grab) grab.enabled = true;
            }
            yield break;
        }

        if (rb) rb.isKinematic = wasKinematic;
        if (grab)
        {
            grab.useDynamicAttach = false;
            grab.enabled = true;
        }

        yield return null; // one frame for the interactable to register

        if (obj != null && obj.TryGetComponent<IXRSelectInteractable>(out var interactable))
            interactionManager.SelectEnter((IXRSelectInteractor)rightInteractor, interactable);
    }

    // ---------------------------------------------------------------- UI

    void RefreshAllSlotsUI()
    {
        for (int i = 0; i < slotImages.Length; i++)
            UpdateSlotUI(i);
    }

    void UpdateSlotUI(int index)
    {
        if (index >= slotImages.Length) return;

        var img = slotImages[index];
        if (img == null) return;

        bool filled = index < items.Length && items[index] != null;

        img.sprite = filled ? items[index].icon : null;
        img.enabled = true;

        if (index == hovered) img.color = highlightColor;
        else img.color = filled ? normalColor : emptySlotColor;

        // Show the "Selected" object only for the hovered slot
        if (selectedHighlights != null && index < selectedHighlights.Length && selectedHighlights[index] != null)
            selectedHighlights[index].SetActive(index == hovered);
    }
}