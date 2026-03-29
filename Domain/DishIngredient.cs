using System.Text.Json.Serialization;

namespace RecipeBook.Api.Domain;
public class DishIngredient
{
    public Guid DishId { get; set; }
    [JsonIgnore]
    public Dish? Dish { get; set; }

    public Guid ProductId { get; set; }
    [JsonIgnore]
    public Product? Product { get; set; }

    public double Grams { get; set; }
}