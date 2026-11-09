using UnityEngine;

public class TrumpetValves : MonoBehaviour
{
    [System.Serializable]
    public class Valve
    {
        public Transform rod;        // drag rod, rod (1), rod (2) here
        public KeyCode key;          // or trigger this from your own note code
        [HideInInspector] public Vector3 restPos;
    }

    public Valve[] valves;
    public float pressDistance = 0.01f;   // in local units
    public float speed = 15f;

    void Start()
    {
        foreach (var v in valves)
            v.restPos = v.rod.localPosition;
    }

    void Update()
    {
        foreach (var v in valves)
        {
            bool pressed = Input.GetKey(v.key);
            Vector3 target = v.restPos + Vector3.down * (pressed ? pressDistance : 0f);
            v.rod.localPosition = Vector3.Lerp(v.rod.localPosition, target, Time.deltaTime * speed);
        }
    }
}