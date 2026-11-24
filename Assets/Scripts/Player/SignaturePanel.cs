using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// The orchestra rules board. After the welcome fades, this board appears in front of the
/// player. To accept the contract the player draws a scribble with the controller tip inside
/// the bottom-right signature box (hold the draw button and drag across the box).
///
/// The ink is drawn on <see cref="board"/> with a LineRenderer in board-local space, and the
/// hit test is done against <see cref="signatureBox"/> so only strokes inside the box count.
/// </summary>
[DisallowMultipleComponent]
public class SignaturePanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root object that is switched on when the rules are shown.")]
    public GameObject panelRoot;

    [Tooltip("Visible board surface. The ink LineRenderer must be a child of this RectTransform.")]
    public RectTransform board;

    [Tooltip("Bottom-right area the player must scribble inside.")]
    public RectTransform signatureBox;

    [Header("Hand")]
    [Tooltip("Right controller transform. Auto-found on the XR Origin when left empty.")]
    public Transform rightHand;

    [Tooltip("Hold to draw, e.g. <XRController>{RightHand}/gripPressed")]
    public InputActionProperty drawAction;

    [Tooltip("How close (metres) the tip must be to the box plane to leave ink.")]
    public float contactTolerance = 0.06f;

    [Tooltip("Extra reach in front of the controller origin used as the pen tip.")]
    public float tipForwardOffset = 0.05f;

    [Header("Ink")]
    public LineRenderer ink;

    [Tooltip("Minimum distance (metres, board space) between two recorded ink points.")]
    public float pointSpacing = 0.008f;

    [Tooltip("Total scribble length (metres, board space) needed to accept the signature.")]
    public float requiredLength = 0.35f;

    public UnityEvent onSigned;

    public bool Signed { get; private set; }

    readonly List<Vector3> points = new List<Vector3>();
    Vector3 lastPoint;
    bool hasLast;
    float drawnLength;

    void Awake()
    {
        if (rightHand == null) rightHand = FindRightHand();

        if (ink != null)
        {
            ink.useWorldSpace = false;
            ink.positionCount = 0;
        }
    }

    void OnEnable()
    {
        drawAction.action?.Enable();
        ResetInk();
    }

    void OnDisable()
    {
        drawAction.action?.Disable();
    }

    void Update()
    {
        if (Signed || rightHand == null || board == null || signatureBox == null) return;
        if (panelRoot != null && !panelRoot.activeInHierarchy) return;

        // With no draw button assigned the tip only has to be inside the signature box,
        // so dragging the controller/finger across it is enough.
        bool pressed = drawAction.action == null || drawAction.action.IsPressed();
        if (!pressed)
        {
            hasLast = false;
            return;
        }

        Vector3 tip = rightHand.position + rightHand.forward * tipForwardOffset;

        Vector3 boxLocal = signatureBox.InverseTransformPoint(tip);
        bool onPlane = Mathf.Abs(boxLocal.z) <= contactTolerance;
        bool inBox = signatureBox.rect.Contains(new Vector2(boxLocal.x, boxLocal.y));
        if (!onPlane || !inBox)
        {
            hasLast = false;
            return;
        }

        AppendPoint(board.InverseTransformPoint(tip));

        if (drawnLength >= requiredLength)
        {
            Signed = true;
            onSigned?.Invoke();
        }
    }

    /// <summary>Clear any previous scribble (called when the panel is shown).</summary>
    public void ResetInk()
    {
        Signed = false;
        hasLast = false;
        drawnLength = 0f;
        points.Clear();

        if (ink != null)
        {
            ink.positionCount = 0;
            ink.gameObject.SetActive(true);
        }
    }

    void AppendPoint(Vector3 p)
    {
        if (hasLast && Vector3.Distance(p, lastPoint) < pointSpacing) return;
        if (hasLast) drawnLength += Vector3.Distance(p, lastPoint);

        lastPoint = p;
        hasLast = true;
        points.Add(p);

        if (ink != null)
        {
            ink.positionCount = points.Count;
            ink.SetPosition(points.Count - 1, p);
        }
    }

    static Transform FindRightHand()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin != null)
        {
            Transform fallback = null;
            foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Right Controller") return t;
                if (fallback == null && t.name.StartsWith("Right Controller")) fallback = t;
            }
            if (fallback != null) return fallback;
        }

        var go = GameObject.Find("Right Controller");
        return go != null ? go.transform : null;
    }
}
