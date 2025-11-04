using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class CustomerPresetsObject : MonoBehaviour
{
    public ConfirmationPopup confirmationPopupPrefab;
    public Transform confirmationPopupPrefabParent;
    public GameObject Canvas;
    public void SavePreset()
    {
        ShowConfirmationPopup("确定<color=red>保存</color>保存吗？\n（同名会覆盖哦）", Save);
    }

    private void Save()
    {
        string filename = transform.Find("title").GetComponent<InputField>().text;
        CustomerPresets.instance.SavePreset(filename);
        SaveLoadPanelTrigger.instance.Destroy();
        SaveLoadPanelTrigger.instance.Init();
        Canvas.SetActive(false);
    }

    public void LoadPreset()
    {
        ShowConfirmationPopup("确定<color=red>读取</color>吗？\n（会丢失当前所有操作哦）", Load);
    }

    private void Load()
    {
        string filename = transform.Find("title").GetComponent<InputField>().text;
        if (filename != "")
        {
            CustomerPresets.instance.LoadPreset(filename);
        }
        else
        {
            Debug.LogError("空白存档不能读取");
        }
        Canvas.SetActive(false);
    }

    private void ShowConfirmationPopup(string message, System.Action callback)
    {
        ConfirmationPopup popup = Instantiate(confirmationPopupPrefab,confirmationPopupPrefabParent);
        popup.Show(message, callback);
    }
    
    public void DeleteSaveLoad()
    {
        if (transform.Find("title").GetComponent<InputField>().text=="")
        {
            Delete();
        }
        else
        {
            ShowConfirmationPopup("确定<color=red>删除</color>吗？\n（无法复原!）", Delete);
        }
    }

    private void Delete()
    {
        Destroy(transform.gameObject);
        string folderPath = Application.persistentDataPath;
        string[] txtFiles = Directory.GetFiles(folderPath, "*.txt");

        foreach (string filePath in txtFiles)
        {
            if (Path.GetFileName(filePath) == transform.Find("title").GetComponent<InputField>().text + ".txt")
            {
                File.Delete(filePath);
            }
        }
        // SaveLoadPanelTrigger.instance.Destroy();
        // SaveLoadPanelTrigger.instance.Init();
    }
}