using RecipeBook.Api.Domain;

namespace RecipeBook.Api.Contracts;

public record ProductUpsertRequest(
    string Name,
    List<string>? Photos,
    Nutrition NutritionPer100g,
    string? Composition,
    ProductCategory Category,
    CookingRequirement CookingRequirement,
    HashSet<ExtraFlag>? Flags
);

public record DishIngredientRequest(Guid ProductId, double Grams);

public record DishUpsertRequest(
    string Name,
    List<string>? Photos,
    Nutrition NutritionPerPortion,
    List<DishIngredientRequest> Ingredients,
    double PortionSizeGrams,
    DishCategory? Category,
    HashSet<ExtraFlag>? Flags
);

public record DishCalculationResponse(Nutrition Nutrition, HashSet<ExtraFlag> AvailableFlags);

public record ProductDeletionBlockedResponse(string Message, List<string> DishNames);

public record ApiErrorResponse(string Error);