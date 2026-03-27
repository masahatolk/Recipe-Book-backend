namespace RecipeBook.Api.Domain;

public class Dish
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<string> Photos { get; set; } = [];
    public Nutrition NutritionPerPortion { get; set; } = new();
    public double PortionSizeGrams { get; set; }
    public DishCategory Category { get; set; }
    public HashSet<ExtraFlag> Flags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<DishIngredient> Ingredients { get; set; } = [];
}