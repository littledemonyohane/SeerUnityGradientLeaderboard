using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveLoadPreset
{
    public static void SaveData(string fileName, string data)
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }
        File.WriteAllText(filePath, data);
    }

    public static string LoadData(string fileName)
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
        else
        {
            return null;
        }
    }

    
}
