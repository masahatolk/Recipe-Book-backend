using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecipeBook.Api.Domain;

namespace RecipeBook.Api.Data;

public class RecipeBookDbContext(DbContextOptions<RecipeBookDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var listStringConverter = new ValueConverter<List<string>, string>(
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
            json => string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? []);

        var flagsConverter = new ValueConverter<HashSet<ExtraFlag>, string>(
            set => JsonSerializer.Serialize(set, (JsonSerializerOptions?)null),
            json => string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<HashSet<ExtraFlag>>(json, (JsonSerializerOptions?)null) ?? []);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Photos).HasConversion(listStringConverter);
            entity.Property(x => x.Flags).HasConversion(flagsConverter);
            entity.OwnsOne(x => x.NutritionPer100g);
        });

        modelBuilder.Entity<Dish>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Photos).HasConversion(listStringConverter);
            entity.Property(x => x.Flags).HasConversion(flagsConverter);
            entity.OwnsOne(x => x.NutritionPerPortion);
        });

        modelBuilder.Entity<DishIngredient>(entity =>
        {
            entity.HasKey(x => new { x.DishId, x.ProductId });
            entity.HasOne(x => x.Dish)
                .WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.DishId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.DishIngredients)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
