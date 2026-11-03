using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Proximity highlight + "press X to grab".
///
/// * The grabbable object closest to the player's head within <see cref="proximityRadius"/>
///   gets a bright tint, showing that it can be interacted with.
/// * Pressing the X button (left controller) snaps that object into the left hand and keeps it
///   there, so it can be moved around by hand (for example sliding the bow across the violin).
/// * Pressing X again releases it.
/// </summary>
[DisallowMultipleComponent]
public class ProximityInteractor : MonoBehaviour
{
    [Header("Proximity highlight")]
    [Tooltip("How close (metres) the player's head must be to an object before it glows.")]
    public float proximityRadius = 1.6f;

    [Tooltip("Tint applied to an object while it is in range.")]
    public Color glowColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Input")]
    [Tooltip("Grab / release button. Left controller X by default.")]
    public InputActionProperty interactAction;

    [Header("Hand")]
    [Tooltip("Hand the object snaps to. Auto-found (Left Controller) when empty.")]
    public Transform hand;

    [Tooltip("Local offset from the hand that a held object sits at.")]
    public Vector3 holdOffset = new Vector3(0f, 0f, 0.08f);

    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int k_Color = Shader.PropertyToID("_Color");
    static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

    Camera cam;
    XRGrabInteractable target;   // object currently glowing
    XRGrabInteractable held;     // object currently held

    Rigidbody heldBody;
    bool heldWasKinematic;
    bool heldUsedGravity;

    XRGrabInteractable[] candidates;
    float nextCandidateRefresh;

    MaterialPropertyBlock block;

    void Awake()
    {
        // Safety net: build the button action if it was not wired up in the Inspector.
        if (interactAction.action == null || interactAction.action.bindings.Count == 0)
        {
            var action = new InputAction(
                "Interact", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            action.Enable();
            interactAction = new InputActionProperty(action);
        }
    }

    void OnEnable() => interactAction.action?.Enable();

    void OnDisable() => interactAction.action?.Disable();

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (hand == null) hand = FindLeftHand();

        if (held == null) UpdateTarget();

        if (interactAction.action != null && interactAction.action.WasPressedThisFrame())
        {
            if (held != null) Release();
            else if (target != null) Grab(target);
        }
    }

    void LateUpdate()
    {
        if (held == null || hand == null) return;

        held.transform.SetPositionAndRotation(
            hand.TransformPoint(holdOffset),
            hand.rotation);
    }

    // ---------------------------------------------------------------- target

    void UpdateTarget()
    {
        if (candidates == null || Time.time >= nextCandidateRefresh)
        {
            candidates = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            nextCandidateRefresh = Time.time + 0.5f;
        }

        XRGrabInteractable nearest = null;
        float best = proximityRadius;

        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate == held) continue;

            float distance = DistanceToPlayer(candidate);
            if (distance <= best)
            {
                best = distance;
                nearest = candidate;
            }
        }

        if (nearest == target) return;

        if (target != null) SetGlow(target, false);
        target = nearest;
        if (target != null) SetGlow(target, true);
    }

    float DistanceToPlayer(XRGrabInteractable candidate)
    {
        if (cam == null) return float.MaxValue;

        Vector3 head = cam.transform.position;

        // Measure to the object's surface when it has a collider, otherwise to its origin.
        var col = candidate.GetComponentInChildren<Collider>();
        Vector3 point = col != null ? col.ClosestPoint(head) : candidate.transform.position;

        return Vector3.Distance(point, head);
    }

    // ---------------------------------------------------------------- grab

    void Grab(XRGrabInteractable grab)
    {
        held = grab;

        heldBody = grab.GetComponent<Rigidbody>();
        if (heldBody != null)
        {
            heldWasKinematic = heldBody.isKinematic;
            heldUsedGravity = heldBody.useGravity;
            heldBody.linearVelocity = Vector3.zero;
            heldBody.angularVelocity = Vector3.zero;
            heldBody.useGravity = false;
            heldBody.isKinematic = true;
        }

        SetGlow(grab, false);
        target = null;

        // Snap straight into the hand.
        if (hand != null)
            grab.transform.SetPositionAndRotation(hand.TransformPoint(holdOffset), hand.rotation);

        // Stop the built-in grab system from fighting us while we hold it.
        grab.enabled = false;
    }

    void Release()
    {
        var grab = held;
        held = null;

        if (grab == null) return;

        grab.enabled = true;

        if (heldBody != null)
        {
            heldBody.isKinematic = heldWasKinematic;
            heldBody.useGravity = heldUsedGravity;
        }

        heldBody = null;
    }

    // ---------------------------------------------------------------- visuals

    void SetGlow(XRGrabInteractable grab, bool on)
    {
        if (grab == null) return;

        var renderers = grab.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            if (!on)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(k_BaseColor, glowColor);
            block.SetColor(k_Color, glowColor);
            block.SetColor(k_EmissionColor, glowColor * 2f);
            r.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- helpers

    static Transform FindLeftHand()
    {
        var controller = GameObject.Find("Left Controller");
        if (controller != null) return controller.transform;

        var interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None);
        for (int i = 0; i < interactors.Length; i++)
        {
            var interactor = interactors[i];
            if (interactor != null && interactor.name.Contains("Left"))
                return interactor.attachTransform != null ? interactor.attachTransform : interactor.transform;
        }

        Debug.LogWarning("[ProximityInteractor] Left hand not found - assign 'Hand' manually.");
        return null;
    }
}
