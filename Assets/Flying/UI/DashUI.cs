using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DashUI : MonoBehaviour
{
    [SerializeField] private DashController dashController;
    [SerializeField] private Image rechargeBar;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;
    }

    // Update is called once per frame
    void Update()
    {
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;
    }
}
