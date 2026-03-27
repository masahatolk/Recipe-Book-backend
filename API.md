# API endpoints

Base URL: `http://localhost:8080`

## Products

- `GET /api/products?category=&cookingRequirement=&flag=VEGAN&query=&sortBy=name|calories|proteins|fats|carbs&direction=asc|desc`
- `GET /api/products/{id}`
- `POST /api/products`
- `PUT /api/products/{id}`
- `DELETE /api/products/{id}` (returns `409` if product is used in dishes)

## Dishes

- `GET /api/dishes?category=&flag=VEGAN&query=`
- `GET /api/dishes/{id}`
- `POST /api/dishes`
- `PUT /api/dishes/{id}`
- `DELETE /api/dishes/{id}`
- `POST /api/dishes/calculate` — body: `[{"productId":"...","grams":120}]`

See full schema in Swagger: `http://localhost:8080/swagger`.
