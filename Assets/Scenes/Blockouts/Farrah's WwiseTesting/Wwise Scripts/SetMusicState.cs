using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SetMusicState : MonoBehaviour
{
    // Keeps track of region nesting
    private static List<AK.Wwise.State> _regionStates = new List<AK.Wwise.State>();
    
    public AK.Wwise.State OnTriggerEnterState; // this (gameObject's) region
    public AK.Wwise.State OnTriggerExitState; // None

    private void OnTriggerEnter(Collider other)
    {
        // If Player enters this region, then insert this region at front of regions list,
        // and update the region state in Wwise
        if (other.CompareTag("Player"))
        {
            // Debug.Log("SetMusicState - Player entered " + gameObject.name);
            
            _regionStates.Insert(0, OnTriggerEnterState);
            
            // Debug.Log("SetMusicState - RegionStates list size after entering region is now " + _regionStates.Count);
            // Debug.Log($"SetMusicState - RegionStates: [{string.Join(", ", _regionStates.Select(obj => obj != null ? obj.Name : "NULL"))}]");
            
            _regionStates[0].SetValue();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // If Player exits this region...
        if (other.CompareTag("Player"))
        {
            // Debug.Log("SetMusicState - Player exited " + gameObject.name);

            // ...then remove the innermost matching enclosing region
            _regionStates.Remove(OnTriggerEnterState);
            
            // Debug.Log("SetMusicState - RegionStates list size after region removal is now " + _regionStates.Count);
            // Debug.Log($"SetMusicState - RegionStates: [{string.Join(", ", _regionStates.Select(obj => obj != null ? obj.Name : "NULL"))}]");
            
            // If there was a region enclosing the removed region...
            if (_regionStates.Count > 0)
            {
                // Debug.Log("SetMusicState - Setting new region to the enclosing region");
                
                // ...then update region in Wwise to be that enclosing region
                _regionStates[0].SetValue();
            }
            else
            {
                // Debug.Log("SetMusicState - Setting region to " + OnTriggerExitState.Name);
                
                // ...otherwise there was no enclosing region so update the region
                // in Wwise to None to signify that Player is now outside of any region
                OnTriggerExitState.SetValue();
            }
        }
    }
}