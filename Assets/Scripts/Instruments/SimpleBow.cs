using UnityEngine;

public class SimpleBow : MonoBehaviour
{
    private Vector3 lastPosition;
    private float currentSpeed;

    [Tooltip("Minimum movement speed required to count as rubbing")]
    public float minRubSpeed = 0.08f; 

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        // Calculate how fast the bow is moving through space
        currentSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
        lastPosition = transform.position;
    }

    void OnTriggerStay(Collider other)
    {
        // If touching the violin AND the player is actually moving the bow
        if (currentSpeed > minRubSpeed)
        {
            SimpleViolin violin = other.GetComponent<SimpleViolin>();
            if (violin != null)
            {
                violin.Rub();
            }
        }
    }
}
