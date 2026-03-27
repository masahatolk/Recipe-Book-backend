using Microsoft.AspNetCore.Mvc;
using RecipeBook.Api.Contracts;
using RecipeBook.Api.Domain;
using RecipeBook.Api.Services;

namespace RecipeBook.Api.Controllers;

[ApiController]
[Route("api/dishes")]
public class DishesController(RecipeService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Dish>>> List(
        [FromQuery] string? category,
        [FromQuery(Name = "flag")] List<ExtraFlag>? flags,
        [FromQuery] string? query)
    {
        var result = await service.ListDishesAsync(category, flags ?? [], query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Dish>> Get(Guid id)
    {
        var dish = await service.GetDishAsync(id);
        return dish is null ? NotFound(new ApiErrorResponse("Dish not found")) : Ok(dish);
    }

    [HttpPost]
    public async Task<ActionResult<Dish>> Create([FromBody] DishUpsertRequest request)
    {
        var created = await service.CreateDishAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Dish>> Update(Guid id, [FromBody] DishUpsertRequest request)
    {
        var updated = await service.UpdateDishAsync(id, request);
        return updated is null ? NotFound(new ApiErrorResponse("Dish not found")) : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteDishAsync(id);
        return NoContent();
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<DishCalculationResponse>> Calculate([FromBody] List<DishIngredientRequest> ingredients)
    {
        var result = await service.CalculateAsync(ingredients);
        return Ok(result);
    }
}