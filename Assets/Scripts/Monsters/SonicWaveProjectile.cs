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

            // Optional: knock the player backward slightly in VR to disorient them
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.Move(transform.forward * 1.5f);
            }

            Destroy(gameObject);
        }
    }
}