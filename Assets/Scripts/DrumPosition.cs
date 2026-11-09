using UnityEngine;

/// Put this on the snare ROOT object (the parent of the head/stand).
/// Assign "head" = Main Camera (under XR Origin > Camera Offset).
public class SnarePositioner : MonoBehaviour
{
    public Transform head;                 // the VR camera
    public float distance = 0.6f;          // meters in front of the player
    public float height = 0.75f;           // snare height above the floor (world Y)
    public bool followContinuously = false; // false = place once / on recenter
    public float followSpeed = 3f;          // smoothing if following
    public KeyCode recenterKey = KeyCode.R; // for testing in editor

    void Start() => Place(true);

    void Update()
    {
        if (Input.GetKeyDown(recenterKey)) Place(true);
        else if (followContinuously) Place(false);
    }

    // Call this from a controller button (e.g. on a "recenter" input action)
    public void Place(bool instant)
    {
        if (head == null) return;

        // Use only the head's left/right turn (yaw), ignore looking up/down
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) return;

        Vector3 target = head.position + forward * distance;
        target.y = height;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        if (instant)
        {
            transform.SetPositionAndRotation(target, targetRot);
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
    }
}