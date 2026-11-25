namespace Metal_Code.Models
{
    public class PartLabel
    {
        public int PositionNo { get; init; }
        public string? Title { get; init; }
        public object? Count { get; init; }
        public double Mass { get; init; }
        public string? MaterialDisplay { get; init; }
        public string? Description { get; init; }
        public string? Dimensions { get; init; }
        public bool IsPurchase { get; init; }
    }
}