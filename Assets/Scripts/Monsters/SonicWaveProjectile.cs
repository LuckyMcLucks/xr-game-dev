using UnityEngine;

public class SonicWaveProjectile : MonoBehaviour
{
    public float speed = 9f;
    public float lifetime = 4f;
    public AudioClip hitPlayerSfx;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (hitPlayerSfx != null)
            {
                AudioSource.PlayClipAtPoint(hitPlayerSfx, other.transform.position, 1.0f);
            }

            if (BossArenaPenaltyManager.Instance != null)
            {
                BossArenaPenaltyManager.Instance.TriggerPlayerHitPenalty(other.transform);
            }

            Destroy(gameObject);
        }
    }
}