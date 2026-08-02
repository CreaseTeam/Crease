using TMPro;
using UnityEngine;
using TMPro;

public class InkTriggerScript : MonoBehaviour
{
    public TextMeshProUGUI InkTextMesh;

    // Update is called once per frame
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Ink"))
        {
            InkTextMesh.text = "Make sure to like and subscribe";
        }
    }
}
