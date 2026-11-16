using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandModelSwitcher : MonoBehaviour
{
    [Header("Hand Visuals")]
    [Tooltip("Drag the GameObject of the hand model or root of hand meshes here")]
    public GameObject handModel;

    [Header("Interactor")]
    [Tooltip("The interactor doing the grabbing (NearFarInteractor, XRDirectInteractor, etc.)")]
    public XRBaseInteractor interactor;

    Renderer[] handRenderers;

    void Awake()
    {
        if (interactor == null)
            interactor = GetComponent<XRBaseInteractor>();

        CacheRenderers();
    }

    void OnEnable()
    {
        if (interactor != null)
        {
            interactor.selectEntered.AddListener(OnSelectEntered);
            interactor.selectExited.AddListener(OnSelectExited);
        }
    }

    void OnDisable()
    {
        if (interactor != null)
        {
            interactor.selectEntered.RemoveListener(OnSelectEntered);
            interactor.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void CacheRenderers()
    {
        if (handModel != null)
        {
            // Gets all SkinnedMeshRenderers and regular MeshRenderers in the hand model
            handRenderers = handModel.GetComponentsInChildren<Renderer>(true);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[HandModelSwitcher] Grabbed: {args.interactableObject.transform.name}. Hiding hand.");
        SetHandVisibility(false);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        Debug.Log("[HandModelSwitcher] Released item. Showing hand.");
        SetHandVisibility(true);
    }

    void SetHandVisibility(bool visible)
    {
        // 1. Toggle Renderers (stops them from drawing even if another script enables the GameObject)
        if (handRenderers == null || handRenderers.Length == 0)
            CacheRenderers();

        if (handRenderers != null)
        {
            for (int i = 0; i < handRenderers.Length; i++)
            {
                if (handRenderers[i] != null)
                    handRenderers[i].enabled = visible;
            }
        }

        // 2. Also toggle the GameObject itself
        if (handModel != null)
        {
            handModel.SetActive(visible);
        }
    }
}
