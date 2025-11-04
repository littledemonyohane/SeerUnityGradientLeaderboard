using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SaveLoadPanelTrigger : MonoBehaviour
{
    [SerializeField] private GameObject SaveLoadPrefab;
    
    private List<GameObject> PresetList=new List<GameObject>();
    
    public static SaveLoadPanelTrigger instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    private void OnEnable()
    {
        Init();
    }
    private void OnDisable()
    {
        Destroy();
    }
    
    public void Init()
    {
        string folderPath = Application.persistentDataPath;
        string[] txtFiles = Directory.GetFiles(folderPath, "*.txt");

        foreach (string filePath in txtFiles)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            GameObject obj= Instantiate(SaveLoadPrefab, SaveLoadPrefab.transform.position, SaveLoadPrefab.transform.rotation);
            obj.transform.Find("title").GetComponent<InputField>().text = fileNameWithoutExtension;
            obj.transform.Find("Load").GetComponent<Button>().interactable=true;
            obj.transform.SetParent(SaveLoadPrefab.transform.parent);
            obj.SetActive(true);
            PresetList.Add(obj);
            obj.transform.localScale = new Vector3(1, 1, 1);
            Debug.Log(fileNameWithoutExtension);
        }
    }

    public void Destroy()
    {
        foreach (var obj in PresetList)
        {
            Destroy(obj);
        }
        PresetList.Clear();
    }

    public void InstantiateSaveLoad()
    {
        GameObject obj= Instantiate(SaveLoadPrefab, SaveLoadPrefab.transform.position, SaveLoadPrefab.transform.rotation);
        obj.transform.SetParent(SaveLoadPrefab.transform.parent);
        PresetList.Add(obj);
        obj.transform.localScale = new Vector3(1, 1, 1);
        obj.SetActive(true);
    }
    
}
