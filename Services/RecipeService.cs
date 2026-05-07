using Microsoft.EntityFrameworkCore;
using RecipeBook.Api.Contracts;
using RecipeBook.Api.Data;
using RecipeBook.Api.Domain;

namespace RecipeBook.Api.Services;

public class ProductDeletionBlockedException(List<string> dishNames) : Exception("Product is used in dishes")
{
    public List<string> DishNames { get; } = dishNames;
}

public class RecipeService(RecipeBookDbContext db)
{
    private static readonly Dictionary<string, DishCategory> MacroMap = new()
    {
        ["!десерт"] = DishCategory.DESSERT,
        ["!первое"] = DishCategory.FIRST_COURSE,
        ["!второе"] = DishCategory.SECOND_COURSE,
        ["!напиток"] = DishCategory.DRINK,
        ["!салат"] = DishCategory.SALAD,
        ["!суп"] = DishCategory.SOUP,
        ["!перекус"] = DishCategory.SNACK
    };

    public async Task<List<Product>> ListProductsAsync(string? category, string? cookingRequirement, List<ExtraFlag> flags, string? query, string sortBy, string direction)
    {
        var products = await db.Products.AsNoTracking().ToListAsync();

        if (Enum.TryParse<ProductCategory>(category, true, out var categoryValue))
            products = products.Where(x => x.Category == categoryValue).ToList();

        if (Enum.TryParse<CookingRequirement>(cookingRequirement, true, out var cookingValue))
            products = products.Where(x => x.CookingRequirement == cookingValue).ToList();

        if (flags.Count > 0)
            products = products.Where(x => flags.All(f => x.Flags.Contains(f))).ToList();

        if (!string.IsNullOrWhiteSpace(query))
            products = products.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        var ordered = sortBy.ToLowerInvariant() switch
        {
            "calories" => products.OrderBy(x => x.NutritionPer100g.Calories),
            "proteins" => products.OrderBy(x => x.NutritionPer100g.Proteins),
            "fats" => products.OrderBy(x => x.NutritionPer100g.Fats),
            "carbs" => products.OrderBy(x => x.NutritionPer100g.Carbs),
            _ => products.OrderBy(x => x.Name)
        };

        return direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? ordered.Reverse().ToList() : ordered.ToList();
    }

    public async Task<Product> CreateProductAsync(ProductUpsertRequest request)
    {
        ValidateProduct(request);
        var entity = new Product
        {
            Name = request.Name.Trim(),
            Photos = NormalizePhotos(request.Photos),
            NutritionPer100g = request.NutritionPer100g,
            Composition = string.IsNullOrWhiteSpace(request.Composition) ? null : request.Composition.Trim(),
            Category = request.Category,
            CookingRequirement = request.CookingRequirement,
            Flags = request.Flags ?? [],
            CreatedAt = DateTime.UtcNow
        };
        db.Products.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<Product?> GetProductAsync(Guid id) => await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<Product?> UpdateProductAsync(Guid id, ProductUpsertRequest request)
    {
        ValidateProduct(request);
        var existing = await db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null) return null;

        existing.Name = request.Name.Trim();
        existing.Photos = NormalizePhotos(request.Photos);
        existing.NutritionPer100g = request.NutritionPer100g;
        existing.Composition = string.IsNullOrWhiteSpace(request.Composition) ? null : request.Composition.Trim();
        existing.Category = request.Category;
        existing.CookingRequirement = request.CookingRequirement;
        existing.Flags = request.Flags ?? [];
        existing.UpdatedAt = DateTime.UtcNow;

        await SyncDishFlagsForProductAsync(id);
        await db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteProductAsync(Guid id)
    {
        var usingDishes = await db.Dishes
            .Include(x => x.Ingredients)
            .Where(d => d.Ingredients.Any(i => i.ProductId == id))
            .Select(x => x.Name)
            .ToListAsync();

        if (usingDishes.Count > 0)
            throw new ProductDeletionBlockedException(usingDishes);

        var entity = await db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;
        db.Products.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<List<Dish>> ListDishesAsync(string? category, List<ExtraFlag> flags, string? query)
    {
        var dishes = await db.Dishes.Include(x => x.Ingredients).AsNoTracking().ToListAsync();

        if (Enum.TryParse<DishCategory>(category, true, out var categoryValue))
            dishes = dishes.Where(x => x.Category == categoryValue).ToList();

        if (flags.Count > 0)
            dishes = dishes.Where(x => flags.All(f => x.Flags.Contains(f))).ToList();

        if (!string.IsNullOrWhiteSpace(query))
            dishes = dishes.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return dishes.OrderBy(x => x.Name).ToList();
    }

    public async Task<Dish> CreateDishAsync(DishUpsertRequest request)
    {
        var normalized = await NormalizeDishRequest(request, null);

        var dish = new Dish
        {
            Name = normalized.Name,
            Photos = NormalizePhotos(normalized.Photos),
            NutritionPerPortion = normalized.NutritionPerPortion,
            PortionSizeGrams = normalized.PortionSizeGrams,
            Category = normalized.Category!.Value,
            Flags = normalized.Flags ?? [],
            CreatedAt = DateTime.UtcNow,
            Ingredients = normalized.Ingredients.Select(x => new DishIngredient { ProductId = x.ProductId, Grams = x.Grams }).ToList()
        };

        db.Dishes.Add(dish);
        await db.SaveChangesAsync();
        return dish;
    }

    public async Task<Dish?> GetDishAsync(Guid id)
    {
        return await db.Dishes.Include(x => x.Ingredients).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Dish?> UpdateDishAsync(Guid id, DishUpsertRequest request)
    {
        var existing = await db.Dishes.Include(x => x.Ingredients).FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null) return null;

        var normalized = await NormalizeDishRequest(request, existing.Category);

        existing.Name = normalized.Name;
        existing.Photos = NormalizePhotos(normalized.Photos);
        existing.NutritionPerPortion = normalized.NutritionPerPortion;
        existing.PortionSizeGrams = normalized.PortionSizeGrams;
        existing.Category = normalized.Category!.Value;
        existing.Flags = normalized.Flags ?? [];
        existing.UpdatedAt = DateTime.UtcNow;

        db.DishIngredients.RemoveRange(existing.Ingredients);
        existing.Ingredients = normalized.Ingredients.Select(x => new DishIngredient
        {
            DishId = existing.Id,
            ProductId = x.ProductId,
            Grams = x.Grams
        }).ToList();

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteDishAsync(Guid id)
    {
        var existing = await db.Dishes.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null) return;
        db.Dishes.Remove(existing);
        await db.SaveChangesAsync();
    }

    public async Task<DishCalculationResponse> CalculateAsync(List<DishIngredientRequest> ingredients)
    {
        if (ingredients.Count == 0) throw new ArgumentException("Dish should contain at least one ingredient");
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var nutrition = CalculateNutrition(ingredients, products);
        var availableFlags = CalculateAvailableFlags(ingredients, products);
        return new DishCalculationResponse(nutrition, availableFlags);
    }

    private async Task<DishUpsertRequest> NormalizeDishRequest(DishUpsertRequest request, DishCategory? fallbackCategory)
    {
        ValidateDish(request);
        var products = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var calculatedNutrition = CalculateNutrition(request.Ingredients, products);
        ValidateNutrition(calculatedNutrition, false, request.PortionSizeGrams);
        
        var hasManualCategory = request.Category is not null;
        var (cleanName, macroCategory) = hasManualCategory
            ? (request.Name.Trim(), (DishCategory?)null)
            : ResolveMacroCategory(request.Name);
        var selectedCategory = request.Category ?? macroCategory ?? fallbackCategory;
        if (selectedCategory is null)
            throw new ArgumentException("Укажите категорию или макрос в названии");
        if (!hasManualCategory && cleanName.Length < 2)
            throw new ArgumentException("Название блюда: минимум 2 символа");

        var availableFlags = CalculateAvailableFlags(request.Ingredients, products);
        var normalizedFlags = (request.Flags ?? []).Where(availableFlags.Contains).ToHashSet();

        return request with
        {
            Name = cleanName,
            NutritionPerPortion = calculatedNutrition,
            Category = selectedCategory,
            Flags = normalizedFlags
        };
    }

    private static (string Name, DishCategory? Category) ResolveMacroCategory(string input)
    {
        var normalized = input.Trim();
        var lower = normalized.ToLowerInvariant();

        var bestMatch = MacroMap
            .Select(x => new { x.Key, x.Value, Index = lower.IndexOf(x.Key, StringComparison.Ordinal) })
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .FirstOrDefault();

        if (bestMatch is null) return (normalized, null);

        var cleaned = (normalized[..bestMatch.Index] + normalized[(bestMatch.Index + bestMatch.Key.Length)..]).Trim();
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return (cleaned, bestMatch.Value);
    }

    private static Nutrition CalculateNutrition(List<DishIngredientRequest> ingredients, IReadOnlyDictionary<Guid, Product> products)
    {
        double Sum(Func<Nutrition, double> selector) => ingredients.Sum(item =>
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new ArgumentException($"Product {item.ProductId} not found");
            return selector(product.NutritionPer100g) * item.Grams / 100.0;
        });

        return new Nutrition
        {
            Calories = Sum(x => x.Calories),
            Proteins = Sum(x => x.Proteins),
            Fats = Sum(x => x.Fats),
            Carbs = Sum(x => x.Carbs)
        };
    }

    private static HashSet<ExtraFlag> CalculateAvailableFlags(List<DishIngredientRequest> ingredients, IReadOnlyDictionary<Guid, Product> products)
    {
        if (ingredients.Count == 0) return [];
        var result = new HashSet<ExtraFlag>();
        foreach (var flag in Enum.GetValues<ExtraFlag>())
        {
            var allHaveFlag = ingredients.All(ingredient =>
                products.TryGetValue(ingredient.ProductId, out var product) && product.Flags.Contains(flag));

            if (allHaveFlag) result.Add(flag);
        }

        return result;
    }
    
    private async Task SyncDishFlagsForProductAsync(Guid productId)
    {
        var affectedDishes = await db.Dishes
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.Product)
            .Where(x => x.Ingredients.Any(i => i.ProductId == productId))
            .ToListAsync();

        foreach (var dish in affectedDishes)
        {
            var ingredientRequests = dish.Ingredients
                .Select(i => new DishIngredientRequest(i.ProductId, i.Grams))
                .ToList();

            if (ingredientRequests.Count == 0) continue;

            var products = dish.Ingredients
                .Where(i => i.Product is not null)
                .ToDictionary(i => i.ProductId, i => i.Product!);

            var availableFlags = CalculateAvailableFlags(ingredientRequests, products);
            var normalizedFlags = dish.Flags.Where(availableFlags.Contains).ToHashSet();

            if (dish.Flags.SetEquals(normalizedFlags)) continue;

            dish.Flags = normalizedFlags;
            dish.UpdatedAt = DateTime.UtcNow;
        }
    }
    
    private static List<string> NormalizePhotos(List<string>? photos)
    {
        if (photos is null) return [];

        return photos
            .Where(photo => !string.IsNullOrWhiteSpace(photo))
            .Select(photo => photo.Trim())
            .Select(NormalizeDataUri)
            .ToList();
    }

    private static string NormalizeDataUri(string input)
    {
        const string marker = ";base64,";
        var markerIndex = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return string.Concat(input.Where(c => !char.IsWhiteSpace(c)));

        var prefix = input[..(markerIndex + marker.Length)];
        var payload = input[(markerIndex + marker.Length)..];
        var compactPayload = string.Concat(payload.Where(c => !char.IsWhiteSpace(c)));
        return prefix + compactPayload;
    }

    
    private static void ValidateProduct(ProductUpsertRequest request)
    {
        if (request.Name.Trim().Length < 2) throw new ArgumentException("Название продукта: минимум 2 символа");
        if ((request.Photos?.Count ?? 0) > 5) throw new ArgumentException("Можно указать не более 5 фото");
        ValidateNutrition(request.NutritionPer100g, true);
    }

    private static void ValidateDish(DishUpsertRequest request)
    {
        if (request.Name.Trim().Length < 2) throw new ArgumentException("Название блюда: минимум 2 символа");
        if ((request.Photos?.Count ?? 0) > 5) throw new ArgumentException("Можно указать не более 5 фото");
        if (request.PortionSizeGrams <= 0) throw new ArgumentException("Размер порции должен быть > 0");
        if (request.Ingredients.Count == 0) throw new ArgumentException("Нужно добавить минимум 1 продукт");
        if (request.Ingredients.Any(x => x.Grams <= 0)) throw new ArgumentException("Вес ингредиента должен быть > 0");
    }

    private static void ValidateNutrition(Nutrition nutrition, bool isPer100g, double? portionSizeGrams = null)
    {
        if (nutrition.Calories < 0) throw new ArgumentException("Калорийность должна быть >= 0");
        if (nutrition.Proteins < 0) throw new ArgumentException("Белки должны быть >= 0");
        if (nutrition.Fats < 0) throw new ArgumentException("Жиры должны быть >= 0");
        if (nutrition.Carbs < 0) throw new ArgumentException("Углеводы должны быть >= 0");
        if (isPer100g)
        {
            if (nutrition.Proteins > 100) throw new ArgumentException("Белки должны быть в диапазоне 0..100");
            if (nutrition.Fats > 100) throw new ArgumentException("Жиры должны быть в диапазоне 0..100");
            if (nutrition.Carbs > 100) throw new ArgumentException("Углеводы должны быть в диапазоне 0..100");
        }

        var maxBjuSum = isPer100g ? 100 : portionSizeGrams ?? 100;
        if (nutrition.Proteins + nutrition.Fats + nutrition.Carbs > maxBjuSum)
        {
            var target = isPer100g ? "на 100 г" : $"на порцию ({maxBjuSum:0.##} г)";
            throw new ArgumentException($"Сумма БЖУ {target} не может превышать {maxBjuSum:0.##}");
        }
    }
}
