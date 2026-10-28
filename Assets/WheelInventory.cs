using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Radial (wheel) inventory.
/// Hold openAction  -> wheel appears at the left hand.
/// Move thumbstick  -> highlights a slot.
/// Release openAction -> spawns that item in the right hand and grabs it.
/// </summary>
public class WheelInventory : MonoBehaviour
{
    [Header("Items (one per slot, in clockwise order from the top)")]
    public ItemData[] items;

    [Header("Input")]
    [Tooltip("Hold to open the wheel, e.g. <XRController>{LeftHand}/primaryButton")]
    public InputActionProperty openAction;
    [Tooltip("Thumbstick used to choose a slot, e.g. <XRController>{LeftHand}/thumbstick")]
    public InputActionProperty selectStickAction;

    [Header("References")]
    public Transform leftHand;                    // Left Controller transform
    public XRInteractionManager interactionManager;
    public XRBaseInteractor rightInteractor;      // e.g. Near-Far Interactor on the right controller
    public GameObject wheelRoot;                  // World Space canvas root (disabled at start)
    public Image[] slotImages;                    // one Image per slot, same count as items

    [Header("Look")]
    public Color normalColor = new Color(1, 1, 1, 0.6f);
    public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
    public float deadZone = 0.4f;
    public Vector3 offsetFromHand = new Vector3(0f, 0.1f, 0.15f);

    int hovered = -1;
    bool open;

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
        if (wheelRoot) wheelRoot.SetActive(false);
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (i < items.Length && items[i] && items[i].icon) slotImages[i].sprite = items[i].icon;
            slotImages[i].color = normalColor;
        }
    }

    void Update()
    {
        bool pressed = openAction.action != null && openAction.action.IsPressed();

        if (pressed && !open) OpenWheel();
        else if (!pressed && open) CloseWheel();

        if (open) UpdateHover();
    }

    void OpenWheel()
    {
        open = true;
        hovered = -1;
        wheelRoot.SetActive(true);
    }

    void UpdateHover()
    {
        // Keep the wheel at the left hand, facing the player's head.
        var cam = Camera.main.transform;
        wheelRoot.transform.position = leftHand.TransformPoint(offsetFromHand);
        wheelRoot.transform.rotation = Quaternion.LookRotation(
            wheelRoot.transform.position - cam.position, Vector3.up);

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
            newHover = Mathf.FloorToInt((angle + step * 0.5f) % 360f / step);
        }

        if (newHover == hovered) return;
        hovered = newHover;

        for (int i = 0; i < slotImages.Length; i++)
            slotImages[i].color = (i == hovered) ? highlightColor : normalColor;
    }

    void CloseWheel()
    {
        open = false;
        wheelRoot.SetActive(false);

        if (hovered >= 0 && hovered < items.Length && items[hovered] && items[hovered].prefab)
            SpawnInRightHand(items[hovered]);

        hovered = -1;
    }

    void SpawnInRightHand(ItemData data)
    {
        // Don't spawn if the right hand is already holding something.
        if (rightInteractor.hasSelection) return;

        var obj = Instantiate(data.prefab, rightInteractor.attachTransform.position,
                              rightInteractor.attachTransform.rotation);

        if (obj.TryGetComponent<IXRSelectInteractable>(out var interactable))
            interactionManager.SelectEnter((IXRSelectInteractor)rightInteractor, interactable);
    }
}