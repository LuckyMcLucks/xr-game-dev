using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Places the drum in front of the player when a drumstick is grabbed,
/// keeps it hovering there, and lets everything pass through it.
/// Put this on an object that is ALWAYS active (not on the drum itself).
public class DrumPlacer : MonoBehaviour
{
    [Header("References")]
    public Transform drumRoot;                 // the drum (root object)
    public Transform xrOrigin;                 // for floor height
    public XRBaseInteractor[] handInteractors; // left + right hand interactors

    [Header("Placement")]
    public float distance = 0.7f;              // metres in front of the player
    public float heightOffset = 0.7f;          // drum height above the floor
    public float yawOffset = 180f;             // rotate so the drum faces the player

    [Header("Follow")]
    public bool followPlayer = true;           // keep the drum in front of the player every frame
    public bool followHeadYaw = true;          // true: follows where you look. false: only the rig's rotation
    public float followSpeed = 8f;             // higher = snappier, 0 = instant

    [Header("Behaviour")]
    public bool hideWhenNoStick = true;        // hide the drum when no stick is held
    public bool replaceOnEachGrab = true;      // move the drum again on each new grab

    [Header("Hover / Pass-through")]
    public bool freezeRigidbodies = true;      // kinematic + no gravity so the drum hovers
    public bool makeCollidersTriggers = true;  // colliders no longer block anything

    int sticksHeld;

    void Awake()
    {
        if (drumRoot == null) return;

        if (freezeRigidbodies)
            FreezeBodies();

        if (makeCollidersTriggers)
        {
            foreach (var col in drumRoot.GetComponentsInChildren<Collider>(true))
            {
                // Non-convex mesh colliders can't be triggers
                if (col is MeshCollider mc && !mc.convex)
                    mc.convex = true;

                col.isTrigger = true;
            }
        }
    }

    void Start()
    {
        if (hideWhenNoStick && drumRoot != null)
            drumRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        foreach (var hand in handInteractors)
        {
            if (hand == null) continue;
            hand.selectEntered.AddListener(OnGrab);
            hand.selectExited.AddListener(OnRelease);
        }
    }

    void OnDisable()
    {
        foreach (var hand in handInteractors)
        {
            if (hand == null) continue;
            hand.selectEntered.RemoveListener(OnGrab);
            hand.selectExited.RemoveListener(OnRelease);
        }
    }

    static bool IsStick(Component c) => c.GetComponentInParent<DrumStick>() != null;

    void OnGrab(SelectEnterEventArgs args)
    {
        if (!IsStick(args.interactableObject.transform)) return;

        sticksHeld++;

        if (sticksHeld == 1 || replaceOnEachGrab)
            PlaceDrum();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (!IsStick(args.interactableObject.transform)) return;

        sticksHeld = Mathf.Max(0, sticksHeld - 1);

        if (sticksHeld == 0 && hideWhenNoStick && drumRoot != null)
            drumRoot.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!followPlayer || sticksHeld == 0 || drumRoot == null) return;
        if (!drumRoot.gameObject.activeSelf) return;
        if (!GetTargetPose(out Vector3 pos, out Quaternion rot)) return;

        if (followSpeed <= 0f)
        {
            drumRoot.SetPositionAndRotation(pos, rot);
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            drumRoot.SetPositionAndRotation(
                Vector3.Lerp(drumRoot.position, pos, t),
                Quaternion.Slerp(drumRoot.rotation, rot, t));
        }
    }

    bool GetTargetPose(out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = default;

        var cam = Camera.main;
        if (cam == null) return false;

        Transform head = cam.transform;

        Transform dirSource = (followHeadYaw || xrOrigin == null) ? head : xrOrigin;
        Vector3 fwd = Vector3.ProjectOnPlane(dirSource.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        pos = head.position + fwd * distance;
        pos.y = head.position.y - heightOffset;   

        rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
        return true;
    }

    public void PlaceDrum()
    {
        if (drumRoot == null) return;
        if (!GetTargetPose(out Vector3 pos, out Quaternion rot)) return;

        FreezeBodies();
        drumRoot.SetPositionAndRotation(pos, rot);
        drumRoot.gameObject.SetActive(true);
    }

    void FreezeBodies()
    {
        // Rigidbodies on the drum, its children, AND its parents
        var bodies = new HashSet<Rigidbody>(drumRoot.GetComponentsInChildren<Rigidbody>(true));
        foreach (var rb in drumRoot.GetComponentsInParent<Rigidbody>(true))
            bodies.Add(rb);

        foreach (var rb in bodies)
        {
            rb.isKinematic = true;   // set kinematic first, velocity can't be set on kinematic bodies in some versions
            rb.useGravity = false;
        }
    }
}