using UnityEngine;

/// <summary>
/// Contract for anything the player can interact with.
/// </summary>
public interface IInteractable
{
    void Interact(GameObject interactor);
}
