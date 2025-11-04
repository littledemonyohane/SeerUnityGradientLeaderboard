using System.Collections.Generic;

public class SkinKind
{
    /// <summary>
    /// 
    /// </summary>
    public int ID { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Type { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int LifeTime { get; set; }
}

public class Skin
{
    /// <summary>
    /// 
    /// </summary>
    public List<SkinKind> SkinKind { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int ID { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int MonID { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Target { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Occasion { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int AddWay { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Type { get; set; }
    /// <summary>
    /// 漆墨白羽·米瑞斯
    /// </summary>
    public string Name { get; set; }
}

public class PetSkins
{
    /// <summary>
    /// 
    /// </summary>
    public List<Skin> Skin { get; set; }
}

public class PetSkinsRoot
{
    /// <summary>
    /// 
    /// </summary>
    public PetSkins PetSkins { get; set; }
}
