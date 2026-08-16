using System;
using Crease.Flying.Player.Health;
using UnityEngine;

public class WwiseEventHandlers : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event DamageTakenEvent;
    [SerializeField] private AK.Wwise.RTPC DamageAmountRTPC;

    private void OnStart()
    {
        Debug.Log("Wwise Event Handlers Started");
    }
    
    private void OnEnable() {
        Health.OnDamageTaken += DamageAmountHandler;
        Debug.Log("Wwise Event Handlers Enabled");
    }

    private void OnDisable() {
        Health.OnDamageTaken -= DamageAmountHandler;
        Debug.Log("Wwise Event Handlers Disabled");
    }

    private void DamageAmountHandler(float damageAmount, DamageType damageType) {
        Debug.Log("Wwise Damage Amount: " + damageAmount);
        DamageAmountRTPC.SetGlobalValue(damageAmount);
        DamageTakenEvent.Post(gameObject);
    }
}