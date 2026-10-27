using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    public Transform playerTransform; // Drag VR Head/CameraRig here
    
    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (playerTransform == null) playerTransform = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Set destination to player's current position
        agent.SetDestination(playerTransform.position);

        // Feed speed parameter to Animator to transition Idle -> Walk/Run
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat("Speed", currentSpeed);
    }
}