using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MonsterCard : MonoBehaviour
{
    public Transform OnDragParent;
    private GameObject CloneObj;
    public string MonsterId;
    public string MonsterName;
    public AutoDownloadJson ADLJ;

    public GameObject temp;

    private void Start()
    {
        OnDragParent = GameObject.Find("OnDragParent").transform;
        ADLJ = GameObject.Find("AutoDownloadJSon").transform.GetComponent<AutoDownloadJson>();
    }

    public void OnBeginDrag(BaseEventData data)
    {
        //Debug.Log("OnBegin");
        CloneObj = Instantiate(transform.gameObject, OnDragParent);
        CloneObj.transform.position = transform.position;
        transform.GetComponent<Animator>().enabled = false;
        transform.GetComponent<CanvasGroup>().alpha = 0;
        //transform.GetComponent<BoxCollider2D>().enabled = false;
    }

    public void OnDrag(BaseEventData data)
    {
        PointerEventData pointEventData = data as PointerEventData;
        CloneObj.transform.position = TranslateScreenToWorld(pointEventData.position);
        //Debug.Log("OnDrag"+ TranslateScreenToWorld(pointEventData.position));
        Collider2D col = Physics2D.OverlapPoint(CloneObj.transform.position);
        Collider2D[] Col = new Collider2D[] { col };
        bool isCol = false;
        if (Col[0] != null && Col[0].tag == "InsertCollider")
        {
            Debug.Log($"碰到InsertCollider框并触发提示动画");
            if (temp)
            {
                temp.SetActive(false);// 注释这行可以开启悬浮插入提示
                temp = Col[0].transform.Find("hint").gameObject;
                temp.SetActive(true);
            }
        }
        else
        {
            if (temp)
            {
                temp.SetActive(false);
            }
        }
    }

    public void OnEndDrag(BaseEventData data)
    {
        if (temp)
        {
            temp.SetActive(false);
        }
        //Debug.Log("OnEnd");
        PointerEventData pointEventData = data as PointerEventData;
        //Collider2D CloneObjCollider = CloneObj.transform.GetComponent<Collider2D>();
        //Collider2D selfCollider = transform.GetComponent<Collider2D>();
        //Physics2D.IgnoreCollision(CloneObjCollider, selfCollider);
        CloneObj.transform.GetComponent<Collider2D>().enabled = false;

        Collider2D col = Physics2D.OverlapPoint(TranslateScreenToWorld(pointEventData.position));
        Vector2 what = TranslateScreenToWorld(pointEventData.position);
        Debug.Log("让我看看转换了个啥坐标" + what);
        Collider2D[] Col = new Collider2D[] { col };
        bool isCol = false;
        if (Col[0] != null)
        {
            foreach (Collider2D c in Col)
            {
                if (c.tag == "SearchList")
                {
                    Debug.Log($"碰到SearchList");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                    break;
                }
                else if (c.tag == "SetFirstCollider")
                {
                    Debug.Log($"碰到SetFirstCollider框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    CloneObj.transform.SetAsFirstSibling();
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "InsertCollider")
                {
                    Debug.Log($"碰到InsertCollider框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.parent.parent);
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    CloneObj.transform.SetSiblingIndex(c.transform.parent.parent.GetSiblingIndex() + 1);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "MonsterCard")
                {
                    Debug.Log($"碰到MonsterCard——{c.name}");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent);
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    CloneObj.transform.SetSiblingIndex(c.transform.GetSiblingIndex());
                    c.transform.SetParent(transform.parent);
                    c.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    c.transform.SetSiblingIndex(transform.GetSiblingIndex());
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "SS+")
                {
                    Debug.Log($"碰到SS+框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "S+")
                {
                    Debug.Log($"碰到S+框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "S")
                {
                    Debug.Log($"碰到S框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "A")
                {
                    Debug.Log($"碰到A框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "B")
                {
                    Debug.Log($"碰到B框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "C")
                {
                    Debug.Log($"碰到C框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "D")
                {
                    Debug.Log($"碰到D框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
                else if (c.tag == "E")
                {
                    Debug.Log($"碰到E框");
                    isCol = true;
                    CloneObj.transform.SetParent(c.transform.parent.gameObject.transform.Find("Content"));
                    CloneObj.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(222f, 271f);
                    Destroy(transform.gameObject);
                }
            }
        }
        if (!isCol)
        {
            Destroy(CloneObj);
            transform.GetComponent<Animator>().enabled = true;
            transform.GetComponent<CanvasGroup>().alpha = 1;
            //transform.GetComponent<BoxCollider2D>().enabled = true;
        }
        CloneObj.transform.GetComponent<Collider2D>().enabled = true;

    }

    public static Vector2 TranslateScreenToWorld(Vector3 position)
    {
        Vector3 cameraTranslatePos = Camera.main.ScreenToWorldPoint(position);
        return new Vector2(cameraTranslatePos.x, cameraTranslatePos.y);
        //return Camera.main.ScreenToWorldPoint(position);

    }

    public void DestroyMonsterCard()
    {
        Destroy(transform.gameObject);
        ADLJ.MonsterId.Remove(MonsterId);

    }
}
