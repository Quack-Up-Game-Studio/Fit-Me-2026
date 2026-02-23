using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SanityCheck : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private AnimationCurve animationCurve;
    public void Update()
    {
        Debug.Log($"Over GOBJ: {EventSystem.current.IsPointerOverGameObject()}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("OnBeginDrag");
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("OnDrag");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("OnEndDrag");
    }
}
