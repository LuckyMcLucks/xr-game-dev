using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item Data", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;

    [Tooltip("Prefab must have a Collider, Rigidbody and XR Grab Interactable.")]
    public GameObject prefab;
}