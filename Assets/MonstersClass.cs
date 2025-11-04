namespace DefaultNamespace
{
    public class LearnableMoves
    {
        public object AdvMove { get; set; }
        public Move[] Move { get; set; }
        public object SpMove { get; set; }
    }

    public class Move
    {
        public int ID { get; set; }
        public int LearningLv { get; set; }
        public int Rec { get; set; }
        public int Tag { get; set; }
    }

    public class Monster
    {
        public string DefName { get; set; }
        public object ExtraMoves { get; set; }
        public LearnableMoves LearnableMoves { get; set; }
        public object Move { get; set; }
        public object ShowExtraMoves { get; set; }
        public object SpExtraMoves { get; set; }
        public int Atk { get; set; }
        public int CharacterAttrParam { get; set; }
        public int Combo { get; set; }
        public int Def { get; set; }
        public int EvolvesTo { get; set; }
        public int EvolvFlag { get; set; }
        public int EvolvingLv { get; set; }
        public int FreeForbidden { get; set; }
        public int Gender { get; set; }
        public int HP { get; set; }
        public int ID { get; set; }
        public int isFlyPet { get; set; }
        public int isRidePet { get; set; }
        public int PetClass { get; set; }
        public int RealId { get; set; }
        public int SpAtk { get; set; }
        public int Spd { get; set; }
        public int SpDef { get; set; }
        public int Support { get; set; }
        public int Transform { get; set; }
        public int Type { get; set; }
        public int Vip { get; set; }
    }

    public class Monsters
    {
        public Monster[] Monster { get; set; }
    }

    public class Root
    {
        public Monsters Monsters { get; set; }
    }

}