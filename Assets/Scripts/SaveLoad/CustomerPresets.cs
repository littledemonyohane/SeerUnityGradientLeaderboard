using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CustomerPresets : MonoBehaviour
{
    public static CustomerPresets instance;
    [SerializeField] private AutoDownloadJson ADLJ;

    private bool isDetectDuplicates;
    private MonstersRoot petData;

    [SerializeField] private Transform SSplus;
    [SerializeField] private Transform Splus;
    [SerializeField] private Transform S;
    [SerializeField] private Transform A;
    [SerializeField] private Transform B;
    [SerializeField] private Transform C;
    [SerializeField] private Transform D;
    [SerializeField] private Transform E;

    [SerializeField] private GameObject InfoCanvas;

    [SerializeField] private Transform MonsterPrefabParent;

    [SerializeField] private GameObject MonsterPrefab;

    [SerializeField] private InputField _inputField;

    [SerializeField] private GameObject SaveLoadPanel;

    [SerializeField] public InputField SSplusInputField;
    [SerializeField] public InputField SplusInputField;
    [SerializeField] public InputField SInputField;
    [SerializeField] public InputField AInputField;
    [SerializeField] public InputField BInputField;
    [SerializeField] public InputField CInputField;
    [SerializeField] public InputField DInputField;
    [SerializeField] public InputField EInputField;

    private GameObject CurrentObj;

    //======================自定义预设默认读取列表====================
    [HideInInspector] public List<int> CustomMonsterIdList_SSplus; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_Splus; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_S; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_A; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_B; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_C; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_D; // 自动生成的列表
    [HideInInspector] public List<int> CustomMonsterIdList_E; // 自动生成的列表

    private JObject jsonObject;
    public string VersionText;

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
        isDetectDuplicates = ADLJ.IsDetectDuplicates;
        // petData = ADLJ.petData;
        petData = AutoDownloadJson.petData;

        // 为了获取version下所有的东西
        LoadVersionData("https://seerh5.61.com/version/version.json");
    }


    private string GetCurrentTitle()
    {
        return _inputField.text;
    }

    public InputField Title
    {
        get { return _inputField; }
        set { _inputField = value; }
    }

    public GameObject InfoPanel
    {
        get { return InfoCanvas; }
        set { InfoCanvas = value; }
    }

    public void RemoveAllMonsters()
    {
        ADLJ.MonsterId.Clear();
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

    public void SavePreset(string saveName)
    {
        string SSplusIdList = " ";
        string SplusIdList = " ";
        string SIdList = " ";
        string AIdList = " ";
        string BIdList = " ";
        string CIdList = " ";
        string DIdList = " ";
        string EIdList = " ";

        #region 获取当前所有精灵

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

        #endregion

        string Data = " " + GetCurrentTitle() + "\r" +
                      " " + SSplusInputField.text + "\r" +
                      " " + SplusInputField.text + "\r" +
                      " " + SInputField.text + "\r" +
                      " " + AInputField.text + "\r" +
                      " " + BInputField.text + "\r" +
                      " " + CInputField.text + "\r" +
                      " " + DInputField.text + "\r" +
                      " " + EInputField.text + "\r" +
                      SSplusIdList + "\r" +
                      SplusIdList + "\r" +
                      SIdList + "\r" +
                      AIdList + "\r" +
                      BIdList + "\r" +
                      CIdList + "\r" +
                      DIdList + "\r" +
                      EIdList;
        if (saveName == "")
        {
            InfoCanvas.GetComponent<AlertInfo>().Info = $"<color=#1CBBFF>存档名称不能为空！</color>";
            InfoCanvas.SetActive(true);
        }
        else
        {
            SaveLoadPreset.SaveData(saveName + ".txt", Data);
            InfoCanvas.GetComponent<AlertInfo>().Info = $"<color=#1CBBFF>保存成功！下次可读取当前预设！</color>";
            InfoCanvas.SetActive(true);
        }
    }

    public void LoadPreset(string filename)
    {
        string loadedData = SaveLoadPreset.LoadData(filename + ".txt");
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
                            _inputField.text = item;
                            break;
                        case 1:
                            SSplusInputField.text = item;
                            break;
                        case 2:
                            SplusInputField.text = item;
                            break;
                        case 3:
                            SInputField.text = item;
                            break;
                        case 4:
                            AInputField.text = item;
                            break;
                        case 5:
                            BInputField.text = item;
                            break;
                        case 6:
                            CInputField.text = item;
                            break;
                        case 7:
                            DInputField.text = item;
                            break;
                        case 8:
                            EInputField.text = item;
                            break;
                        case 9:
                            CustomMonsterIdList_SSplus.Add(int.Parse(item));
                            break;
                        case 10:
                            CustomMonsterIdList_Splus.Add(int.Parse(item));
                            break;
                        case 11:
                            CustomMonsterIdList_S.Add(int.Parse(item));
                            break;
                        case 12:
                            CustomMonsterIdList_A.Add(int.Parse(item));
                            break;
                        case 13:
                            CustomMonsterIdList_B.Add(int.Parse(item));
                            break;
                        case 14:
                            CustomMonsterIdList_C.Add(int.Parse(item));
                            break;
                        case 15:
                            CustomMonsterIdList_D.Add(int.Parse(item));
                            break;
                        case 16:
                            CustomMonsterIdList_E.Add(int.Parse(item));
                            break;
                        default:
                            break;
                    }
                }
            }

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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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
                        if (ADLJ.MonsterId.TryAdd(petData.Monsters.Monster[i].ID.ToString(), ADLJ.count++))
                        {
                        }
                        else
                        {
                            if (isDetectDuplicates)
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
                        foreach (var skin in ADLJ.petSkinList)
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


    IEnumerator LoadTexture(int id, GameObject currentObj)
    {
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

    }

    /// <summary>
    /// 部分精灵真实头像
    /// </summary>
    /// <param name="selectedKey"></param>
    /// <returns></returns>
    public string LoadRealHeaderData(string selectedKey)
    {
        string selectedValue;
        // 获取指定key的值，例如 key = "41110.png"
        if (jsonObject["files"]["resource"]["assets"]["pet"]["head"][selectedKey]!= null)
        {
            selectedValue = jsonObject["files"]["resource"]["assets"]["pet"]["head"][selectedKey].ToString();
        }
        else
        {
            selectedValue = "";
        }
        return selectedValue;
    }

    public void LoadVersionData(string url)
    {
        // 下载json数据
        WWW www = new WWW(url);
        while (!www.isDone)
        {
        }

        VersionText = www.text;
        // 将JSON字符串转换为JObject，方便动态查找
        jsonObject = JObject.Parse(VersionText);
    }
}