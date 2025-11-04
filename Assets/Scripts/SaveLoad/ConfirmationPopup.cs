using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationPopup : MonoBehaviour
{
    public Text messageText;
    private System.Action confirmationCallback;

    public Button Yes;
    public Button No;

    private void Awake()
    {
        Yes.onClick.AddListener(OnYesButtonClick);
        No.onClick.AddListener(OnNoButtonClick);
    }

    public void Show(string message, System.Action callback)
    {
        messageText.text = message;
        confirmationCallback = callback;
        gameObject.SetActive(true);
    }

    public void OnYesButtonClick()
    {
        confirmationCallback?.Invoke();
        gameObject.SetActive(false);
    }

    public void OnNoButtonClick()
    {
        gameObject.SetActive(false);
    }

}
