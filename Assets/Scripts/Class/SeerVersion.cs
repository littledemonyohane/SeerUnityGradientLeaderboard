using Newtonsoft.Json;

// ??????
//{
//    "version": 1702629180974,
//	"files": {
//        "resource": {
//            "config": {
//                "xml": {
//                    "pet_skin.json": "pet_skin_e67de108.json",
//					"monsters.json": "monsters_27ac4946.json"

//                }
//            },
//			"assets": {
//                "countermark": {
//                    "icon": {
//                        "2301013.png": "2301013_38aed004.png"

//                    }
//                }
//            }
//        }
//    }
//}

public class Xml
{
    /// <summary>
    /// 
    /// </summary>
    [JsonProperty("monsters.json")]
    public string monsters_json { get; set; }

    [JsonProperty("pet_skin.json")]
    public string pet_skin_json { get; set; }
}

public class Config
{
    /// <summary>
    /// 
    /// </summary>
    public Xml xml { get; set; }
}

public class Resource
{
    /// <summary>
    /// 
    /// </summary>
    public Config config { get; set; }
}

public class Files
{
    /// <summary>
    /// 
    /// </summary>
    public Resource resource { get; set; }
}

public class VersionRoot
{
    /// <summary>
    /// 
    /// </summary>
    public long version { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public Files files { get; set; }
}
