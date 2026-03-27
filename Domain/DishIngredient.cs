namespace RecipeBook.Api.Domain;

public class DishIngredient
{
    public Guid DishId { get; set; }
    public Dish? Dish { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public double Grams { get; set; }
}