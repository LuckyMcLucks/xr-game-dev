using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Locks the XR rig's locomotion and its head orientation independently.
///
/// The concert-hall intro needs two different locks:
///   * while the "Welcome" word plays  -> nothing moves AND the head is frozen,
///   * after it fades out              -> the player may look around but stays rooted.
///
/// Locomotion providers are toggled by type name so this keeps working across XRI versions.
/// Head lock is applied in LateUpdate (after the tracked-pose driver writes the camera),
/// which is the standard way to freeze the view on a 6DoF headset.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRigLock : MonoBehaviour
{
    [Tooltip("XR Origin to control. Auto-found in the scene when left empty.")]
    public XROrigin xrOrigin;

    [Tooltip("Headset transform. Auto-found from the XR Origin when left empty.")]
    public Transform head;

    readonly List<Behaviour> movementProviders = new List<Behaviour>();
    readonly List<Behaviour> turnProviders = new List<Behaviour>();

    bool headLocked;
    bool positionLocked;
    bool anchorCaptured;
    Vector3 anchorLocalPos;
    Quaternion anchorLocalRot;

    public bool HeadLocked => headLocked;
    public bool PositionLocked => positionLocked;

    void Awake()
    {
        Resolve();
        CacheProviders();
    }

    void Resolve()
    {
        if (xrOrigin == null) xrOrigin = FindAnyObjectByType<XROrigin>();

        if (head == null && xrOrigin != null && xrOrigin.Camera != null)
            head = xrOrigin.Camera.transform;

        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    void CacheProviders()
    {
        if (xrOrigin == null) return;

        foreach (Behaviour b in xrOrigin.GetComponentsInChildren<Behaviour>(true))
        {
            if (b == null) continue;
            string typeName = b.GetType().Name;

            if (typeName.Contains("Turn"))
                turnProviders.Add(b);
            else if (typeName.Contains("MoveProvider") || typeName.Contains("Gravity") ||
                     typeName.Contains("Teleport") || typeName.Contains("VRPlayerController") ||
                     typeName.Contains("Jump") || typeName.Contains("Climb"))
                movementProviders.Add(b);
        }
    }

    /// <summary>Freeze movement, turning and head orientation.</summary>
    public void LockAll()
    {
        positionLocked = true;
        headLocked = true;
        anchorCaptured = false;
        Apply();
    }

    /// <summary>Freeze movement/teleport but let the player look around and turn.</summary>
    public void LockPositionOnly()
    {
        positionLocked = true;
        headLocked = false;
        Apply();
    }

    /// <summary>Hand control back to the player.</summary>
    public void Unlock()
    {
        positionLocked = false;
        headLocked = false;
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < movementProviders.Count; i++)
            if (movementProviders[i]) movementProviders[i].enabled = !positionLocked;

        for (int i = 0; i < turnProviders.Count; i++)
            if (turnProviders[i]) turnProviders[i].enabled = !headLocked;
    }

    void LateUpdate()
    {
        if (!headLocked || head == null) return;

        if (!anchorCaptured)
        {
            anchorLocalPos = head.localPosition;
            anchorLocalRot = head.localRotation;
            anchorCaptured = true;
        }

        head.localPosition = anchorLocalPos;
        head.localRotation = anchorLocalRot;
    }
}
