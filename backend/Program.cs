using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using backend.Data;
using backend.Config; // Giả sử bạn có một thư mục Config chứa các cấu hình như JwtConfig và EmailSettings
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Đọc cấu hình từ appsettings.json
var configuration = builder.Configuration;

// Cấu hình DbContext kết nối với MySQL
builder.Services.AddDbContext<MyAppContext>(options =>
    options.UseMySql(configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(8, 0, 21))));

// Cấu hình JWT Authentication
var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

// Cấu hình email từ appsettings.json
builder.Services.Configure<EmailSettings>(configuration.GetSection("Email"));

// Cấu hình CORS để cho phép React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // React frontend URL
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Thêm Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

// Cấu hình Swagger cho API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Kiểm tra môi trường để bật Swagger khi phát triển
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sử dụng CORS để cho phép các yêu cầu từ React frontend
app.UseCors("AllowReactApp");

// Bật các middleware cần thiết
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map các controller
app.MapControllers();

// Chạy ứng dụng
app.Run();
