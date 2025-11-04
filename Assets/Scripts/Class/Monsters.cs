using System.Collections.Generic;

public class Move
{
    /// <summary>
    /// 
    /// </summary>
    public int ID { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int LearningLv { get; set; }
}

public class LearnableMoves
{
    /// <summary>
    /// 
    /// </summary>
    public List<Move> Move { get; set; }
}

public class Monster
{
    /// <summary>
    /// 
    /// </summary>
    public LearnableMoves LearnableMoves { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int ID { get; set; }
    /// <summary>
    /// 布布种子
    /// </summary>
    public string DefName { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Type { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int GrowthType { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int HP { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Atk { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Def { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int SpAtk { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int SpDef { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Spd { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int YieldingExp { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int CatchRate { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string YieldingEV { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int EvolvesFrom { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int EvolvesTo { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int EvolvingLv { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int FreeForbidden { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int FuseMaster { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int FuseSub { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int Gender { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int PetClass { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int FormParam { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int CharacterAttrParam { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int GradeParam { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int AddSeParam { get; set; }
}

public class Monsters
{
    /// <summary>
    /// 
    /// </summary>
    public List<Monster> Monster { get; set; }
}

public class MonstersRoot
{
    /// <summary>
    /// 
    /// </summary>
    public Monsters Monsters { get; set; }
}

public class petSkinList{
    public string MonsterID;
    public string SkinID;
}