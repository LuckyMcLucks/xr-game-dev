using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Places the XR rig at this object's transform when the scene starts, so the
/// cutscene always begins from an authored pose.
///
/// Only the rig root is moved - the headset camera is a child of the rig, so
/// the player keeps full 6DoF head tracking and can still look around while
/// the "Welcome" text plays directly in front of them.
/// </summary>
[DefaultExecutionOrder(-100)]
public class StartCameraPose : MonoBehaviour
{
    [Tooltip("XR Origin (rig) to move. If left empty, the first XROrigin in the scene is found automatically.")]
    public XROrigin xrOrigin;

    [Tooltip("Yaw the rig to match this object's forward direction.")]
    public bool applyYaw = true;

    void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (xrOrigin == null)
        {
            Debug.LogWarning($"{nameof(StartCameraPose)}: no XROrigin found in the scene.", this);
            return;
        }

        var rig = xrOrigin.transform;
        rig.position = transform.position;

        if (applyYaw)
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
                rig.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
