using UnityEngine;

public class InkTriggerScript : MonoBehaviour
{
    [SerializeField] private InkTextManager inkTextManager;

    [TextArea(2, 5)]
    [SerializeField] private string textToShow;

    [SerializeField] private string playerTag = "Player";

    private bool hasBeenCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenCollected)
            return;

        if (!other.CompareTag(playerTag))
            return;

        hasBeenCollected = true;

        if (inkTextManager != null)
        {
            inkTextManager.PlayText(textToShow);
        }
        else
        {
            Debug.LogWarning($"{name} is missing an InkTextManager reference.");
        }
    }
}