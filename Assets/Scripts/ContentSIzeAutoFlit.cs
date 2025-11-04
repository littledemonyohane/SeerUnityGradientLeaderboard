using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContentSIzeAutoFlit : MonoBehaviour
{
    public Transform Content;
    public RectTransform All;
    public BoxCollider2D Col;

    private Vector2 originalAllSize;
    private Vector2 originalColSize;

    private float offset = 204.88f;
    public GameObject InfoCanvas;
    private void Start()
    {
        originalAllSize = All.sizeDelta;
        originalColSize = Col.size;
    }
    private void Update()
    {
        if (Content.childCount >= 0 && Content.childCount <= 8)
        {
            All.sizeDelta = originalAllSize;
            Col.size = originalColSize;
        }
        else if (Content.childCount > 8 && Content.childCount <= 16)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y+offset);
            Col.size = new Vector2(Col.size.x, originalColSize.y+offset);
        }
        else if (Content.childCount > 16 && Content.childCount <= 24)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset*2);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 2);
        }
        else if (Content.childCount > 24 && Content.childCount <= 32)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset *3);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 3);
        }
        else if (Content.childCount > 32 && Content.childCount <= 40)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset *4);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 4);
        }
        else if (Content.childCount > 40 && Content.childCount <= 48)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset * 5);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 5);
        }
        else if (Content.childCount > 48 && Content.childCount <= 56)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset * 6);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 6);
        }
        else if (Content.childCount > 56 && Content.childCount <= 64)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset * 7);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 7);
        }
        else if (Content.childCount > 64 && Content.childCount <= 72)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset * 8);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 8);
        }
        else if (Content.childCount > 72 && Content.childCount <= 80)
        {
            All.sizeDelta = new Vector2(All.sizeDelta.x, originalAllSize.y + offset * 9);
            Col.size = new Vector2(Col.size.x, originalColSize.y + offset * 9);
        }
        else
        {
            Debug.Log("塞不下了，太多了");
            InfoCanvas.GetComponent<AlertInfo>().Info = "塞不下了，多到溢出来了";
            InfoCanvas.SetActive(true);
        }

    }
}
