using UnityEngine;
using UnityEngine.XR;

/// Put this on the stick TIP (a small child object at the end of the stick).
/// Needs: a Collider (Is Trigger ON) + a Rigidbody (Is Kinematic ON), tagged "DrumStick".
public class DrumStick : MonoBehaviour
{
    public XRNode hand = XRNode.RightHand;   // which controller vibrates

    public Vector3 Velocity { get; private set; }
    Vector3 lastPos;

    void Start() => lastPos = transform.position;

    void FixedUpdate()
    {
        // Tracked objects have no physics velocity, so measure it ourselves
        Velocity = (transform.position - lastPos) / Time.fixedDeltaTime;
        lastPos = transform.position;
    }

    public void Haptic(float amplitude, float duration = 0.05f)
    {
        var device = InputDevices.GetDeviceAtXRNode(hand);
        if (device.isValid)
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
    }
}