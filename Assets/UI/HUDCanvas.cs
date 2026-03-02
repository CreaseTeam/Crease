using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class HUDCanvas : MonoBehaviour
{
    [SerializeField] private DashController dashController;
    [SerializeField] private Image rechargeBar;
    [SerializeField] private TextMeshProUGUI collectibleText;
    [SerializeField] private List<Heart> hearts;

    [SerializeField] private GameObject foldingUI;
    [SerializeField] private GameObject flyingUI;
    [SerializeField] private GameObject checkpointUI;


    public static HUDCanvas Instance { get; private set; }

    private int collectibleCount = 0;
    private int maxHealth = 5;
    private int health = 5;

    public int Collect()
    {
        collectibleCount++;
        collectibleText.text = $"{collectibleCount}";
        return collectibleCount;
    }

    public void TakeDamage()
    {
        if (health > 0)
        {
            health--;
            UpdateHearts();
        }
    }

    public void Heal()
    {
        if (health < maxHealth)
        {
            health++;
            UpdateHearts();
        }
    }

    private void UpdateHearts()
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            hearts[i].SetHealth(i < health);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        collectibleText.text = $"{collectibleCount}";
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;

        maxHealth = hearts.Count;
        health = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;
    }

    public void ShowFoldingUI(bool show)
    {
        foldingUI.SetActive(show);
        flyingUI.SetActive(!show);
    }

    public void ShowFlyingUI(bool show)
    {
        flyingUI.SetActive(show);
        foldingUI.SetActive(!show);
    }

    public void ShowCheckpointUI(bool show)
    {
        checkpointUI.SetActive(show);
    }
}
