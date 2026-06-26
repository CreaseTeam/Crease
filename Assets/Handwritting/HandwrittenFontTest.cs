using Crease.Handwritting;
using UnityEngine;

public class HandwrittenFontTest : MonoBehaviour
{
    [SerializeField] HandwrittenTextPlayer _player;
    [SerializeField] string _text = "Hello";
    [SerializeField] bool _instant;

    void Start()
    {
        if (_instant)
            _player.ShowInstant(_text);
        else
            _player.PlayWriteIn(_text);
    }
}