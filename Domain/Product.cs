namespace RecipeBook.Api.Domain;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<string> Photos { get; set; } = [];
    public Nutrition NutritionPer100g { get; set; } = new();
    public string? Composition { get; set; }
    public ProductCategory Category { get; set; }
    public CookingRequirement CookingRequirement { get; set; }
    public HashSet<ExtraFlag> Flags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<DishIngredient> DishIngredients { get; set; } = [];
}