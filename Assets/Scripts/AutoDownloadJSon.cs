using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DefaultNamespace;
using DefaultNamespace.Data;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AutoDownloadJson : MonoBehaviour
{
    [SerializeField] private GameObject MonsterPrefab;
    [SerializeField] private GameObject CountermarkPrefab;
    [SerializeField] private Transform MonsterPrefabParent;
    [SerializeField] private InputField inputField;
    [SerializeField] private Button button;
    [SerializeField] private InputField DateTimeText;

    private GameObject CurrentObj;
    private GameObject CurrentCountermarkObj;

    public GameObject InfoCanvas;
    [SerializeField] public Dictionary<string, int> MonsterId = new Dictionary<string, int> { };
    public int count = 0;

    //======================吧主预设默认读取列表====================
    [HideInInspector] public List<int> MonsterIdList_SSplus; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_Splus; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_S; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_A; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_B; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_C; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_D; // 自动生成的列表
    [HideInInspector] public List<int> MonsterIdList_E; // 自动生成的列表

    //======================自定义预设默认读取列表====================
    [HideInInspector] public List<int> CustomMonsterIdList_SSplus; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_Splus; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_S; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_A; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_B; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_C; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_D; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_E; // 自动生成的列表

    public string CustomPresetFileName = "CustomSaveData.txt";

    private Queue<int> textureQueue = new Queue<int>(); // 存储需要加载的图片ID的队列
    private bool isLoading = false; // 是否正在加载图片的标志

    public Transform SSplus;
    public Transform Splus;
    public Transform S;
    public Transform A;
    public Transform B;
    public Transform C;
    public Transform D;
    public Transform E;

    /// <summary>
    /// Json数据
    /// </summary>
    public static MonstersRoot petData => MonstersData.MonstersRoot;

    public PetSkinsRoot petSkinData => SkinData.PetSkinsRoot;
    private VersionRoot version;

    /// <summary>
    /// 存储version的json信息
    /// </summary>
    public string VersionText;

    /// <summary>
    /// 检测是否重复
    /// </summary>
    public bool IsDetectDuplicates = true;

    public List<petSkinList> petSkinList = new List<petSkinList>();

    public static AutoDownloadJson instance;

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

    private void Start()
    {
        // 获取最新version，里面包含所有最新的json
        version = LoadVersionData("https://seerh5.61.com/version/version.json");
        // xml目录，包含精灵数据和皮肤数据
        string url = "https://seerh5.61.com/resource/config/xml/";
        // petData
        string petDataurl = url + version.files.resource.config.xml.monsters_json;
        // petData = LoadPetData(petDataurl);
        // petSkinData
        string petSkinDataurl = url + version.files.resource.config.xml.pet_skin_json;
        // petSkinData = LoadPetSkinData(petSkinDataurl);

        // 修正皮肤ID长度
        foreach (var skin in petSkinData.PetSkins.Skin)
        {
            String skinID;
            if (skin.ID.ToString().Length == 1)
            {
                skinID = "140000" + skin.ID.ToString();
            }
            else if (skin.ID.ToString().Length == 2)
            {
                skinID = "14000" + skin.ID.ToString();
            }
            else
            {
                skinID = "1400" + skin.ID.ToString();
            }

            petSkinList.Add(new petSkinList { MonsterID = skin.MonID.ToString(), SkinID = skinID });
        }
        
        // 监听搜索按钮
        button.onClick.AddListener(OnSearch);
        DateTimeText.text = $"{DateTime.Now.Year}年{DateTime.Now.Month}月版本";
        // 从本地文件读取默认列表
        if (Application.platform == RuntimePlatform.Android)
        {
            // Android
            TextAsset AndroidText = GameObject.Find("DefaultPreset").GetComponent<GextTxtFromAndroid>().txt;
            ReadFromTxt(AndroidText.text);
        }
        else
        {
            // Windows
            //string path = Application.streamingAssetsPath + "/默认列表.txt";
            //string WindowsText = File.ReadAllText(path);
            TextAsset WindowsText = GameObject.Find("DefaultPreset").GetComponent<GextTxtFromAndroid>().txt;
            ReadFromTxt(WindowsText.text);
        }
    }

    private void Update()
    {
        if (!isLoading && textureQueue.Count > 0)
        {
            int id = textureQueue.Dequeue(); // 取出队列中的第一个ID
            //StartCoroutine(LoadTexture(id),); // 开始加载图片
        }
    }

    public void RemoveAllMonsters()
    {
        CustomerPresets.instance.Title.text = "";
        CustomerPresets.instance.SSplusInputField.text = "";
        CustomerPresets.instance.SplusInputField.text = "";
        CustomerPresets.instance.SInputField.text = "";
        CustomerPresets.instance.AInputField.text = "";
        CustomerPresets.instance.BInputField.text = "";
        CustomerPresets.instance.CInputField.text = "";
        CustomerPresets.instance.DInputField.text = "";
        CustomerPresets.instance.EInputField.text = "";
        MonsterId.Clear();
        for (int i = MonsterPrefabParent.childCount; i > 0; i--)
        {
            Destroy(MonsterPrefabParent.GetChild(i - 1).gameObject);
        }

        for (int i = SSplus.childCount; i > 0; i--)
        {
            Destroy(SSplus.GetChild(i - 1).gameObject);
        }

        for (int i = Splus.childCount; i > 0; i--)
        {
            Destroy(Splus.GetChild(i - 1).gameObject);
        }

        for (int i = S.childCount; i > 0; i--)
        {
            Destroy(S.GetChild(i - 1).gameObject);
        }

        for (int i = A.childCount; i > 0; i--)
        {
            Destroy(A.GetChild(i - 1).gameObject);
        }

        for (int i = B.childCount; i > 0; i--)
        {
            Destroy(B.GetChild(i - 1).gameObject);
        }

        for (int i = C.childCount; i > 0; i--)
        {
            Destroy(C.GetChild(i - 1).gameObject);
        }

        for (int i = D.childCount; i > 0; i--)
        {
            Destroy(D.GetChild(i - 1).gameObject);
        }

        for (int i = E.childCount; i > 0; i--)
        {
            Destroy(E.GetChild(i - 1).gameObject);
        }
    }


    public void ReadFromTxt(string DefaultSetting)
    {
        string txt = DefaultSetting;
        string[] List = txt.Split("\r", StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in List[0].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_SSplus.Add(int.Parse(item));
        }

        foreach (var item in List[1].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_Splus.Add(int.Parse(item));
        }

        foreach (var item in List[2].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_S.Add(int.Parse(item));
        }

        foreach (var item in List[3].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_A.Add(int.Parse(item));
        }

        foreach (var item in List[4].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_B.Add(int.Parse(item));
        }

        foreach (var item in List[5].Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            MonsterIdList_C.Add(int.Parse(item));
        }

        if (List.Length>6)
        {
            foreach (var item in List[6].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            {
                MonsterIdList_D.Add(int.Parse(item));
            }
        }

        if (List.Length > 7)
        {
            foreach (var item in List[7].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            {
                MonsterIdList_E.Add(int.Parse(item));
            }
        }
    }

    public void AutoSpawn()
    {
        #region SSplus

        foreach (var monster in MonsterIdList_SSplus)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, SSplus);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region Splus

        foreach (var monster in MonsterIdList_Splus)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, Splus);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region S

        foreach (var monster in MonsterIdList_S)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, S);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region A

        foreach (var monster in MonsterIdList_A)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, A);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region B

        foreach (var monster in MonsterIdList_B)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, B);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region C

        foreach (var monster in MonsterIdList_C)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, C);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region D

        foreach (var monster in MonsterIdList_D)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, D);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion
    }

    private void OnSearch()
    {
        if (inputField.text != "")
        {
            bool isExist = false;
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse(inputField.text, out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        isExist = true;
                        Debug.Log(petData.Monsters.Monster[i].DefName);
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            // 如果需要开启检测重复，就执行后面这段
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        CurrentObj = Instantiate(MonsterPrefab, MonsterPrefabParent);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);
                        //inputFikeld.text = "";
                    }
                }
                else
                {
                    if (Regex.IsMatch(petData.Monsters.Monster[i].DefName, inputField.text))
                    {
                        isExist = true;
                        Debug.Log(petData.Monsters.Monster[i].DefName);
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            // 如果需要开启检测重复，就执行后面这段
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        CurrentObj = Instantiate(MonsterPrefab, MonsterPrefabParent);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);
                        //inputField.text = "";
                    }
                }
            }

            if (!isExist)
            {
                Debug.Log("没这只精灵！");
                InfoCanvas.GetComponent<AlertInfo>().Info = "无此精灵或输入有误！";
                InfoCanvas.SetActive(true);
                //inputField.text = "";
            }
        }
    }

    /// <summary>
    /// 生成刻印对象
    /// </summary>
    /// <param name="CountermarkID"> 刻印ID </param>
    public void CountermarkSpawnTest(string CountermarkID)
    {
        // 刻印图片地址
        string address = "https://seerh5.61.com/resource/assets/countermark/icon/" +
                         LoadCountermarkData(CountermarkID + ".png");
        CurrentCountermarkObj = Instantiate(CountermarkPrefab, MonsterPrefabParent);
        // 开协程下载图片
        StartCoroutine(LoadCountermarkTexture(address, CurrentCountermarkObj));
    }

    public string LoadCountermarkData(string selectedKey)
    {
        // 将JSON字符串转换为JObject，方便动态查找
        JObject jsonObject = JObject.Parse(VersionText);
        // 获取指定key的值，例如 key = "41110.png"
        string selectedValue = jsonObject["files"]["resource"]["assets"]["countermark"]["icon"][selectedKey].ToString();

        return selectedValue;
    }


    public VersionRoot LoadVersionData(string url)
    {
        // 下载json数据
        WWW www = new WWW(url);
        while (!www.isDone)
        {
        }

        VersionText = www.text;
        // 解析json数据
        VersionRoot versionData = JsonConvert.DeserializeObject<VersionRoot>(VersionText);

        return versionData;
    }

    public static MonstersRoot LoadPetData(string url)
    {
        // 下载json数据
        WWW www = new WWW(url);
        while (!www.isDone)
        {
        }

        string jsonData = www.text;

        // 解析json数据
        MonstersRoot petData = JsonConvert.DeserializeObject<MonstersRoot>(jsonData);

        return petData;
    }

    public static PetSkinsRoot LoadPetSkinData(string url)
    {
        // 下载json数据
        WWW www = new WWW(url);
        while (!www.isDone)
        {
        }

        string jsonData = www.text;

        // 解析json数据
        PetSkinsRoot petSkinData = JsonConvert.DeserializeObject<PetSkinsRoot>(jsonData);

        return petSkinData;
    }

    void AddTextureToQueue(int id)
    {
        textureQueue.Enqueue(id); // 将需要加载的图片ID添加到队列中
    }

    // IEnumerator LoadTexture(int id, GameObject currentObj)
    // {
    //     isLoading = true; // 设置正在加载图片的标志
    //     string address;
    //     if (CustomerPresets.instance.LoadRealHeaderData(id + ".png") == "")
    //     {
    //         address = "https://seerh5.61.com/resource/assets/pet/head/" + id + ".png";
    //     }
    //     else
    //     {
    //         address = "https://seerh5.61.com/resource/assets/pet/head/" +
    //                   CustomerPresets.instance.LoadRealHeaderData(id + ".png");
    //     }
    //
    //     UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(address);
    //     yield return uwr.SendWebRequest();
    //
    //     if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
    //     {
    //         Debug.LogError(uwr.error);
    //         currentObj.transform.Find("NoImage").gameObject.SetActive(true);
    //
    //         // 等待一段时间后重试
    //         yield return new WaitForSeconds(1.0f);
    //         yield return LoadTexture(id, currentObj);
    //     }
    //     else
    //     {
    //         Texture2D t2d = DownloadHandlerTexture.GetContent(uwr);
    //         Sprite spr = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), new Vector2(0.5f, 0.5f));
    //         currentObj.transform.Find("NoImage").gameObject.SetActive(false);
    //         currentObj.transform.Find("Header").GetComponent<Image>().sprite = spr;
    //     }
    //     //=============================================
    //     DownloadRepositoryAsZipAsync.DownloadZipAsync(id);
    //     //=============================================
    //
    //     isLoading = false; // 设置加载图片完成的标志
    // }
    IEnumerator LoadTexture(int id, GameObject currentObj)
    {
        isLoading = true; // 设置正在加载图片的标志
        string address;
        address= $"https://raw.githubusercontent.com/SeerAPI/seer-unity-assets/main/newseer/assets/art/ui/assets/pet/head/{id}.png";


        // 检查本地缓存文件是否存在
        string cachePath = Path.Combine(Application.persistentDataPath, $"{id}.png");
        Texture2D t2d = null;
    
        // 如果本地缓存文件存在，直接从本地加载
        if (File.Exists(cachePath))
        {
            byte[] fileData = File.ReadAllBytes(cachePath);
            t2d = new Texture2D(200, 200);
            t2d.LoadImage(fileData);
            Debug.Log($"从本地缓存加载图片: {id}.png");
        }
        else
        {
            // 本地无缓存，从网络下载
            UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(address);
            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(uwr.error);
                currentObj.transform.Find("NoImage").gameObject.SetActive(true);

                // 等待一段时间后重试
                yield return new WaitForSeconds(1.0f);
                yield return LoadTexture(id, currentObj);
            }
            else
            {
                t2d = DownloadHandlerTexture.GetContent(uwr);
                // 保存到本地缓存
                byte[] textureBytes = t2d.EncodeToPNG();
                File.WriteAllBytes(cachePath, textureBytes);
                Debug.Log($"图片已下载并缓存: {cachePath}");
            }
        }
    
        // 应用图片到UI
        if (t2d != null)
        {
            Sprite spr = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), new Vector2(0.5f, 0.5f));
            currentObj.transform.Find("NoImage").gameObject.SetActive(false);
            currentObj.transform.Find("Header").GetComponent<Image>().sprite = spr;
        }

        isLoading = false; // 设置加载图片完成的标志
    }
    
    //================================
    IEnumerator LoadCountermarkTexture(string address, GameObject currentCountermarkObj)
    {
        //isLoading = true; // 设置正在加载图片的标志

        UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(address);
        yield return uwr.SendWebRequest();

        if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(uwr.error);
            currentCountermarkObj.transform.Find("NoImage").gameObject.SetActive(true);

            // 等待一秒重试
            yield return new WaitForSeconds(1.0f);
            yield return LoadCountermarkTexture(address, currentCountermarkObj);
        }
        else
        {
            Texture2D t2d = DownloadHandlerTexture.GetContent(uwr);
            Sprite spr = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), new Vector2(0.5f, 0.5f));
            currentCountermarkObj.transform.Find("NoImage").gameObject.SetActive(false);
            currentCountermarkObj.transform.Find("Header").GetComponent<Image>().sprite = spr;
        }

        //isLoading = false; // 设置加载图片完成的标志
    }

    public void SavePreset()
    {
        string SSplusIdList = " ";
        string SplusIdList = " ";
        string SIdList = " ";
        string AIdList = " ";
        string BIdList = " ";
        string CIdList = " ";
        string DIdList = " ";
        string EIdList = " ";

        MonsterCard[] monsterCardsSSP = SSplus.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsSSP.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsSSP)
            {
                SSplusIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsSP = Splus.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsSP.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsSP)
            {
                SplusIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsS = S.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsS.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsS)
            {
                SIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsA = A.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsA.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsA)
            {
                AIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsB = B.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsB.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsB)
            {
                BIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsC = C.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsC.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsC)
            {
                CIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsD = D.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsD.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsD)
            {
                DIdList += card.MonsterId + " ";
            }
        }

        MonsterCard[] monsterCardsE = E.GetComponentsInChildren<MonsterCard>();
        if (monsterCardsE.Length > 0)
        {
            foreach (MonsterCard card in monsterCardsE)
            {
                EIdList += card.MonsterId + " ";
            }
        }

        string Data = SSplusIdList + "\r" +
                      SplusIdList + "\r" +
                      SIdList + "\r" +
                      AIdList + "\r" +
                      BIdList + "\r" +
                      CIdList + "\r" +
                      DIdList + "\r" +
                      EIdList;
        SaveLoadPreset.SaveData(CustomPresetFileName, Data);
        InfoCanvas.GetComponent<AlertInfo>().Info = $"<color=#1CBBFF>保存成功！下次可读取当前预设！</color>";
        InfoCanvas.SetActive(true);
    }

    public void LoadPreset()
    {
        string loadedData = SaveLoadPreset.LoadData(CustomPresetFileName);
        if (loadedData != null)
        {
            string[] List = loadedData.Split("\r", StringSplitOptions.RemoveEmptyEntries);
            CustomMonsterIdList_SSplus.Clear();
            CustomMonsterIdList_Splus.Clear();
            CustomMonsterIdList_S.Clear();
            CustomMonsterIdList_A.Clear();
            CustomMonsterIdList_B.Clear();
            CustomMonsterIdList_C.Clear();
            CustomMonsterIdList_D.Clear();
            CustomMonsterIdList_E.Clear();
            for (int i = 0; i < List.Length; i++)
            {
                foreach (var item in List[i].Split(" ", StringSplitOptions.RemoveEmptyEntries))
                {
                    switch (i)
                    {
                        case 0:
                            CustomMonsterIdList_SSplus.Add(int.Parse(item));
                            break;
                        case 1:
                            CustomMonsterIdList_Splus.Add(int.Parse(item));
                            break;
                        case 2:
                            CustomMonsterIdList_S.Add(int.Parse(item));
                            break;
                        case 3:
                            CustomMonsterIdList_A.Add(int.Parse(item));
                            break;
                        case 4:
                            CustomMonsterIdList_B.Add(int.Parse(item));
                            break;
                        case 5:
                            CustomMonsterIdList_C.Add(int.Parse(item));
                            break;
                        case 6:
                            CustomMonsterIdList_D.Add(int.Parse(item));
                            break;
                        case 7:
                            CustomMonsterIdList_E.Add(int.Parse(item));
                            break;
                        default:
                            break;
                    }
                }
            }

            //if (List.Length == 6)
            //{
            //    foreach (var item in List[0].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_SSplus.Add(int.Parse(item));
            //    }
            //    foreach (var item in List[1].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_Splus.Add(int.Parse(item));
            //    }
            //    foreach (var item in List[2].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_S.Add(int.Parse(item));
            //    }
            //    foreach (var item in List[3].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_A.Add(int.Parse(item));
            //    }
            //    foreach (var item in List[4].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_B.Add(int.Parse(item));
            //    }
            //    foreach (var item in List[5].Split(" ", StringSplitOptions.RemoveEmptyEntries))
            //    {
            //        CustomMonsterIdList_C.Add(int.Parse(item));
            //    }
            //}
            AutoSpawnCustomPreset();
            InfoCanvas.GetComponent<AlertInfo>().Info = $"<color=#1CBBFF>读取成功！已自动加载预设！</color>";
            InfoCanvas.SetActive(true);
        }
        else
        {
            InfoCanvas.GetComponent<AlertInfo>().Info = $"<color=#1CBBFF>读取失败！没有的东西就是没有~</color>";
            InfoCanvas.SetActive(true);
        }
    }

    public void AutoSpawnCustomPreset()
    {
        RemoveAllMonsters();

        #region SSplus

        foreach (var monster in CustomMonsterIdList_SSplus)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, SSplus);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region Splus

        foreach (var monster in CustomMonsterIdList_Splus)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, Splus);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region S

        foreach (var monster in CustomMonsterIdList_S)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, S);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region A

        foreach (var monster in CustomMonsterIdList_A)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, A);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region B

        foreach (var monster in CustomMonsterIdList_B)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, B);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region C

        foreach (var monster in CustomMonsterIdList_C)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, C);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region D

        foreach (var monster in CustomMonsterIdList_D)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, D);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion

        #region E

        foreach (var monster in CustomMonsterIdList_E)
        {
            for (int i = 0; i < petData.Monsters.Monster.Count; i++)
            {
                if (int.TryParse($"{monster}", out int monsterId))
                {
                    if (petData.Monsters.Monster[i].ID == monsterId)
                    {
                        if (MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), count++))
                        {
                        }
                        else
                        {
                            if (IsDetectDuplicates)
                            {
                                Debug.Log("列表中已有！");
                                InfoCanvas.GetComponent<AlertInfo>().Info =
                                    $"列表中已有<color=#1CBBFF>{petData.Monsters.Monster[i].DefName}</color>！";
                                InfoCanvas.SetActive(true);
                                //inputField.text = "";
                                continue;
                            }
                        }

                        Debug.Log(petData.Monsters.Monster[i].DefName);

                        CurrentObj = Instantiate(MonsterPrefab, E);
                        CurrentObj.GetComponent<MonsterCard>().MonsterId = petData.Monsters.Monster[i].ID.ToString();
                        CurrentObj.GetComponent<MonsterCard>().MonsterName =
                            petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.name = petData.Monsters.Monster[i].DefName.ToString();
                        CurrentObj.transform.Find("Name").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].DefName;
                        CurrentObj.transform.Find("Id").GetComponent<Text>().text =
                            petData.Monsters.Monster[i].ID.ToString();
                        foreach (var skin in petSkinList)
                        {
                            if (petData.Monsters.Monster[i].ID.ToString() == skin.SkinID)
                            {
                                CurrentObj.transform.Find("Id").GetComponent<Text>().text = skin.MonsterID;
                            }
                        }

                        StartCoroutine(LoadTexture(petData.Monsters.Monster[i].ID, CurrentObj));
                        //AddTextureToQueue(petData.Monsters.Monster[i].ID);

                        //inputField.text = "";
                    }
                }
            }
        }

        #endregion
    }
}