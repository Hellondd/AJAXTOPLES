using WebApplication3.Application.Orders.CancelOrder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();          // MVC (Views)
builder.Services.AddControllers();                    // REST (без views) — либо совместить:
// Проще: builder.Services.AddControllersWithViews(); — покрывает оба.

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=shop.db"));
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddScoped<CreateOrderUseCase>();
builder.Services.AddScoped<GetOrderUseCase>();
builder.Services.AddScoped<CancelOrderUseCase>();

// ProblemDetails как стандарт ошибок
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// REST
app.MapControllers();

app.Run();