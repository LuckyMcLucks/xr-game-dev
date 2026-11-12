using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item Data", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    public string displayName;
    public Sprite icon;

    [Tooltip("Prefab must have a Collider, Rigidbody and XR Grab Interactable.")]
    public GameObject prefab;

    [Tooltip("Optional. Summoned together with the main prefab (goes to the LEFT hand). Leave empty for a single item.")]
    public GameObject secondPrefab;
}