using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TempSpriteOrderLinker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _sprites;
    [SerializeField] private SpriteRenderer _targetSprite;

    private void Update()
    {
        foreach(var sprite in _sprites)
        {
            if (sprite.sortingOrder != _targetSprite.sortingOrder)
            {
                sprite.sortingOrder = _targetSprite.sortingOrder;
            }
        }
    }
}
