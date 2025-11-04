using UnityEngine;
using Newtonsoft.Json;
using System.IO;

public class PetDataLoader
{
    public static Monsters LoadPetData(string url)
    {
        // 下载json数据
        WWW www = new WWW(url);
        while (!www.isDone) { }
        string jsonData = www.text;

        // 解析json数据
        Monsters petData = JsonConvert.DeserializeObject<Monsters>(jsonData);

        return petData;
    }
}
