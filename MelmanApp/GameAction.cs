namespace MelmanApp
{
    public class GameAction
    {
        public string Type { get; set; } = default!;
        public int? Amount { get; set; }
        public int? TargetId { get; set; }
        public int? TroopCount { get; set; }
    }

}
