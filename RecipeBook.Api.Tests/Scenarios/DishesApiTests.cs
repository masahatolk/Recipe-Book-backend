using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RecipeBook.Api.Contracts;
using RecipeBook.Api.Domain;
using RecipeBook.Api.Tests.Infrastructure;
using Xunit;

namespace RecipeBook.Api.Tests.Scenarios;

[Collection("Api collection")]
public class DishesApiTests(TestWebAppFactory factory) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _client = factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public static TheoryData<string, DishCategory> MacroCases => new()
    {
        {"!десерт торт", DishCategory.DESSERT},
        {"!первое борщ", DishCategory.FIRST_COURSE},
        {"!второе рагу", DishCategory.SECOND_COURSE},
        {"!напиток сок", DishCategory.DRINK},
        {"!салат цезарь", DishCategory.SALAD},
        {"!суп куриный", DishCategory.SOUP},
        {"!перекус батончик", DishCategory.SNACK}
    };
    
    public static TheoryData<double> PortionSizeData => new()
    {
        { 0 },
        { -1 },
        { 1 },
        { 250 },
        { 30 }
    };
    
    public static TheoryData<double> IngredientWeightData => new()
    {
        { 0 },
        { -1 }
    };
    
    public static TheoryData<string> DishFlagsData => new()
    {
        { "VEGAN" },
        { "GLUTEN_FREE" },
        { "SUGAR_FREE" },
        { "VEGAN&flag=GLUTEN_FREE" },
        { "VEGAN&flag=SUGAR_FREE" },
        { "GLUTEN_FREE&flag=SUGAR_FREE" }
    };
    
    public static TheoryData<string> DishCategoryFilterData => new()
    {
        { "SALAD" },
        { "SOUP" },
        { "DESSERT" },
        { "DRINK" },
        { "SNACK" }
    };
    
    public static TheoryData<double, double> CalculatedIngredientPortionData => new()
    {
        { 100, 100 },
        { 50, 200 },
        { 25, 400 },
        { 10, 1000 },
        { 100, 30 }
    };
    [Theory]
    [MemberData(nameof(MacroCases))]
    public async Task CreateDish_ShouldApplyMacroCategory_WhenCategoryNotProvided(string nameWithMacro, DishCategory expected)
    {
        var product = await CreateProductAsync("banana", 1, 0, 23, [ExtraFlag.VEGAN, ExtraFlag.SUGAR_FREE, ExtraFlag.GLUTEN_FREE]);
        var request = BuildDishRequest(nameWithMacro, [new DishIngredientRequest(product.Id, 120)], category: null);

        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        var dish = await response.Content.ReadFromJsonAsync<Dish>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        dish!.Category.Should().Be(expected);
        dish.Name.Should().NotContain("!");
    }

    [Fact]
    public async Task CreateDish_ShouldRespectManualCategory_OverMacro()
    {
        var product = await CreateProductAsync("apple", 0.4, 0.2, 10, [ExtraFlag.VEGAN]);
        var request = BuildDishRequest("!десерт яблоко", [new DishIngredientRequest(product.Id, 100)], DishCategory.SALAD);

        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        var dish = await response.Content.ReadFromJsonAsync<Dish>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        dish!.Category.Should().Be(DishCategory.SALAD);
    }

    [Theory]
    [MemberData(nameof(PortionSizeData))]
    public async Task CreateDish_ShouldValidatePortionSize(double portion)
    {
        var product = await CreateProductAsync("beans", 9, 0.5, 20, [ExtraFlag.VEGAN]);
        var request = new DishUpsertRequest("portion-check", null, new Nutrition(), [new DishIngredientRequest(product.Id, 100)], portion, DishCategory.SALAD, []);

        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        var expected = portion <= 0
            ? HttpStatusCode.BadRequest
            : (9 + 0.5 + 20) > portion
                ? HttpStatusCode.BadRequest
                : HttpStatusCode.Created;
        
        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(IngredientWeightData))]
    public async Task CreateDish_ShouldValidateIngredientWeight(double grams)
    {
        var product = await CreateProductAsync("rice", 7, 1, 77, [ExtraFlag.GLUTEN_FREE]);
        var request = new DishUpsertRequest("bad-gram", null, new Nutrition(), [new DishIngredientRequest(product.Id, grams)], 200, DishCategory.SECOND_COURSE, []);

        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDish_ShouldRequireAtLeastOneIngredient()
    {
        var request = new DishUpsertRequest("no-ingredients", null, new Nutrition(), [], 200, DishCategory.SALAD, []);
        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(DishFlagsData))]
    public async Task ListDishes_ShouldFilterByFlags(string flags)
    {
        var p = await CreateProductAsync("f", 1, 1, 1, [ExtraFlag.VEGAN, ExtraFlag.GLUTEN_FREE, ExtraFlag.SUGAR_FREE]);
        await CreateDishAsync(BuildDishRequest($"dish-{Guid.NewGuid():N}", [new DishIngredientRequest(p.Id, 50)], DishCategory.SALAD, [ExtraFlag.VEGAN, ExtraFlag.GLUTEN_FREE, ExtraFlag.SUGAR_FREE]));

        var response = await _client.GetAsync($"/api/dishes?flag={flags}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(DishCategoryFilterData))]
    public async Task ListDishes_ShouldAcceptCategoryFilter(string category)
    {
        var response = await _client.GetAsync($"/api/dishes?category={category}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateDish_ShouldDropUnavailableFlags_AfterProductFlagChange()
    {
        var product = await CreateProductAsync("beans", 9, 0.5, 20, [ExtraFlag.VEGAN]);
        var dish = await CreateDishAsync(BuildDishRequest("Бобы", [new DishIngredientRequest(product.Id, 100)], DishCategory.SALAD, [ExtraFlag.VEGAN]));

        var updateProduct = new ProductUpsertRequest(product.Name, null,
            new Nutrition { Calories = 120, Proteins = 9, Fats = 0.5, Carbs = 20 },
            null, ProductCategory.GRAINS, CookingRequirement.REQUIRES_COOKING, []);

        var updateProductResponse = await _client.PutAsJsonAsync($"/api/products/{product.Id}", updateProduct);
        updateProductResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDish = await _client.GetFromJsonAsync<Dish>($"/api/dishes/{dish.Id}", JsonOptions);
        getDish!.Flags.Should().NotContain(ExtraFlag.VEGAN);
    }

    [Fact]
    public async Task CalculateEndpoint_ShouldReturnOk_ForValidIngredients()
    {
        var product = await CreateProductAsync("calc", 10, 5, 20, [ExtraFlag.VEGAN]);
        var req = new List<DishIngredientRequest> { new(product.Id, 100) };

        var response = await _client.PostAsJsonAsync("/api/dishes/calculate", req);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(CalculatedIngredientPortionData))]
    public async Task CreateDish_FromCalculatedIngredients_ShouldRespectPortionRules(double grams, double portion)
    {
        var proteinsPer100g = 10d;
        var fatsPer100g = 5d;
        var carbsPer100g = 20d;

        var product = await CreateProductAsync("calc-dish", proteinsPer100g, fatsPer100g, carbsPer100g, [ExtraFlag.VEGAN]);
        var req = new List<DishIngredientRequest> { new(product.Id, grams) };

        var dishReq = new DishUpsertRequest($"calc-{Guid.NewGuid():N}", null, new Nutrition(), req, portion, DishCategory.SECOND_COURSE, []);
        var createResp = await _client.PostAsJsonAsync("/api/dishes", dishReq);

        var bjuSumPerPortion = grams * (proteinsPer100g + fatsPer100g + carbsPer100g) / 100d;
        var expected = bjuSumPerPortion > portion
            ? HttpStatusCode.BadRequest
            : HttpStatusCode.Created;

        createResp.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DishCrud_HappyPath()
    {
        var product = await CreateProductAsync("crud-dish", 5, 5, 5, [ExtraFlag.VEGAN]);
        var dish = await CreateDishAsync(BuildDishRequest("crud", [new DishIngredientRequest(product.Id, 100)], DishCategory.SALAD));

        var byId = await _client.GetAsync($"/api/dishes/{dish.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = BuildDishRequest("crud updated", [new DishIngredientRequest(product.Id, 150)], DishCategory.SALAD);
        var updResp = await _client.PutAsJsonAsync($"/api/dishes/{dish.Id}", update);
        updResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var delResp = await _client.DeleteAsync($"/api/dishes/{dish.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<Product> CreateProductAsync(string name, double proteins, double fats, double carbs, HashSet<ExtraFlag> flags)
    {
        var request = new ProductUpsertRequest(name + Guid.NewGuid().ToString("N")[..4], null,
            new Nutrition { Calories = 100, Proteins = proteins, Fats = fats, Carbs = carbs },
            null, ProductCategory.FROZEN, CookingRequirement.READY_TO_EAT, flags);
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Product>(JsonOptions))!;
    }

    private DishUpsertRequest BuildDishRequest(string name, List<DishIngredientRequest> ingredients, DishCategory? category, HashSet<ExtraFlag>? flags = null)
        => new(name, null, new Nutrition(), ingredients, 250, category, flags ?? []);

    private async Task<Dish> CreateDishAsync(DishUpsertRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/dishes", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Dish>(JsonOptions))!;
    }
}
